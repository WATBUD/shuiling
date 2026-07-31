using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
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
		var healths = new List<float>();
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
			healths.Add(actor.HealthRatio);
		}

		Rpc(MethodName.ClientCompanionStates, ids.ToArray(), positions.ToArray(), yaws.ToArray(), healths.ToArray());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	private void ClientCompanionStates(int[] slots, Vector3[] positions, float[] yaws, float[] healths)
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
				if (i < healths.Length)
				{
					puppet.SetHealth(healths[i]);
				}
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

	// Party changed locally — push a fresh roster to peers on the next tick.
	public void MarkCompanionRosterDirty()
	{
		_companionRosterDirty = true;
	}
}
