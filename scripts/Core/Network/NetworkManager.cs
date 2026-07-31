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
	// The local player's chosen character model, mirrored to every peer so their
	// puppet shows the right character (not the default model).
	public string LocalPlayerModelPath { get; private set; } = string.Empty;

	// Set by World on _Ready/_ExitTree so RPC handlers can reach the live world.
	public World? ActiveWorld { get; set; }

	// Fired on the joining client once the server sent the world seed.
	public event System.Action? JoinWelcomed;
	public event System.Action<string>? JoinFailed;
	// Fired on a client already in the world when the host closes / the link drops.
	public event System.Action? ServerConnectionLost;

	private readonly Dictionary<long, string> _playerNames = new();
	private readonly Dictionary<long, string> _playerModels = new();
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
	private readonly List<long> _localPartyPeers = new();
	public IReadOnlyList<string> LocalPartyNames => _localPartyNames;
	public IReadOnlyList<long> LocalPartyPeers => _localPartyPeers;
	public long LocalPeerId => IsOnline ? Multiplayer.GetUniqueId() : 0;

	// The hunting-ground instance group of the local player: the party leader's
	// peer id when in a party, otherwise the player's own id. 0 in single-player,
	// so each party / solo player gets a separate wild-map instance.
	public int LocalGroupId
	{
		get
		{
			if (!IsOnline)
			{
				return 0;
			}

			if (_localPartyPeers.Count > 0)
			{
				return (int)_localPartyPeers[0]; // leader is broadcast first
			}

			return (int)Multiplayer.GetUniqueId();
		}
	}
	public bool LocalIsPartyLeader { get; private set; }
	public bool LocalInParty => _localPartyNames.Count > 0;
	// Only a leader (or a solo player about to become one) may send invites.
	public bool CanInviteToParty => IsOnline && (!LocalInParty || LocalIsPartyLeader);
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
		_playerModels.Clear();
		_leaderOf.Clear();
		SetLocalPartyMirror(System.Array.Empty<long>(), System.Array.Empty<string>(), -1);
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

	// Companion sync moved to NetworkManager.Companions.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

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

	// Mirror the local player's chosen character model to every peer so their
	// puppet renders the right character. Sent alongside the name on world entry.
	public void SetLocalPlayerModel(string modelPath)
	{
		LocalPlayerModelPath = modelPath ?? string.Empty;
		if (!IsOnline)
		{
			return;
		}

		_playerModels[Multiplayer.GetUniqueId()] = LocalPlayerModelPath;
		Rpc(MethodName.ReceivePlayerModel, LocalPlayerModelPath);
	}

	public string GetPlayerModel(long peerId)
	{
		return _playerModels.TryGetValue(peerId, out string? path) ? path : string.Empty;
	}

	// ---------------------------------------------------------------- events

	private void OnPeerConnected(long peerId)
	{
		// Everyone introduces themselves to the newcomer directly (name + model).
		RpcId(peerId, MethodName.ReceivePlayerName, LocalPlayerName);
		RpcId(peerId, MethodName.ReceivePlayerModel, LocalPlayerModelPath);
	}

	private void OnPeerDisconnected(long peerId)
	{
		string name = GetPlayerName(peerId);
		_playerNames.Remove(peerId);
		_playerModels.Remove(peerId);
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
		bool wasInWorld = ActiveWorld != null && IsInstanceValid(ActiveWorld);
		ResetSession();
		if (wasInWorld)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
			if (ServerConnectionLost != null)
			{
				// In-world UI shows a notice, then returns to the menu on confirm.
				ServerConnectionLost.Invoke();
			}
			else
			{
				CallDeferred(MethodName.ReturnClientToMainMenu);
			}
		}
		else
		{
			JoinFailed?.Invoke(LocaleText.T("net.error.connect_failed"));
		}
	}

	private void ReturnClientToMainMenu()
	{
		if (IsInstanceValid(this) && GetTree() != null)
		{
			GetTree().ChangeSceneToFile("res://main_menu.tscn");
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
		// The "joined" announcement is deferred to ServerReceiveWorldReady — a client
		// only counts as having joined once it has picked a character and actually
		// entered the shared world (not merely completed the TCP handshake).
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
		long peerId = Multiplayer.GetRemoteSenderId();
		_playerNames[peerId] = SanitizeName(playerName);
		if (_playerPuppets.TryGetValue(peerId, out RemotePlayerPuppet? puppet) && IsInstanceValid(puppet))
		{
			puppet.SetPlayerName(_playerNames[peerId]);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ReceivePlayerModel(string modelPath)
	{
		long peerId = Multiplayer.GetRemoteSenderId();
		_playerModels[peerId] = modelPath ?? string.Empty;
		if (_playerPuppets.TryGetValue(peerId, out RemotePlayerPuppet? puppet) && IsInstanceValid(puppet))
		{
			puppet.SetPlayerModel(_playerModels[peerId]);
		}
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

		// Send the newcomer the character (name + model) of every player already in
		// the session, so pre-existing players render correctly on the late joiner's
		// screen (their own info already reached everyone via the broadcast above).
		foreach (KeyValuePair<long, string> entry in _playerNames)
		{
			if (entry.Key == peerId)
			{
				continue;
			}

			RpcId(peerId, MethodName.ClientReceiveRosterEntry, entry.Key, entry.Value, GetPlayerModel(entry.Key));
		}

		// The client has entered the world (after character-select). Announce the
		// join now, using the character name it just sent.
		PostWorldMessage(LocaleText.F("system.net.player_joined", GetPlayerName(peerId)), new Color(0.6f, 1.0f, 0.7f));
	}

	// Server → a single client: another player's authoritative name + model. Used
	// to catch a late joiner up on everyone already present.
	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveRosterEntry(long entryPeer, string playerName, string modelPath)
	{
		_playerNames[entryPeer] = SanitizeName(playerName);
		_playerModels[entryPeer] = modelPath ?? string.Empty;
		if (_playerPuppets.TryGetValue(entryPeer, out RemotePlayerPuppet? puppet) && IsInstanceValid(puppet))
		{
			puppet.SetPlayerName(_playerNames[entryPeer]);
			puppet.SetPlayerModel(_playerModels[entryPeer]);
		}
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

	// Companion sync moved to NetworkManager.Companions.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private void BroadcastLocalPlayerState()
	{
		PlayerController? player = ActiveWorld?.ActivePlayer;
		if (player == null || !IsInstanceValid(player))
		{
			return;
		}

		string mapId = ActiveWorld!.ActiveMapId;
		Rpc(MethodName.ReceivePlayerState, player.GlobalPosition, player.Rotation.Y, mapId, ActiveWorld.GetSelectedTier(mapId), LocalGroupId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ReceivePlayerState(Vector3 position, float yaw, string mapId, int tier, int groupId)
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
			puppet.SetPlayerModel(GetPlayerModel(peerId));
			_playerPuppets[peerId] = puppet;
		}

		// Host: a remote player entered a (map, tier, group) instance — make sure
		// that group's instance is populated so they have monsters to fight.
		if (IsHost && (puppet.MapId != mapId || puppet.Tier != tier || puppet.GroupId != groupId))
		{
			ActiveWorld.EnsureWildInstancePopulated(mapId, tier, groupId);
		}

		puppet.SetPlayerName(GetPlayerName(peerId));
		puppet.ApplyNetworkState(position, yaw, mapId, tier, groupId);
	}

	private void UpdatePlayerPuppetVisibility()
	{
		foreach (RemotePlayerPuppet puppet in _playerPuppets.Values)
		{
			if (IsInstanceValid(puppet))
			{
				puppet.Visible = ActiveWorld!.IsInstanceVisibleLocally(puppet.MapId, puppet.Tier, puppet.GroupId);
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

	// Host-side: is any remote player currently inside this (map, tier, group)?
	public bool IsRemoteInstanceInUse(string mapId, int tier, int groupId)
	{
		foreach (RemotePlayerPuppet puppet in _playerPuppets.Values)
		{
			if (IsInstanceValid(puppet) && puppet.MapId == mapId && puppet.Tier == tier && puppet.GroupId == groupId)
			{
				return true;
			}
		}

		return false;
	}

	// Host-side: nearest remote player standing in this monster's instance, so a
	// host-simulated monster can chase and attack players other than the host.
	public Node3D? FindNearestRemotePlayer(string mapId, int tier, int groupId, Vector3 origin, out long peerId, out float distance)
	{
		peerId = 0;
		distance = float.MaxValue;
		RemotePlayerPuppet? best = null;
		foreach (KeyValuePair<long, RemotePlayerPuppet> entry in _playerPuppets)
		{
			RemotePlayerPuppet puppet = entry.Value;
			if (!IsInstanceValid(puppet) || puppet.MapId != mapId || puppet.Tier != tier || puppet.GroupId != groupId)
			{
				continue;
			}

			float d = origin.DistanceTo(puppet.GlobalPosition);
			if (d < distance)
			{
				distance = d;
				best = puppet;
				peerId = entry.Key;
			}
		}

		return best;
	}

	// Monster sync moved to NetworkManager.Monsters.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Gift mail moved to NetworkManager.Mail.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Companion sync moved to NetworkManager.Companions.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Party sync moved to NetworkManager.Party.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).
}
