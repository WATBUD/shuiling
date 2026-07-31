using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
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

	// Local player leaves their party (leader leaving disbands it).
	public void LeaveParty()
	{
		if (!IsOnline)
		{
			return;
		}

		if (IsHost)
		{
			HostProcessLeave(1);
		}
		else
		{
			RpcId(1, MethodName.ServerLeaveParty);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ServerLeaveParty()
	{
		if (Multiplayer.IsServer())
		{
			HostProcessLeave(Multiplayer.GetRemoteSenderId());
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

		// Only the party leader (or a solo player) may invite. A non-leader member
		// must leave their party first.
		if (_leaderOf.TryGetValue(inviter, out long inviterLeader) && inviterLeader != inviter)
		{
			NotifyPartyPeer(inviter, "system.party.not_leader");
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
		// Leader always first so clients can flag it.
		var members = new List<long> { leader };
		foreach (KeyValuePair<long, long> entry in _leaderOf)
		{
			if (entry.Value == leader && entry.Key != leader)
			{
				members.Add(entry.Key);
			}
		}

		// A party of one is not a party — disband it.
		if (members.Count <= 1)
		{
			_leaderOf.Remove(leader);
			SendEmptyPartyTo(leader);
			return;
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
				SetLocalPartyMirror(peers, names, leader);
			}
			else
			{
				RpcId(member, MethodName.ClientPartyMembers, peers, names, leader);
			}
		}
	}

	// Host: a member leaves; a leader leaving disbands the whole party.
	private void HostProcessLeave(long peer)
	{
		if (!_leaderOf.TryGetValue(peer, out long leader))
		{
			return;
		}

		if (peer == leader)
		{
			// Disband: clear everyone in this party and tell them.
			var members = new List<long> { leader };
			foreach (KeyValuePair<long, long> entry in _leaderOf)
			{
				if (entry.Value == leader && entry.Key != leader)
				{
					members.Add(entry.Key);
				}
			}

			foreach (long member in members)
			{
				_leaderOf.Remove(member);
				SendEmptyPartyTo(member);
				NotifyPartyPeer(member, "system.party.disbanded");
			}

			return;
		}

		_leaderOf.Remove(peer);
		SendEmptyPartyTo(peer);
		NotifyPartyPeer(peer, "system.party.left");
		BroadcastPartyForLeader(leader);
	}

	private void SendEmptyPartyTo(long peer)
	{
		if (peer == 1)
		{
			SetLocalPartyMirror(System.Array.Empty<long>(), System.Array.Empty<string>(), -1);
		}
		else
		{
			RpcId(peer, MethodName.ClientPartyMembers, System.Array.Empty<long>(), System.Array.Empty<string>(), -1L);
		}
	}

	private void NotifyPartyPeer(long peer, string messageKey)
	{
		if (peer == 1)
		{
			PostWorldMessage(LocaleText.T(messageKey), new Color(1.0f, 0.82f, 0.55f));
		}
		else
		{
			RpcId(peer, MethodName.ClientPartyNotice, messageKey);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientReceivePartyInvite(long inviterPeer, string inviterName)
	{
		PartyInviteReceived?.Invoke(inviterPeer, inviterName);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPartyMembers(long[] peers, string[] names, long leaderPeer)
	{
		SetLocalPartyMirror(peers, names, leaderPeer);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPartyInviteResult(string responderName, bool accepted)
	{
		DeliverInviteResultLocally(responderName, accepted);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void ClientPartyNotice(string messageKey)
	{
		PostWorldMessage(LocaleText.T(messageKey), new Color(1.0f, 0.82f, 0.55f));
	}

	private void SetLocalPartyMirror(long[] peers, string[] names, long leaderPeer)
	{
		_localPartyNames.Clear();
		if (names != null)
		{
			_localPartyNames.AddRange(names);
		}

		_localPartyPeers.Clear();
		if (peers != null)
		{
			_localPartyPeers.AddRange(peers);
		}

		LocalIsPartyLeader = leaderPeer == Multiplayer.GetUniqueId();
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
			SendEmptyPartyTo(orphan);
			NotifyPartyPeer(orphan, "system.party.disbanded");
		}

		// A remaining party shrinks by one — refresh it.
		if (wasMember && leaderOfPeer != peerId)
		{
			BroadcastPartyForLeader(leaderOfPeer);
		}
	}
}
