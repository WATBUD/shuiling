using Godot;
using System.Collections.Generic;

// Mailbox (信箱) — the received-gift screen. Lists letters newest-first with the
// sender, the timestamp, the written message and any attached items (claimed
// with a button). A compose button opens the send screen.
public partial class MailboxPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private Label _emptyLabel = null!;
	private Button _composeButton = null!;
	private VBoxContainer _listContainer = null!;

	public System.Action? CloseRequested { get; set; }
	public System.Action? ComposeRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshAll;
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		RefreshAll();
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
			// Seeing the mailbox clears the unread badge (letters stay in the list).
			_player?.MarkAllMailRead();
		}
	}

	private void BuildPanel()
	{
		Name = "MailboxPanel";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -390.0f;
		OffsetRight = 390.0f;
		OffsetTop = -300.0f;
		OffsetBottom = 300.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.96f),
			BorderColor = new Color(1.0f, 0.86f, 0.55f, 0.78f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(8);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		AddChild(margin);

		var root = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		root.AddThemeConstantOverride("separation", 8);
		margin.AddChild(root);

		_titleLabel = new Label { Text = LocaleText.T("mail.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 24);
		_titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.92f, 0.7f));
		root.AddChild(_titleLabel);

		_hintLabel = new Label { Text = LocaleText.T("mail.hint"), HorizontalAlignment = HorizontalAlignment.Center };
		_hintLabel.AddThemeFontSizeOverride("font_size", 13);
		_hintLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.82f, 0.92f));
		root.AddChild(_hintLabel);

		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(topRow);

		_composeButton = new Button { Text = LocaleText.T("mail.button.compose"), CustomMinimumSize = new Vector2(0.0f, 36.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_composeButton.Pressed += () => ComposeRequested?.Invoke();
		topRow.AddChild(_composeButton);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(720.0f, 420.0f),
		};
		root.AddChild(scroll);

		_listContainer = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_listContainer.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_listContainer);

		_emptyLabel = new Label { Text = LocaleText.T("mail.empty"), HorizontalAlignment = HorizontalAlignment.Center };
		_emptyLabel.AddThemeFontSizeOverride("font_size", 15);
		_emptyLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.70f, 0.80f));
		root.AddChild(_emptyLabel);

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	public void RefreshAll()
	{
		if (_listContainer == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("mail.title");
		_hintLabel.Text = LocaleText.T("mail.hint");
		_composeButton.Text = LocaleText.T("mail.button.compose");

		bool online = NetworkManager.Instance is { IsOnline: true };
		_composeButton.Disabled = !online;
		_composeButton.TooltipText = online ? string.Empty : LocaleText.T("system.mail.need_online");

		ClearChildren(_listContainer);
		if (_player == null)
		{
			return;
		}

		IReadOnlyList<MailMessageSaveData> mails = _player.Mailbox;
		_emptyLabel.Visible = mails.Count == 0;
		foreach (MailMessageSaveData mail in mails)
		{
			_listContainer.AddChild(BuildMailRow(mail));
		}
	}

	private Control BuildMailRow(MailMessageSaveData mail)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var rowStyle = new StyleBoxFlat
		{
			BgColor = mail.IsRead ? new Color(0.09f, 0.11f, 0.14f, 0.92f) : new Color(0.14f, 0.13f, 0.08f, 0.94f),
			BorderColor = mail.IsRead ? new Color(0.4f, 0.5f, 0.6f, 0.5f) : new Color(1.0f, 0.82f, 0.4f, 0.85f),
		};
		rowStyle.SetBorderWidthAll(mail.IsRead ? 1 : 2);
		rowStyle.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", rowStyle);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		content.AddThemeConstantOverride("separation", 5);
		margin.AddChild(content);

		var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		header.AddThemeConstantOverride("separation", 8);
		content.AddChild(header);

		var sender = new Label { Text = LocaleText.F("mail.from", mail.SenderName) };
		sender.AddThemeFontSizeOverride("font_size", 16);
		sender.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.66f));
		header.AddChild(sender);

		var time = new Label
		{
			Text = FormatTime(mail.SentUnix),
			HorizontalAlignment = HorizontalAlignment.Right,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		time.AddThemeFontSizeOverride("font_size", 12);
		time.AddThemeColorOverride("font_color", new Color(0.66f, 0.74f, 0.84f));
		header.AddChild(time);

		if (!string.IsNullOrWhiteSpace(mail.Body))
		{
			var body = new Label { Text = mail.Body, AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			body.AddThemeFontSizeOverride("font_size", 14);
			body.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 0.98f));
			content.AddChild(body);
		}

		if (mail.AttachedItems.Count > 0)
		{
			var attachRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
			attachRow.AddThemeConstantOverride("separation", 6);
			content.AddChild(attachRow);

			var attachLabel = new Label { Text = LocaleText.T("mail.attachments") };
			attachLabel.AddThemeFontSizeOverride("font_size", 13);
			attachLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 1.0f));
			attachRow.AddChild(attachLabel);

			foreach (KeyValuePair<string, int> entry in mail.AttachedItems)
			{
				var chip = new HBoxContainer();
				chip.AddThemeConstantOverride("separation", 2);
				chip.AddChild(ItemIconLibrary.CreateRect(entry.Key, 28.0f));
				var name = new Label { Text = entry.Value > 1 ? $"{GetItemName(entry.Key)} x{entry.Value}" : GetItemName(entry.Key) };
				name.AddThemeFontSizeOverride("font_size", 13);
				name.AddThemeColorOverride("font_color", mail.IsClaimed ? new Color(0.6f, 0.66f, 0.72f) : new Color(0.94f, 0.96f, 1.0f));
				chip.AddChild(name);
				attachRow.AddChild(chip);
			}
		}

		var actions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		actions.AddThemeConstantOverride("separation", 8);
		content.AddChild(actions);

		if (mail.AttachedItems.Count > 0)
		{
			var claim = new Button
			{
				Text = mail.IsClaimed ? LocaleText.T("mail.button.claimed") : LocaleText.T("mail.button.claim"),
				Disabled = mail.IsClaimed,
				CustomMinimumSize = new Vector2(0.0f, 32.0f),
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
			};
			claim.Pressed += () =>
			{
				if (_player != null && _player.TryClaimMail(mail.Id))
				{
					RefreshAll();
				}
			};
			actions.AddChild(claim);
		}

		var delete = new Button { Text = LocaleText.T("mail.button.delete"), CustomMinimumSize = new Vector2(0.0f, 32.0f) };
		delete.Pressed += () =>
		{
			_player?.DeleteMail(mail.Id);
			RefreshAll();
		};
		actions.AddChild(delete);

		return row;
	}

	private static string FormatTime(double sentUnix)
	{
		string stamp = Time.GetDatetimeStringFromUnixTime((long)sentUnix, true);
		return LocaleText.F("mail.sent_at", stamp);
	}

	private static string GetItemName(string itemId)
	{
		return MonsterLootCatalog.IsMonsterLoot(itemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(itemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(itemId));
	}

	private static void ClearChildren(Node parent)
	{
		foreach (Node child in parent.GetChildren())
		{
			parent.RemoveChild(child);
			child.QueueFree();
		}
	}
}
