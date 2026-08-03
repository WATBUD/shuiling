using Godot;

public partial class NetworkManager : Node
{
	// Host → clients: a monster is (un)protected during a capture attempt, so the
	// clients can show/hide the shield. HP no-death is already enforced host-side.
	public void BroadcastMonsterCaptureProtection(int netId, bool protectedState)
	{
		if (!IsHost)
		{
			return;
		}

		Rpc(MethodName.ClientMonsterCaptureProtection, netId, protectedState);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterCaptureProtection(int netId, bool protectedState)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.SetNetworkMonsterCaptureProtection(netId, protectedState);
		}
	}

	// Host → a specific client: a monster in that player's instance hit them.
	public void SendMonsterAttackToPlayer(long peerId, int damage)
	{
		if (!IsHost || peerId == 1)
		{
			return;
		}

		RpcId(peerId, MethodName.ClientReceiveMonsterAttack, damage);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveMonsterAttack(int damage)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld) && ActiveWorld.ActivePlayer is { } player && IsInstanceValid(player))
		{
			player.ReceiveDamage(Mathf.Clamp(damage, 1, 99999), null);
		}
	}

	// ---------------------------------------------------------------- monsters

	// Host → clients: a monster now exists (also used for join snapshots).
	public void BroadcastMonsterSpawn(int netId, string mapId, string nameKey, int level, int tier, int groupId, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (IsHost)
		{
			Rpc(MethodName.ClientMonsterSpawn, netId, mapId, nameKey, level, tier, groupId, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	public void SendMonsterSpawnTo(long peerId, int netId, string mapId, string nameKey, int level, int tier, int groupId, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (IsHost)
		{
			RpcId(peerId, MethodName.ClientMonsterSpawn, netId, mapId, nameKey, level, tier, groupId, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterSpawn(int netId, string mapId, string nameKey, int level, int tier, int groupId, int rarity,
		int maxHealth, int health, bool isBoss, string bossNameKey, float visualScale, Color auraColor, Vector3 position)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterSpawn(netId, mapId, nameKey, level, tier, groupId, rarity, maxHealth, health, isBoss, bossNameKey, visualScale, auraColor, position);
		}
	}

	public void BroadcastMonsterStates(int[] netIds, Vector3[] positions, float[] yaws, int[] healths, byte[] captureReady)
	{
		if (IsHost)
		{
			Rpc(MethodName.ClientMonsterStates, netIds, positions, yaws, healths, captureReady);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ClientMonsterStates(int[] netIds, Vector3[] positions, float[] yaws, int[] healths, byte[] captureReady)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterStates(netIds, positions, yaws, healths, captureReady);
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

	public void SendMonsterCaptureNetHitRequest(int netId)
	{
		if (IsClient && netId >= 0)
		{
			RpcId(1, MethodName.ServerReceiveMonsterCaptureNetHit, netId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveMonsterCaptureNetHit(int netId)
	{
		if (Multiplayer.IsServer() && ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.ApplyNetworkMonsterCaptureNetHit(netId, Multiplayer.GetRemoteSenderId());
		}
	}

	public void SendMonsterCaptureLockRequest(int netId, bool locked)
	{
		if (IsClient && netId >= 0)
		{
			RpcId(1, MethodName.ServerSetMonsterCaptureLock, netId, locked);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerSetMonsterCaptureLock(int netId, bool locked)
	{
		if (Multiplayer.IsServer() && ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.SetNetworkMonsterCaptureLock(netId, Multiplayer.GetRemoteSenderId(), locked);
		}
	}

	public void SendMonsterCaptureRequest(int netId)
	{
		if (IsClient && netId >= 0)
		{
			RpcId(1, MethodName.ServerReceiveMonsterCaptureRequest, netId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerReceiveMonsterCaptureRequest(int netId)
	{
		if (Multiplayer.IsServer() && ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.TryGrantNetworkMonsterCapture(netId, Multiplayer.GetRemoteSenderId());
		}
	}

	public void SendMonsterCaptureGranted(long peerId, int netId, string actorJson)
	{
		if (IsHost && peerId != 1)
		{
			RpcId(peerId, MethodName.ClientMonsterCaptureGranted, netId, actorJson);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterCaptureGranted(int netId, string actorJson)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterCaptureGranted(netId, actorJson);
		}
	}

	public void SendMonsterCaptureDenied(long peerId, int netId)
	{
		if (IsHost && peerId != 1)
		{
			RpcId(peerId, MethodName.ClientMonsterCaptureDenied, netId);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientMonsterCaptureDenied(int netId)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.HandleNetworkMonsterCaptureDenied(netId);
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

	// Host → owner client: a party-loot drop diced to this participant. The host's
	// own share (peer 1) spawns locally; everyone else receives it over RPC so only
	// the owner sees their loot.
	public void SendLootDropTo(long peerId, Vector3 position, string itemId, int amount, int goldAmount)
	{
		if (!IsHost)
		{
			return;
		}

		if (peerId == 1)
		{
			ActiveWorld?.SpawnLootDrop(position, itemId ?? string.Empty, amount, goldAmount);
			return;
		}

		RpcId(peerId, MethodName.ClientReceiveLootDrop, position, itemId ?? string.Empty, amount, goldAmount);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceiveLootDrop(Vector3 position, string itemId, int amount, int goldAmount)
	{
		if (ActiveWorld != null && IsInstanceValid(ActiveWorld))
		{
			ActiveWorld.SpawnLootDrop(position, itemId, amount, goldAmount);
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
}
