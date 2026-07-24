using Godot;
using System.Collections.Generic;

// Multiplayer hub (autoload "Net", /root/Net). Host-authoritative phase 1:
// - Anyone can host (custom port, default 7777) or join by IP. Max 5 players total.
// - The host simulates the world; clients render host-synced puppet monsters.
// - Player position/map state is broadcast peer-to-peer (relayed by the server).
// - Client damage on puppet monsters is forwarded to the host; kill rewards
//   (XP/gold) are sent back to the killer. Combat *against* players, loot drops,
//   capture and companion sync are NOT synced yet (next phase).
public partial class NetworkManager : Node
{
	public const int MaxPlayers = 5;
	public const int DefaultPort = 7777;
	private const float PlayerStateInterval = 1.0f / 15.0f;
	private const float MonsterStateInterval = 1.0f / 10.0f;

	public static NetworkManager? Instance { get; private set; }

	public enum NetMode { Offline, Host, Client }

	public NetMode Mode { get; private set; } = NetMode.Offline;
	public bool IsHost => Mode == NetMode.Host;
	public bool IsClient => Mode == NetMode.Client;
	public bool IsOnline => Mode != NetMode.Offline;
	public int WorldSeed { get; private set; }
	public string LocalPlayerName { get; private set; } = "Player";

	// Set by World on _Ready/_ExitTree so RPC handlers can reach the live world.
	public World? ActiveWorld { get; set; }

	// Fired on the joining client once the server sent the world seed.
	public event System.Action? JoinWelcomed;
	public event System.Action<string>? JoinFailed;

	private readonly Dictionary<long, string> _playerNames = new();
	private readonly Dictionary<long, RemotePlayerPuppet> _playerPuppets = new();
	// Per-owner companion puppets: ownerPeerId -> (partySlot -> puppet).
	private readonly Dictionary<long, Dictionary<int, RemoteCompanionPuppet>> _companionPuppets = new();
	private float _companionStateRemaining;
	private float _companionRosterRemaining;
	private bool _companionRosterDirty;
	private const float CompanionStateInterval = 1.0f / 10.0f;
	private const float CompanionRosterInterval = 3.0f;
	private const int MaxSyncedCompanions = 8;
	// Host-only outbox: gift mail for players not currently connected, keyed by
	// name and flushed when that player next joins (persisted in the host save).
	private readonly List<PendingMailSaveData> _pendingMail = new();

	// Party (自由組隊). Host authority: _leaderOf maps each member peer to its
	// party's leader peer. Every client mirrors its own party's member list.
	private readonly Dictionary<long, long> _leaderOf = new();
	private readonly List<string> _localPartyNames = new();
	public IReadOnlyList<string> LocalPartyNames => _localPartyNames;
	public event System.Action<long, string>? PartyInviteReceived;
	public event System.Action? PartyChanged;
	private float _playerStateRemaining;
	private float _monsterStateRemaining;

	public override void _Ready()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
		LocalPlayerName = MakeLocalPlayerName();
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	private static string MakeLocalPlayerName()
	{
		string name = System.Environment.UserName;
		return string.IsNullOrWhiteSpace(name) ? LocaleText.T("net.player.default_name") : name;
	}

	// ---------------------------------------------------------------- lifecycle

	// Returns an empty string on success, otherwise an error description.
	public string CreateServer(int port)
	{
		if (IsOnline)
		{
			ResetSession();
		}

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateServer(port, MaxPlayers - 1);
		if (error != Error.Ok)
		{
			return error.ToString();
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Host;
		// Best-effort UPnP auto port-forward so most players can host without
		// manual router setup (off-thread; failures fall back to manual/relay).
		int forwardPort = port;
		System.Threading.Tasks.Task.Run(() => NetworkDiagnostics.TryOpenPort(forwardPort));
		WorldSeed = (int)(GD.Randi() % int.MaxValue);
		if (WorldSeed == 0)
		{
			WorldSeed = 1;
		}
		_playerNames[1] = LocalPlayerName;
		return string.Empty;
	}

	// Host-only: use an existing world's saved seed so a hosted world matches its
	// single-player layout. Call after CreateServer, before entering the world.
	public void OverrideWorldSeed(int seed)
	{
		if (IsHost && seed != 0)
		{
			WorldSeed = seed;
		}
	}

	public string JoinServer(string address, int port)
	{
		if (IsOnline)
		{
			ResetSession();
		}

		var peer = new ENetMultiplayerPeer();
		Error error = peer.CreateClient(address, port);
		if (error != Error.Ok)
		{
			return error.ToString();
		}

		Multiplayer.MultiplayerPeer = peer;
		Mode = NetMode.Client;
		return string.Empty;
	}

	public void ResetSession()
	{
		if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer is not OfflineMultiplayerPeer)
		{
			Multiplayer.MultiplayerPeer.Close();
		}
		Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();
		Mode = NetMode.Offline;
		WorldSeed = 0;
		_playerNames.Clear();
		_leaderOf.Clear();
		SetLocalPartyMirror(System.Array.Empty<string>());
		ClearPlayerPuppets();
	}

	public void ClearPlayerPuppets()
	{
		foreach (RemotePlayerPuppet puppet in _playerPuppets.Values)
		{
			if (IsInstanceValid(puppet))
			{
				puppet.QueueFree();
			}
		}
		_playerPuppets.Clear();

		foreach (Dictionary<int, RemoteCompanionPuppet> owner in _companionPuppets.Values)
		{
			foreach (RemoteCompanionPuppet companion in owner.Values)
			{
				if (IsInstanceValid(companion))
				{
					companion.QueueFree();
				}
			}
		}
		_companionPuppets.Clear();
	}

	private void RemoveCompanionPuppetsFor(long peerId)
	{
		if (!_companionPuppets.TryGetValue(peerId, out Dictionary<int, RemoteCompanionPuppet>? owner))
		{
			return;
		}

		foreach (RemoteCompanionPuppet companion in owner.Values)
		{
			if (IsInstanceValid(companion))
			{
				companion.QueueFree();
			}
		}

		_companionPuppets.Remove(peerId);
	}

	public string GetPlayerName(long peerId)
	{
		return _playerNames.TryGetValue(peerId, out string? name) ? name : LocaleText.T("net.player.default_name");
	}

	public readonly record struct ConnectedPlayer(long PeerId, string Name, string MapId, int Tier, bool IsLocal);

	// Everyone currently in the session (local player first), with the map/tier
	// instance they are in — drives the HUD party list and the invite panel.
	public List<ConnectedPlayer> GetConnectedPlayers()
	{
		var players = new List<ConnectedPlayer>();
		if (!IsOnline || ActiveWorld == null || !IsInstanceValid(ActiveWorld))
		{
			return players;
		}

		string localMap = ActiveWorld.ActiveMapId;
		players.Add(new ConnectedPlayer(Multiplayer.GetUniqueId(), LocalPlayerName, localMap, ActiveWorld.GetSelectedTier(localMap), true));
		foreach (KeyValuePair<long, RemotePlayerPuppet> entry in _playerPuppets)
		{
			if (IsInstanceValid(entry.Value))
			{
				players.Add(new ConnectedPlayer(entry.Key, GetPlayerName(entry.Key), entry.Value.MapId, entry.Value.Tier, false));
			}
		}

		return players;
	}

	// Use the player's chosen character name (not the OS user name) for multiplayer.
	// Broadcasts the new name so every connected peer relabels this player.
	public void SetLocalPlayerName(string name)
	{
		string sanitized = SanitizeName(name);
		if (sanitized == LocalPlayerName)
		{
			return;
		}

		LocalPlayerName = sanitized;
		if (!IsOnline)
		{
			return;
		}

		_playerNames[Multiplayer.GetUniqueId()] = sanitized;
		Rpc(MethodName.ReceivePlayerName, sanitized);
	}

	// ---------------------------------------------------------------- events

	private void OnPeerConnected(long peerId)
	{
		// Everyone introduces themselves to the newcomer directly.
		RpcId(peerId, MethodName.ReceivePlayerName, LocalPlayerName);
	}

	private void OnPeerDisconnected(long peerId)
	{
		string name = GetPlayerName(peerId);
		_playerNames.Remove(peerId);
		if (_playerPuppets.TryGetValue(peerId, out RemotePlayerPuppet? puppet))
		{
			if (IsInstanceValid(puppet))
			{
				puppet.QueueFree();
			}
			_playerPuppets.Remove(peerId);
		}
		RemoveCompanionPuppetsFor(peerId);
		HandlePartyDisconnect(peerId);
		PostWorldMessage(LocaleText.F("system.net.player_left", name), new Color(1.0f, 0.72f, 0.5f));
	}

	private void OnConnectedToServer()
	{
		RpcId(1, MethodName.ServerReceiveHello, LocalPlayerName);
	}

	private void OnConnectionFailed()
	{
		ResetSession();
		JoinFailed?.Invoke(LocaleText.T("net.error.connect_failed"));
	}

	private void OnServerDisconnected()
	{
		ResetSession();
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.ClearNetworkPuppetMonsters();
			PostWorldMessage(LocaleText.T("system.net.server_closed"), new Color(1.0f, 0.5f, 0.4f));
		}
		else
		{
			JoinFailed?.Invoke(LocaleText.T("net.error.connect_failed"));
		}
	}

	private void PostWorldMessage(string message, Color color)
	{
		PlayerController? player = ActiveWorld != null && IsInstanceValid(ActiveWorld) ? ActiveWorld.ActivePlayer : null;
		if (player != null && IsInstanceValid(player))
		{
			player.PostSystemMessage(message, color);
		}
	}

	// ---------------------------------------------------------------- handshake

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveHello(string playerName)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		long peerId = Multiplayer.GetRemoteSenderId();
		_playerNames[peerId] = SanitizeName(playerName);
		RpcId(peerId, MethodName.ClientReceiveWelcome, WorldSeed);
		PostWorldMessage(LocaleText.F("system.net.player_joined", _playerNames[peerId]), new Color(0.6f, 1.0f, 0.7f));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveWelcome(int worldSeed)
	{
		WorldSeed = worldSeed;
		JoinWelcomed?.Invoke();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceivePlayerName(string playerName)
	{
		_playerNames[Multiplayer.GetRemoteSenderId()] = SanitizeName(playerName);
	}

	private static string SanitizeName(string name)
	{
		name = name.Trim();
		if (name.Length == 0)
		{
			return LocaleText.T("net.player.default_name");
		}
		return name.Length > 24 ? name[..24] : name;
	}

	// Called by the client World once it has generated and can accept puppets.
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveWorldReady()
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		long peerId = Multiplayer.GetRemoteSenderId();
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.SendNetworkMonsterSnapshotTo(peerId);
		}

		FlushPendingMailTo(peerId, GetPlayerName(peerId));
	}

	public void NotifyWorldReady()
	{
		if (IsClient)
		{
			RpcId(1, MethodName.ServerReceiveWorldReady);
		}
	}

	// ---------------------------------------------------------------- per-frame

	public override void _Process(double delta)
	{
		if (!IsOnline || ActiveWorld == null || !IsInstanceValid(ActiveWorld))
		{
			return;
		}

		float step = (float)delta;
		_playerStateRemaining -= step;
		if (_playerStateRemaining <= 0.0f)
		{
			_playerStateRemaining = PlayerStateInterval;
			BroadcastLocalPlayerState();
		}

		if (IsHost)
		{
			_monsterStateRemaining -= step;
			if (_monsterStateRemaining <= 0.0f)
			{
				_monsterStateRemaining = MonsterStateInterval;
				ActiveWorld.BroadcastNetworkMonsterStates();
			}
		}

		_companionStateRemaining -= step;
		if (_companionStateRemaining <= 0.0f)
		{
			_companionStateRemaining = CompanionStateInterval;
			BroadcastLocalCompanionState();
		}

		_companionRosterRemaining -= step;
		if (_companionRosterDirty || _companionRosterRemaining <= 0.0f)
		{
			_companionRosterDirty = false;
			_companionRosterRemaining = CompanionRosterInterval;
			BroadcastLocalCompanionRoster();
		}

		UpdatePlayerPuppetVisibility();
	}

	// Party changed locally — push a fresh roster to peers on the next tick.
	public void MarkCompanionRosterDirty()
	{
		_companionRosterDirty = true;
	}

	private void BroadcastLocalPlayerState()
	{
		PlayerController? player = ActiveWorld?.ActivePlayer;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		string mapId = ActiveWorld!.ActiveMapId;
		Rpc(MethodName.ReceivePlayerState, player.GlobalPosition, player.Rotation.Y, mapId, ActiveWorld.GetSelectedTier(mapId));
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ReceivePlayerState(Vector3 position, float yaw, string mapId, int tier)
	{
		if (ActiveWorld == null || !IsInstanceValid(ActiveWorld))
		{
			return;
		}

		long peerId = Multiplayer.GetRemoteSenderId();
		if (!_playerPuppets.TryGetValue(peerId, out RemotePlayerPuppet? puppet) || !IsInstanceValid(puppet))
		{
			puppet = new RemotePlayerPuppet { Name = $"RemotePlayer_{peerId}" };
			ActiveWorld.AddChild(puppet);
			puppet.SetPlayerName(GetPlayerName(peerId));
			_playerPuppets[peerId] = puppet;
		}

		// Host: a remote player entered a (map, tier) instance — make sure it
		// is simulated so there is something for them to fight.
		if (IsHost && (puppet.MapId != mapId || puppet.Tier != tier))
		{
			ActiveWorld.EnsureWildInstancePopulated(mapId, tier);
		}

		puppet.SetPlayerName(GetPlayerName(peerId));
		puppet.ApplyNetworkState(position, yaw, mapId, tier);
	}

	private void UpdatePlayerPuppetVisibility()
	{
		foreach (RemotePlayerPuppet puppet in _playerPuppets.Values)
		{
			if (IsInstanceValid(puppet))
			{
				puppet.Visible = ActiveWorld!.IsInstanceVisibleLocally(puppet.MapId, puppet.Tier);
			}
		}

		// A companion is visible only when its owning player is (same instance).
		foreach (KeyValuePair<long, Dictionary<int, RemoteCompanionPuppet>> owner in _companionPuppets)
		{
			bool ownerVisible = _playerPuppets.TryGetValue(owner.Key, out RemotePlayerPuppet? ownerPuppet)
				&& IsInstanceValid(ownerPuppet) && ownerPuppet.Visible;
			foreach (RemoteCompanionPuppet companion in owner.Value.Values)
			{
				if (IsInstanceValid(companion))
				{
					companion.Visible = ownerVisible;
				}
			}
		}
	}

	// Host-side: is any remote player currently inside this (map, tier)?
	public bool IsRemoteInstanceInUse(string mapId, int tier)
	{
		foreach (RemotePlayerPuppet puppet in _playerPuppets.Values)
		{
			if (IsInstanceValid(puppet) && puppet.MapId == mapId && puppet.Tier == tier)
			{
				return true;
			}
		}

		return false;
	}

	// ---------------------------------------------------------------- monsters

	// Host → clients: a monster now exists (also used for join snapshots).
	public void BroadcastMonsterSpawn(int netId, string mapId, string nameKey, int level, int tier, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (IsHost)
		{
			Rpc(MethodName.ClientMonsterSpawn, netId, mapId, nameKey, level, tier, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	public void SendMonsterSpawnTo(long peerId, int netId, string mapId, string nameKey, int level, int tier, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (IsHost)
		{
			RpcId(peerId, MethodName.ClientMonsterSpawn, netId, mapId, nameKey, level, tier, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterSpawn(int netId, string mapId, string nameKey, int level, int tier, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterSpawn(netId, mapId, nameKey, level, tier, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	public void BroadcastMonsterStates(int[] netIds, Vector3[] positions, float[] yaws, int[] healths)
	{
		if (IsHost)
		{
			Rpc(MethodName.ClientMonsterStates, netIds, positions, yaws, healths);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ClientMonsterStates(int[] netIds, Vector3[] positions, float[] yaws, int[] healths)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterStates(netIds, positions, yaws, healths);
		}
	}

	public void BroadcastMonsterRemoved(int netId, bool defeated)
	{
		if (IsHost)
		{
			Rpc(MethodName.ClientMonsterRemoved, netId, defeated);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterRemoved(int netId, bool defeated)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterRemoved(netId, defeated);
		}
	}

	// Client → host: my companion hit puppet monster netId for rawDamage.
	public void SendMonsterDamageRequest(int netId, int rawDamage)
	{
		if (IsClient)
		{
			RpcId(1, MethodName.ServerReceiveMonsterDamage, netId, rawDamage);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveMonsterDamage(int netId, int rawDamage)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.ApplyNetworkMonsterDamage(netId, rawDamage, Multiplayer.GetRemoteSenderId());
		}
	}

	// Host → killer client: you defeated this map's boss at this tier — apply
	// your own per-player tier unlock.
	public void SendBossDefeatTo(long peerId, string mapId, int tier)
	{
		if (IsHost && peerId != 1)
		{
			RpcId(peerId, MethodName.ClientReceiveBossDefeat, mapId, tier);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveBossDefeat(string mapId, int tier)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleRemoteBossDefeat(mapId, tier);
		}
	}

	// Host → killer client: reward for a monster your damage finished off.
	public void SendKillRewardTo(long peerId, string monsterName, int experience, int gold, int sourceLevel)
	{
		if (IsHost && peerId != 1)
		{
			RpcId(peerId, MethodName.ClientReceiveKillReward, monsterName, experience, gold, sourceLevel);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveKillReward(string monsterName, int experience, int gold, int sourceLevel)
	{
		PlayerController? player = ActiveWorld != null && IsInstanceValid(ActiveWorld) ? ActiveWorld.ActivePlayer : null;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		player.GrantCombatExperience(experience, sourceLevel);
		player.AddGold(gold);
		player.PostSystemMessage(LocaleText.F("system.net.kill_reward", monsterName, experience, gold), new Color(0.72f, 1.0f, 0.78f), GameMessageChannel.Combat);
	}

	// ---------------------------------------------------------------- gift mail

	// Names of every OTHER connected player (compose recipient list).
	public List<string> GetOtherPlayerNames()
	{
		var names = new List<string>();
		if (!IsOnline)
		{
			return names;
		}

		long myId = Multiplayer.GetUniqueId();
		foreach (KeyValuePair<long, string> entry in _playerNames)
		{
			if (entry.Key != myId && !string.IsNullOrWhiteSpace(entry.Value) && !names.Contains(entry.Value))
			{
				names.Add(entry.Value);
			}
		}

		names.Sort(string.CompareOrdinal);
		return names;
	}

	private long FindPeerByName(string name)
	{
		foreach (KeyValuePair<long, string> entry in _playerNames)
		{
			if (entry.Value == name)
			{
				return entry.Key;
			}
		}

		return -1;
	}

	// Called by the local player. Host routes directly; clients ask the host to.
	public void SendMailToPlayer(string recipient, string body, string[] itemIds, int[] itemCounts)
	{
		if (IsHost)
		{
			RouteMail(LocalPlayerName, recipient, body, itemIds, itemCounts);
		}
		else if (IsClient)
		{
			RpcId(1, MethodName.ServerReceiveMail, recipient, body, itemIds, itemCounts);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveMail(string recipient, string body, string[] itemIds, int[] itemCounts)
	{
		if (!Multiplayer.IsServer())
		{
			return;
		}

		// Trust the registered name of the sender, never the payload (anti-spoof).
		string sender = GetPlayerName(Multiplayer.GetRemoteSenderId());
		RouteMail(sender, recipient, body, itemIds, itemCounts);
	}

	// Host-authoritative delivery. Online recipient → push now; otherwise queue
	// the letter until they next join (persisted in the host save).
	private void RouteMail(string sender, string recipient, string body, string[] itemIds, int[] itemCounts)
	{
		if (!IsHost)
		{
			return;
		}

		double sentUnix = Time.GetUnixTimeFromSystem();
		long targetPeer = FindPeerByName(recipient);
		if (targetPeer == 1)
		{
			DeliverMailLocally(sender, sentUnix, body, itemIds, itemCounts);
		}
		else if (targetPeer > 1)
		{
			RpcId(targetPeer, MethodName.ClientReceiveMail, sender, sentUnix, body, itemIds, itemCounts);
		}
		else
		{
			_pendingMail.Add(BuildPendingMail(recipient, sender, sentUnix, body, itemIds, itemCounts));
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveMail(string sender, double sentUnix, string body, string[] itemIds, int[] itemCounts)
	{
		DeliverMailLocally(sender, sentUnix, body, itemIds, itemCounts);
	}

	private void DeliverMailLocally(string sender, double sentUnix, string body, string[] itemIds, int[] itemCounts)
	{
		PlayerController? player = ActiveWorld != null && IsInstanceValid(ActiveWorld) ? ActiveWorld.ActivePlayer : null;
		if (player != null && IsInstanceValid(player))
		{
			player.ReceiveMail(sender, sentUnix, body, itemIds, itemCounts);
		}
	}

	private void FlushPendingMailTo(long peerId, string name)
	{
		if (!IsHost || _pendingMail.Count == 0 || string.IsNullOrWhiteSpace(name))
		{
			return;
		}

		for (int index = _pendingMail.Count - 1; index >= 0; index--)
		{
			PendingMailSaveData pending = _pendingMail[index];
			if (pending.RecipientName != name)
			{
				continue;
			}

			MailMessageSaveData mail = pending.Mail;
			(string[] ids, int[] counts) = SplitAttachments(mail.AttachedItems);
			if (peerId == 1)
			{
				DeliverMailLocally(mail.SenderName, mail.SentUnix, mail.Body, ids, counts);
			}
			else
			{
				RpcId(peerId, MethodName.ClientReceiveMail, mail.SenderName, mail.SentUnix, mail.Body, ids, counts);
			}

			_pendingMail.RemoveAt(index);
		}
	}

	private static PendingMailSaveData BuildPendingMail(string recipient, string sender, double sentUnix, string body, string[] itemIds, int[] itemCounts)
	{
		var attached = new Dictionary<string, int>();
		if (itemIds != null && itemCounts != null)
		{
			int count = System.Math.Min(itemIds.Length, itemCounts.Length);
			for (int index = 0; index < count; index++)
			{
				if (itemCounts[index] > 0)
				{
					attached[itemIds[index]] = attached.TryGetValue(itemIds[index], out int existing)
						? existing + itemCounts[index]
						: itemCounts[index];
				}
			}
		}

		return new PendingMailSaveData
		{
			RecipientName = recipient,
			Mail = new MailMessageSaveData
			{
				Id = System.Guid.NewGuid().ToString("N"),
				SenderName = sender,
				SentUnix = sentUnix,
				Body = body ?? string.Empty,
				AttachedItems = attached,
			},
		};
	}

	private static (string[] ids, int[] counts) SplitAttachments(Dictionary<string, int> attached)
	{
		if (attached == null || attached.Count == 0)
		{
			return (System.Array.Empty<string>(), System.Array.Empty<int>());
		}

		var ids = new string[attached.Count];
		var counts = new int[attached.Count];
		int index = 0;
		foreach (KeyValuePair<string, int> entry in attached)
		{
			ids[index] = entry.Key;
			counts[index] = entry.Value;
			index++;
		}

		return (ids, counts);
	}

	// Host save round-trip for the pending outbox.
	public List<PendingMailSaveData> ExportPendingMail()
	{
		return new List<PendingMailSaveData>(_pendingMail);
	}

	public void ImportPendingMail(List<PendingMailSaveData> pending)
	{
		_pendingMail.Clear();
		if (pending != null)
		{
			_pendingMail.AddRange(pending);
		}
	}

	// ---------------------------------------------------------------- companion sync

	// Stream this player's deployed companions' transforms to peers (visuals only;
	// combat stays local to each owner). Mirrors the player-state broadcast.
	private void BroadcastLocalCompanionState()
	{
		PlayerController? player = ActiveWorld?.ActivePlayer;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		IReadOnlyList<SimpleActor> party = player.ActiveParty;
		int count = Mathf.Min(party.Count, MaxSyncedCompanions);
		var ids = new List<int>();
		var positions = new List<Vector3>();
		var yaws = new List<float>();
		for (int i = 0; i < count; i++)
		{
			SimpleActor actor = party[i];
			if (!IsInstanceValid(actor) || !actor.IsCaptured || !actor.IsInActiveParty)
			{
				continue;
			}

			ids.Add(i);
			positions.Add(actor.GlobalPosition);
			yaws.Add(actor.Rotation.Y);
		}

		Rpc(MethodName.ClientCompanionStates, ids.ToArray(), positions.ToArray(), yaws.ToArray());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ClientCompanionStates(int[] slots, Vector3[] positions, float[] yaws)
	{
		if (ActiveWorld == null || !IsInstanceValid(ActiveWorld))
		{
			return;
		}

		long owner = Multiplayer.GetRemoteSenderId();
		if (!_companionPuppets.TryGetValue(owner, out Dictionary<int, RemoteCompanionPuppet>? map))
		{
			return; // wait for the roster to create the puppets
		}

		int count = Mathf.Min(slots.Length, Mathf.Min(positions.Length, yaws.Length));
		for (int i = 0; i < count; i++)
		{
			if (map.TryGetValue(slots[i], out RemoteCompanionPuppet? puppet) && IsInstanceValid(puppet))
			{
				puppet.ApplyNetworkState(positions[i], yaws[i]);
			}
		}
	}

	// Push the identity (model/name/level) of this player's deployed companions so
	// peers can spawn the right puppets. Reliable; sent on change + periodically.
	private void BroadcastLocalCompanionRoster()
	{
		PlayerController? player = ActiveWorld?.ActivePlayer;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		IReadOnlyList<SimpleActor> party = player.ActiveParty;
		int count = Mathf.Min(party.Count, MaxSyncedCompanions);
		var ids = new List<int>();
		var models = new List<string>();
		var names = new List<string>();
		var levels = new List<int>();
		for (int i = 0; i < count; i++)
		{
			SimpleActor actor = party[i];
			if (!IsInstanceValid(actor) || !actor.IsCaptured || !actor.IsInActiveParty)
			{
				continue;
			}

			ids.Add(i);
			models.Add(actor.GetExternalModelPath());
			names.Add(actor.LocalizedDisplayName);
			levels.Add(actor.Level);
		}

		Rpc(MethodName.ClientCompanionRoster, ids.ToArray(), models.ToArray(), names.ToArray(), levels.ToArray());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientCompanionRoster(int[] slots, string[] models, string[] names, int[] levels)
	{
		if (ActiveWorld == null || !IsInstanceValid(ActiveWorld))
		{
			return;
		}

		long owner = Multiplayer.GetRemoteSenderId();
		if (!_companionPuppets.TryGetValue(owner, out Dictionary<int, RemoteCompanionPuppet>? map))
		{
			map = new Dictionary<int, RemoteCompanionPuppet>();
			_companionPuppets[owner] = map;
		}

		var present = new HashSet<int>();
		int count = Mathf.Min(slots.Length, Mathf.Min(models.Length, Mathf.Min(names.Length, levels.Length)));
		for (int i = 0; i < count; i++)
		{
			int slot = slots[i];
			present.Add(slot);
			if (!map.TryGetValue(slot, out RemoteCompanionPuppet? puppet) || !IsInstanceValid(puppet))
			{
				puppet = new RemoteCompanionPuppet { Name = $"RemoteCompanion_{owner}_{slot}" };
				ActiveWorld.AddChild(puppet);
				map[slot] = puppet;
			}

			puppet.SetModel(models[i]);
			puppet.SetInfo(names[i], levels[i]);
		}

		// Drop puppets for companions no longer in the owner's synced party.
		var stale = new List<int>();
		foreach (int slot in map.Keys)
		{
			if (!present.Contains(slot))
			{
				stale.Add(slot);
			}
		}

		foreach (int slot in stale)
		{
			if (IsInstanceValid(map[slot]))
			{
				map[slot].QueueFree();
			}

			map.Remove(slot);
		}
	}

	// ---------------------------------------------------------------- party (自由組隊)

	// Local player invites another player (by peer id) to their party.
	public void InvitePlayerToParty(long targetPeer)
	{
		if (!IsOnline || targetPeer == Multiplayer.GetUniqueId())
		{
			return;
		}

		if (IsHost)
		{
			HostProcessInvite(1, targetPeer);
		}
		else
		{
			RpcId(1, MethodName.ServerRequestPartyInvite, targetPeer);
		}
	}

	// Local player answers an invite (accept/decline).
	public void RespondToPartyInvite(long inviterPeer, bool accept)
	{
		if (!IsOnline)
		{
			return;
		}

		if (IsHost)
		{
			HostProcessResponse(1, inviterPeer, accept);
		}
		else
		{
			RpcId(1, MethodName.ServerRespondPartyInvite, inviterPeer, accept);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerRequestPartyInvite(long targetPeer)
	{
		if (Multiplayer.IsServer())
		{
			HostProcessInvite(Multiplayer.GetRemoteSenderId(), targetPeer);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerRespondPartyInvite(long inviterPeer, bool accept)
	{
		if (Multiplayer.IsServer())
		{
			HostProcessResponse(Multiplayer.GetRemoteSenderId(), inviterPeer, accept);
		}
	}

	// Host: route an invite to the target (locally if the host is the target).
	private void HostProcessInvite(long inviter, long target)
	{
		if (inviter == target || !_playerNames.ContainsKey(target))
		{
			return;
		}

		string inviterName = GetPlayerName(inviter);
		if (target == 1)
		{
			PartyInviteReceived?.Invoke(inviter, inviterName);
		}
		else
		{
			RpcId(target, MethodName.ClientReceivePartyInvite, inviter, inviterName);
		}
	}

	// Host: apply an invite response and update party membership.
	private void HostProcessResponse(long responder, long inviter, bool accept)
	{
		string responderName = GetPlayerName(responder);
		if (inviter == 1)
		{
			DeliverInviteResultLocally(responderName, accept);
		}
		else
		{
			RpcId(inviter, MethodName.ClientPartyInviteResult, responderName, accept);
		}

		if (!accept)
		{
			return;
		}

		long leader = _leaderOf.TryGetValue(inviter, out long existing) ? existing : inviter;
		_leaderOf[inviter] = leader;
		_leaderOf[responder] = leader;
		BroadcastPartyForLeader(leader);
	}

	private void BroadcastPartyForLeader(long leader)
	{
		var members = new List<long> { leader };
		foreach (KeyValuePair<long, long> entry in _leaderOf)
		{
			if (entry.Value == leader && entry.Key != leader)
			{
				members.Add(entry.Key);
			}
		}

		var peers = members.ToArray();
		var names = new string[members.Count];
		for (int i = 0; i < members.Count; i++)
		{
			names[i] = GetPlayerName(members[i]);
		}

		foreach (long member in members)
		{
			if (member == 1)
			{
				SetLocalPartyMirror(names);
			}
			else
			{
				RpcId(member, MethodName.ClientPartyMembers, peers, names);
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceivePartyInvite(long inviterPeer, string inviterName)
	{
		PartyInviteReceived?.Invoke(inviterPeer, inviterName);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPartyMembers(long[] peers, string[] names)
	{
		SetLocalPartyMirror(names);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPartyInviteResult(string responderName, bool accepted)
	{
		DeliverInviteResultLocally(responderName, accepted);
	}

	private void SetLocalPartyMirror(string[] names)
	{
		_localPartyNames.Clear();
		if (names != null)
		{
			_localPartyNames.AddRange(names);
		}

		PartyChanged?.Invoke();
	}

	private void DeliverInviteResultLocally(string responderName, bool accepted)
	{
		string key = accepted ? "system.party.accepted" : "system.party.declined";
		PostWorldMessage(LocaleText.F(key, responderName), accepted ? new Color(0.7f, 1.0f, 0.78f) : new Color(1.0f, 0.78f, 0.55f));
	}

	private void HandlePartyDisconnect(long peerId)
	{
		if (!IsHost)
		{
			return;
		}

		_leaderOf.TryGetValue(peerId, out long leaderOfPeer);
		bool wasMember = _leaderOf.Remove(peerId);

		// Members led by the departing peer are orphaned — clear their party.
		var orphans = new List<long>();
		foreach (KeyValuePair<long, long> entry in _leaderOf)
		{
			if (entry.Value == peerId)
			{
				orphans.Add(entry.Key);
			}
		}

		foreach (long orphan in orphans)
		{
			_leaderOf.Remove(orphan);
			if (orphan == 1)
			{
				SetLocalPartyMirror(System.Array.Empty<string>());
			}
			else
			{
				RpcId(orphan, MethodName.ClientPartyMembers, System.Array.Empty<long>(), System.Array.Empty<string>());
			}
		}

		// A remaining party shrinks by one — refresh it.
		if (wasMember && leaderOfPeer != peerId)
		{
			BroadcastPartyForLeader(leaderOfPeer);
		}
	}
}
