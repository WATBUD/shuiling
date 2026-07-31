using Godot;
using System.Collections.Generic;
using System.Text.Json;

// Multiplayer half of World (host-authoritative phase 1, see NetworkManager.cs).
// Host: owns the real simulation, assigns net ids to wild monsters/bosses and
// broadcasts spawn/state/removal. Client: skips local monster simulation and
// renders "puppet" SimpleActors driven by host state; local damage on puppets
// is forwarded to the host. Caves and city NPCs remain purely local.
public partial class World
{
	private readonly record struct NetMonsterInfo(SimpleActor Actor, float VisualScale, Color AuraColor);

	private readonly Dictionary<int, NetMonsterInfo> _netMonstersById = new();
	private readonly Dictionary<int, long> _netLastDamagePeerByNetId = new();
	private readonly Dictionary<int, (long PeerId, ulong ExpiresAtMsec)> _netCaptureReservations = new();
	private readonly List<int> _netRemovalScratch = new();
	private readonly List<int> _netCaptureReservationRemovalScratch = new();
	private int _nextNetMonsterId;
	private const ulong NetworkCaptureReservationMsec = 30000;

	// Reused across ticks so the 10 Hz state broadcast allocates nothing (avoids
	// GC hitches that showed up as combat/pickup stutter in multiplayer).
	private int[] _netStateIds = System.Array.Empty<int>();
	private Vector3[] _netStatePositions = System.Array.Empty<Vector3>();
	private float[] _netStateYaws = System.Array.Empty<float>();
	private int[] _netStateHealths = System.Array.Empty<int>();
	private byte[] _netStateCaptureReady = System.Array.Empty<byte>();

	public PlayerController ActivePlayer => _player;

	private static NetworkManager? Net => NetworkManager.Instance;
	private bool IsNetworkClientWorld => Net?.IsClient == true;
	private bool IsNetworkHostWorld => Net?.IsHost == true;

	// ---------------------------------------------------------------- lifecycle

	// Called at the top of _Ready: in multiplayer everyone generates the world
	// from the host's seed so terrain/obstacles match on all peers.
	private void NetworkBeforeWorldGeneration()
	{
		if (Net != null && Net.IsOnline)
		{
			SeedValue = Net.WorldSeed;
			Net.ActiveWorld = this;
		}
	}

	// Called at the end of _Ready: a client announces it can accept puppets and
	// the host answers with a full monster snapshot.
	private void NetworkAfterWorldReady()
	{
		Net?.NotifyWorldReady();
	}

	// Whether content living in (mapId, tier) is visible to the LOCAL player
	// right now — same map, and for wild maps also the same selected tier.
	public bool IsInstanceVisibleLocally(string mapId, int tier, int groupId)
	{
		if (mapId != _activeMapId)
		{
			return false;
		}

		if (!IsWildMapId(mapId))
		{
			return true;
		}

		return tier == GetSelectedTier(mapId) && groupId == LocalGroupId();
	}

	private void NetworkOnWorldExit()
	{
		if (Net != null)
		{
			Net.ClearPlayerPuppets();
			if (Net.ActiveWorld == this)
			{
				Net.ActiveWorld = null;
			}
			Net.ResetSession();
		}
	}

	// ---------------------------------------------------------------- host side

	// Called after a wild monster/boss enters the tree on the host.
	private void RegisterNetworkMonster(SimpleActor actor, float visualScale, Color auraColor)
	{
		if (!IsNetworkHostWorld)
		{
			return;
		}

		int netId = ++_nextNetMonsterId;
		actor.NetworkMonsterId = netId;
		_netMonstersById[netId] = new NetMonsterInfo(actor, visualScale, auraColor);
		Net!.BroadcastMonsterSpawn(netId, actor.MapId, actor.DisplayName, actor.Level, actor.WorldTier, actor.GroupId, actor.Rarity,
			actor.MaxHealth, actor.CurrentHealth, actor.IsBoss, actor.BossNameKey, visualScale, auraColor, actor.Position);
	}

	public void SendNetworkMonsterSnapshotTo(long peerId)
	{
		if (!IsNetworkHostWorld)
		{
			return;
		}

		foreach (KeyValuePair<int, NetMonsterInfo> entry in _netMonstersById)
		{
			SimpleActor actor = entry.Value.Actor;
			if (!IsInstanceValid(actor) || actor.IsDefeated || actor.IsCaptured)
			{
				continue;
			}

			Net!.SendMonsterSpawnTo(peerId, entry.Key, actor.MapId, actor.DisplayName, actor.Level, actor.WorldTier, actor.GroupId, actor.Rarity,
				actor.MaxHealth, actor.CurrentHealth, actor.IsBoss, actor.BossNameKey,
				entry.Value.VisualScale, entry.Value.AuraColor, actor.Position);
		}
	}

	// Host tick (throttled by NetworkManager): sweep dead/gone monsters, then
	// broadcast a position/health batch for the rest.
	public void BroadcastNetworkMonsterStates()
	{
		if (!IsNetworkHostWorld)
		{
			return;
		}

		CleanupExpiredNetworkCaptureReservations();
		_netRemovalScratch.Clear();
		foreach (KeyValuePair<int, NetMonsterInfo> entry in _netMonstersById)
		{
			SimpleActor actor = entry.Value.Actor;
			if (!IsInstanceValid(actor) || actor.IsQueuedForDeletion() || actor.IsDefeated || actor.IsCaptured)
			{
				_netRemovalScratch.Add(entry.Key);
			}
		}

		foreach (int netId in _netRemovalScratch)
		{
			SimpleActor actor = _netMonstersById[netId].Actor;
			bool defeated = IsInstanceValid(actor) && actor.IsDefeated;
			Net!.BroadcastMonsterRemoved(netId, defeated);
			_netMonstersById.Remove(netId);
			_netLastDamagePeerByNetId.Remove(netId);
			_netCaptureReservations.Remove(netId);
		}

		int count = _netMonstersById.Count;
		if (count == 0)
		{
			return;
		}

		// Reuse buffers; only reallocate when the population size changes.
		if (_netStateIds.Length != count)
		{
			_netStateIds = new int[count];
			_netStatePositions = new Vector3[count];
			_netStateYaws = new float[count];
			_netStateHealths = new int[count];
			_netStateCaptureReady = new byte[count];
		}

		int index = 0;
		foreach (KeyValuePair<int, NetMonsterInfo> entry in _netMonstersById)
		{
			SimpleActor actor = entry.Value.Actor;
			_netStateIds[index] = entry.Key;
			_netStatePositions[index] = actor.GlobalPosition;
			_netStateYaws[index] = actor.Rotation.Y;
			_netStateHealths[index] = actor.CurrentHealth;
			_netStateCaptureReady[index] = actor.CaptureReady ? (byte)1 : (byte)0;
			index++;
		}

		Net!.BroadcastMonsterStates(_netStateIds, _netStatePositions, _netStateYaws, _netStateHealths, _netStateCaptureReady);
	}

	// Host: a wild monster just died — broadcast its removal immediately so
	// clients don't wait for the next periodic sweep (kills feel instant).
	public void OnNetworkMonsterDefeated(SimpleActor actor)
	{
		if (!IsNetworkHostWorld || actor == null)
		{
			return;
		}

		int netId = actor.NetworkMonsterId;
		if (netId < 0 || !_netMonstersById.ContainsKey(netId))
		{
			return;
		}

		Net!.BroadcastMonsterRemoved(netId, true);
		_netMonstersById.Remove(netId);
		_netLastDamagePeerByNetId.Remove(netId);
		_netCaptureReservations.Remove(netId);
	}

	// A client's companion hit one of our monsters; host applies real damage.
	// The killer gets XP/gold credit via a direct reward RPC.
	public void ApplyNetworkMonsterDamage(int netId, int rawDamage, long attackerPeerId)
	{
		if (!IsNetworkHostWorld
			|| !_netMonstersById.TryGetValue(netId, out NetMonsterInfo info)
			|| !IsInstanceValid(info.Actor)
			|| info.Actor.IsDefeated)
		{
			return;
		}

		_netLastDamagePeerByNetId[netId] = attackerPeerId;
		info.Actor.ReceiveDamage(Mathf.Clamp(rawDamage, 1, 99999), null);
		if (info.Actor.IsDefeated)
		{
			Net!.SendKillRewardTo(attackerPeerId, info.Actor.LocalizedDisplayName, info.Actor.ExperienceReward, info.Actor.GoldReward, info.Actor.Level);
			if (info.Actor.IsBoss)
			{
				// Per-player progression: the killer unlocks their own next tier.
				Net.SendBossDefeatTo(attackerPeerId, GetTierMapId(info.Actor.MapId), info.Actor.WorldTier);
			}
		}
	}

	public void ApplyNetworkMonsterCaptureNetHit(int netId, long peerId)
	{
		if (!TryGetNetworkCaptureTarget(netId, peerId, out SimpleActor actor))
		{
			return;
		}

		actor.GrantCaptureProtection(8.0f);
		if (!actor.CaptureReady)
		{
			actor.AddCaptureStagger(actor.MaxStagger * 0.25f);
		}
	}

	public void SetNetworkMonsterCaptureLock(int netId, long peerId, bool locked)
	{
		if (!locked)
		{
			if (_netCaptureReservations.TryGetValue(netId, out var activeReservation) && activeReservation.PeerId == peerId)
			{
				_netCaptureReservations.Remove(netId);
				if (_netMonstersById.TryGetValue(netId, out NetMonsterInfo activeInfo) && IsInstanceValid(activeInfo.Actor))
				{
					activeInfo.Actor.EndCaptureProtection();
				}
			}
			return;
		}

		if (!TryGetNetworkCaptureTarget(netId, peerId, out SimpleActor actor))
		{
			return;
		}

		ulong now = Time.GetTicksMsec();
		if (!actor.CaptureReady
			|| (_netCaptureReservations.TryGetValue(netId, out var reservation)
				&& reservation.PeerId != peerId
				&& reservation.ExpiresAtMsec > now))
		{
			return;
		}

		_netCaptureReservations[netId] = (peerId, now + NetworkCaptureReservationMsec);
		actor.GrantCaptureProtection(NetworkCaptureReservationMsec / 1000.0f);
		actor.SetCaptureLocked(true);
	}

	public void TryGrantNetworkMonsterCapture(int netId, long peerId)
	{
		ulong now = Time.GetTicksMsec();
		if (!TryGetNetworkCaptureTarget(netId, peerId, out SimpleActor actor)
			|| !_netCaptureReservations.TryGetValue(netId, out var reservation)
			|| reservation.PeerId != peerId
			|| reservation.ExpiresAtMsec <= now)
		{
			Net!.SendMonsterCaptureDenied(peerId, netId);
			return;
		}

		ActorSaveData data = actor.ExportSaveData();
		string actorJson = JsonSerializer.Serialize(data);
		_netMonstersById.Remove(netId);
		_netLastDamagePeerByNetId.Remove(netId);
		_netCaptureReservations.Remove(netId);
		Net!.SendMonsterCaptureGranted(peerId, netId, actorJson);
		Net.BroadcastMonsterRemoved(netId, false);
		actor.QueueFree();
	}

	private bool TryGetNetworkCaptureTarget(int netId, long peerId, out SimpleActor actor)
	{
		actor = null!;
		if (!IsNetworkHostWorld
			|| !_netMonstersById.TryGetValue(netId, out NetMonsterInfo info)
			|| !IsInstanceValid(info.Actor)
			|| info.Actor.IsDefeated
			|| info.Actor.IsCaptured
			|| !Net!.IsRemotePlayerNear(peerId, info.Actor.MapId, info.Actor.WorldTier, info.Actor.GroupId, info.Actor.GlobalPosition, 36.0f))
		{
			return false;
		}

		actor = info.Actor;
		return true;
	}

	private void CleanupExpiredNetworkCaptureReservations()
	{
		ulong now = Time.GetTicksMsec();
		_netCaptureReservationRemovalScratch.Clear();
		foreach (KeyValuePair<int, (long PeerId, ulong ExpiresAtMsec)> entry in _netCaptureReservations)
		{
			if (entry.Value.ExpiresAtMsec > now
				&& _netMonstersById.TryGetValue(entry.Key, out NetMonsterInfo info)
				&& IsInstanceValid(info.Actor))
			{
				continue;
			}

			if (_netMonstersById.TryGetValue(entry.Key, out NetMonsterInfo expiredInfo) && IsInstanceValid(expiredInfo.Actor))
			{
				expiredInfo.Actor.EndCaptureProtection();
			}
			_netCaptureReservationRemovalScratch.Add(entry.Key);
		}

		foreach (int netId in _netCaptureReservationRemovalScratch)
		{
			_netCaptureReservations.Remove(netId);
		}
	}

	// Client-side: mirror a monster's capture-protection flag (shield visual).
	public void SetNetworkMonsterCaptureProtection(int netId, bool protectedState)
	{
		if (_netMonstersById.TryGetValue(netId, out NetMonsterInfo info) && IsInstanceValid(info.Actor))
		{
			info.Actor.SetCaptureProtectedVisual(protectedState);
		}
	}

	// A boss WE killed on the host's world — apply our own tier unlock.
	public void HandleRemoteBossDefeat(string mapId, int tier)
	{
		if (IsNetworkClientWorld)
		{
			TryUnlockNextTier(mapId, tier);
		}
	}

	// ---------------------------------------------------------------- client side

	public void HandleNetworkMonsterSpawn(int netId, string mapId, string nameKey, int level, int tier, int groupId, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (!IsNetworkClientWorld)
		{
			return;
		}

		if (_netMonstersById.TryGetValue(netId, out NetMonsterInfo existing) && IsInstanceValid(existing.Actor))
		{
			existing.Actor.ApplyNetworkState(position, existing.Actor.Rotation.Y, health, health <= existing.Actor.EffectiveMaxHealth * existing.Actor.CaptureHealthThreshold);
			return;
		}

		if (!_wildMapRootsById.ContainsKey(mapId))
		{
			return;
		}

		SimpleActor actor = CreateActor(true, mapId, nameKey, "", level);
		// Approximate attack/defense with the base formula: puppets never fight
		// locally, these values are only shown in info panels.
		actor.ConfigureStats(nameKey, level, maxHealth, 9 + level * 4, 5 + level * 3, 0, 0);
		actor.WorldTier = tier;
		actor.GroupId = groupId;

		if (isBoss)
		{
			actor.Name = $"NetBoss_{mapId}_t{tier}_g{groupId}";
			actor.ConfigureBoss(bossNameKey, string.Empty);
			ScaleActorVisualChildren(actor, visualScale);
			ScaleBossCollision(actor, visualScale);
			AddBossAura(actor, auraColor, visualScale);
			_wildBossesByInstance[WildInstanceKey(mapId, tier, groupId)] = actor;
		}
		else
		{
			if (visualScale > 1.001f)
			{
				ScaleActorVisualChildren(actor, visualScale);
			}

			// Rarity is host-rolled; the client only mirrors the display (nameplate
			// colour + star, aura and bigger body). Stats already arrive via health.
			if (rarity > MonsterRarity.Common)
			{
				actor.Rarity = rarity;
				float rarityScale = MonsterRarity.VisualScale(rarity);
				if (rarityScale > 1.001f)
				{
					ScaleActorVisualChildren(actor, rarityScale);
				}
				if (MonsterRarity.HasAura(rarity))
				{
					AddBossAura(actor, MonsterRarity.Color(rarity), rarityScale);
				}
				actor.RefreshNameplateDisplay();
			}
		}

		actor.Position = position;
		actor.HomePosition = position;
		_actorsRoot.AddChild(actor);
		actor.CurrentHealth = Mathf.Clamp(health, 0, actor.EffectiveMaxHealth);
		actor.SetNetworkPuppet(netId);
		actor.ApplyNetworkState(position, actor.Rotation.Y, health, actor.HealthRatio <= actor.CaptureHealthThreshold);
		// Puppet physics on when it's in the local player's instance (see
		// ApplyActorInstanceState) so it moves toward streamed positions and stays
		// targetable; hidden + inert otherwise.
		ApplyActorInstanceState(actor);
		_netMonstersById[netId] = new NetMonsterInfo(actor, visualScale, auraColor);

		if (isBoss && mapId == _activeMapId && tier == GetSelectedTier(mapId) && groupId == LocalGroupId())
		{
			UpdateActiveBossHud(false);
		}
	}

	public void HandleNetworkMonsterStates(int[] netIds, Vector3[] positions, float[] yaws, int[] healths, byte[] captureReady)
	{
		if (!IsNetworkClientWorld)
		{
			return;
		}

		int count = Mathf.Min(Mathf.Min(netIds.Length, positions.Length), Mathf.Min(yaws.Length, Mathf.Min(healths.Length, captureReady.Length)));
		for (int index = 0; index < count; index++)
		{
			if (_netMonstersById.TryGetValue(netIds[index], out NetMonsterInfo info) && IsInstanceValid(info.Actor))
			{
				info.Actor.ApplyNetworkState(positions[index], yaws[index], healths[index], captureReady[index] != 0);
			}
		}
	}

	public void HandleNetworkMonsterCaptureGranted(int netId, string actorJson)
	{
		if (!IsNetworkClientWorld || !_netMonstersById.TryGetValue(netId, out NetMonsterInfo info) || !IsInstanceValid(info.Actor))
		{
			return;
		}

		ActorSaveData? data;
		try
		{
			data = JsonSerializer.Deserialize<ActorSaveData>(actorJson);
		}
		catch (JsonException)
		{
			data = null;
		}

		if (data == null)
		{
			HandleNetworkMonsterCaptureDenied(netId);
			return;
		}

		_netMonstersById.Remove(netId);
		if (!_player.AcceptNetworkCapturedActor(info.Actor, data))
		{
			info.Actor.QueueFree();
			_player.NotifyNetworkCaptureDenied();
		}
	}

	public void HandleNetworkMonsterCaptureDenied(int netId)
	{
		if (IsNetworkClientWorld)
		{
			_player.NotifyNetworkCaptureDenied();
		}
	}

	// Called when the connection drops mid-game: frozen puppets would otherwise
	// linger as unkillable statues while the world falls back to local rules.
	public void ClearNetworkPuppetMonsters()
	{
		foreach (KeyValuePair<int, NetMonsterInfo> entry in _netMonstersById)
		{
			SimpleActor actor = entry.Value.Actor;
			if (IsInstanceValid(actor) && actor.IsNetworkPuppet)
			{
				actor.QueueFree();
			}
		}

		_netMonstersById.Clear();
		_netLastDamagePeerByNetId.Clear();
		_netCaptureReservations.Clear();
	}

	public void HandleNetworkMonsterRemoved(int netId, bool defeated)
	{
		if (!IsNetworkClientWorld || !_netMonstersById.TryGetValue(netId, out NetMonsterInfo info))
		{
			return;
		}

		_netMonstersById.Remove(netId);
		if (!IsInstanceValid(info.Actor))
		{
			return;
		}

		if (info.Actor.IsBoss)
		{
			foreach (KeyValuePair<string, SimpleActor> entry in _wildBossesByInstance)
			{
				if (entry.Value == info.Actor)
				{
					_wildBossesByInstance.Remove(entry.Key);
					break;
				}
			}
			UpdateActiveBossHud(false);
		}

		info.Actor.QueueFree();
	}
}
