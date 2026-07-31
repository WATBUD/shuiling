using Godot;
using System.Collections.Generic;

public partial class NetworkManager : Node
{
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
}
