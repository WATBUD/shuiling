using Godot;
using System.Collections.Generic;

// Gift-mail (寄送) — replaces direct player trading. A player can send another
// player a letter with a written message and attached items. Delivery is host-
// authoritative (see NetworkManager): online recipients get it immediately, and
// the mailbox itself is saved so an unread letter still shows the "!" badge on
// the next login. Attachments are only granted when the recipient claims them.
public partial class PlayerController
{
	private readonly List<MailMessageSaveData> _mailbox = new();

	public IReadOnlyList<MailMessageSaveData> Mailbox => _mailbox;

	public int UnreadMailCount
	{
		get
		{
			int count = 0;
			foreach (MailMessageSaveData mail in _mailbox)
			{
				if (!mail.IsRead)
				{
					count++;
				}
			}

			return count;
		}
	}

	// Incoming letter (local delivery target). Sorted newest-first.
	public void ReceiveMail(string senderName, double sentUnix, string body, string[] itemIds, int[] itemCounts)
	{
		var attached = new Dictionary<string, int>();
		if (itemIds != null && itemCounts != null)
		{
			int count = Mathf.Min(itemIds.Length, itemCounts.Length);
			for (int index = 0; index < count; index++)
			{
				if (!BuildCatalog.IsFreeItem(itemIds[index]) && itemCounts[index] > 0)
				{
					attached[itemIds[index]] = attached.TryGetValue(itemIds[index], out int existing)
						? existing + itemCounts[index]
						: itemCounts[index];
				}
			}
		}

		var mail = new MailMessageSaveData
		{
			Id = System.Guid.NewGuid().ToString("N"),
			SenderName = string.IsNullOrWhiteSpace(senderName) ? LocaleText.T("net.player.default_name") : senderName,
			SentUnix = sentUnix,
			Body = body ?? string.Empty,
			AttachedItems = attached,
			IsRead = false,
			IsClaimed = attached.Count == 0,
		};
		_mailbox.Insert(0, mail);

		PostSystemMessage(LocaleText.F("system.mail.received", mail.SenderName), new Color(1.0f, 0.86f, 0.5f));
		UpdateMailboxHud();
		if (_mailboxPanel != null && IsInstanceValid(_mailboxPanel) && _mailboxPanel.Visible)
		{
			_mailboxPanel.RefreshAll();
		}
	}

	// Outgoing letter. Returns false (with a hint message) when it cannot be sent.
	public bool SendMail(string recipientName, string body, IReadOnlyDictionary<string, int> attachments)
	{
		NetworkManager? net = NetworkManager.Instance;
		if (net == null || !net.IsOnline)
		{
			PostSystemMessage(LocaleText.T("system.mail.need_online"), new Color(1.0f, 0.6f, 0.45f));
			return false;
		}

		if (string.IsNullOrWhiteSpace(recipientName))
		{
			PostSystemMessage(LocaleText.T("system.mail.need_recipient"), new Color(1.0f, 0.6f, 0.45f));
			return false;
		}

		// Consume the attachments off the sender now; only what is actually pulled
		// from the bag rides along with the letter.
		var ids = new List<string>();
		var counts = new List<int>();
		if (attachments != null)
		{
			foreach (KeyValuePair<string, int> entry in attachments)
			{
				if (entry.Value <= 0 || BuildCatalog.IsFreeItem(entry.Key))
				{
					continue;
				}

				if (TryConsumeInventoryItem(entry.Key, entry.Value))
				{
					ids.Add(entry.Key);
					counts.Add(entry.Value);
				}
			}
		}

		net.SendMailToPlayer(recipientName, body ?? string.Empty, ids.ToArray(), counts.ToArray());
		PostSystemMessage(LocaleText.F("system.mail.sent", recipientName), new Color(0.7f, 1.0f, 0.78f));
		return true;
	}

	public bool TryClaimMail(string mailId)
	{
		MailMessageSaveData? mail = FindMail(mailId);
		if (mail == null || mail.IsClaimed || mail.AttachedItems.Count == 0)
		{
			return false;
		}

		foreach (KeyValuePair<string, int> entry in mail.AttachedItems)
		{
			if (!BuildCatalog.IsFreeItem(entry.Key) && entry.Value > 0)
			{
				AddInventoryItem(entry.Key, entry.Value);
			}
		}

		mail.IsClaimed = true;
		mail.IsRead = true;
		UpdateMailboxHud();
		return true;
	}

	public void DeleteMail(string mailId)
	{
		for (int index = 0; index < _mailbox.Count; index++)
		{
			if (_mailbox[index].Id == mailId)
			{
				_mailbox.RemoveAt(index);
				break;
			}
		}

		UpdateMailboxHud();
	}

	public void MarkAllMailRead()
	{
		foreach (MailMessageSaveData mail in _mailbox)
		{
			mail.IsRead = true;
		}

		UpdateMailboxHud();
	}

	private MailMessageSaveData? FindMail(string mailId)
	{
		foreach (MailMessageSaveData mail in _mailbox)
		{
			if (mail.Id == mailId)
			{
				return mail;
			}
		}

		return null;
	}

	// --- save round-trip ------------------------------------------------------

	private List<MailMessageSaveData> ExportMailbox()
	{
		var copy = new List<MailMessageSaveData>();
		foreach (MailMessageSaveData mail in _mailbox)
		{
			copy.Add(new MailMessageSaveData
			{
				Id = mail.Id,
				SenderName = mail.SenderName,
				SentUnix = mail.SentUnix,
				Body = mail.Body,
				AttachedItems = new Dictionary<string, int>(mail.AttachedItems),
				IsRead = mail.IsRead,
				IsClaimed = mail.IsClaimed,
			});
		}

		return copy;
	}

	private void RestoreMailbox(PlayerSaveData data)
	{
		_mailbox.Clear();
		if (data.Mailbox != null)
		{
			foreach (MailMessageSaveData mail in data.Mailbox)
			{
				if (string.IsNullOrEmpty(mail.Id))
				{
					mail.Id = System.Guid.NewGuid().ToString("N");
				}

				mail.AttachedItems ??= new Dictionary<string, int>();
				_mailbox.Add(mail);
			}
		}

		UpdateMailboxHud();
		if (_mailboxPanel != null && IsInstanceValid(_mailboxPanel))
		{
			_mailboxPanel.RefreshAll();
		}
	}
}
