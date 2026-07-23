using Godot;
using System.Collections.Generic;

// Compose (撰寫) — the send-gift screen. Pick an online recipient, write a
// message, and attach items from the bag. Items are not removed until the letter
// is actually sent (cancelling leaves the bag untouched).
public partial class ComposePanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _recipientLabel = null!;
	private OptionButton _recipientOption = null!;
	private Label _noPlayersLabel = null!;
	private TextEdit _bodyEdit = null!;
	private Label _attachLabel = null!;
	private GridContainer _inventoryGrid = null!;
	private HBoxContainer _attachedRow = null!;
	private Button _sendButton = null!;

	private readonly Dictionary<string, int> _attachments = new();

	public System.Action? CloseRequested { get; set; }

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
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			_attachments.Clear();
			_bodyEdit.Text = string.Empty;
			RefreshAll();
		}
	}

	private void BuildPanel()
	{
		Name = "ComposePanel";
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -340.0f;
		OffsetRight = 340.0f;
		OffsetTop = -300.0f;
		OffsetBottom = 300.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.97f),
			BorderColor = new Color(0.62f, 0.86f, 1.0f, 0.78f),
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

		_titleLabel = new Label { Text = LocaleText.T("mail.compose.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 22);
		_titleLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.94f, 1.0f));
		root.AddChild(_titleLabel);

		var recipientRow = new HBoxContainer();
		recipientRow.AddThemeConstantOverride("separation", 8);
		root.AddChild(recipientRow);

		_recipientLabel = new Label { Text = LocaleText.T("mail.compose.recipient") };
		_recipientLabel.AddThemeFontSizeOverride("font_size", 15);
		_recipientLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.92f, 0.98f));
		recipientRow.AddChild(_recipientLabel);

		_recipientOption = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		recipientRow.AddChild(_recipientOption);

		_noPlayersLabel = new Label { Text = LocaleText.T("mail.compose.no_players"), HorizontalAlignment = HorizontalAlignment.Center };
		_noPlayersLabel.AddThemeFontSizeOverride("font_size", 13);
		_noPlayersLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.72f, 0.55f));
		root.AddChild(_noPlayersLabel);

		var bodyLabel = new Label { Text = LocaleText.T("mail.compose.message") };
		bodyLabel.AddThemeFontSizeOverride("font_size", 15);
		bodyLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.92f, 0.98f));
		root.AddChild(bodyLabel);

		_bodyEdit = new TextEdit
		{
			PlaceholderText = LocaleText.T("mail.compose.message_hint"),
			CustomMinimumSize = new Vector2(0.0f, 110.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_bodyEdit.WrapMode = TextEdit.LineWrappingMode.Boundary;
		root.AddChild(_bodyEdit);

		_attachLabel = new Label { Text = LocaleText.T("mail.compose.attached") };
		_attachLabel.AddThemeFontSizeOverride("font_size", 14);
		_attachLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.9f, 0.66f));
		root.AddChild(_attachLabel);

		var attachedScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0.0f, 46.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		root.AddChild(attachedScroll);
		_attachedRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_attachedRow.AddThemeConstantOverride("separation", 6);
		attachedScroll.AddChild(_attachedRow);

		var bagLabel = new Label { Text = LocaleText.T("mail.compose.bag") };
		bagLabel.AddThemeFontSizeOverride("font_size", 14);
		bagLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.86f, 1.0f));
		root.AddChild(bagLabel);

		var bagScroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(640.0f, 200.0f),
		};
		root.AddChild(bagScroll);
		_inventoryGrid = new GridContainer { Columns = 8, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_inventoryGrid.AddThemeConstantOverride("h_separation", 4);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 4);
		bagScroll.AddChild(_inventoryGrid);

		var buttons = new HBoxContainer();
		buttons.AddThemeConstantOverride("separation", 8);
		root.AddChild(buttons);

		_sendButton = new Button { Text = LocaleText.T("mail.button.send"), CustomMinimumSize = new Vector2(0.0f, 42.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_sendButton.Pressed += OnSendPressed;
		buttons.AddChild(_sendButton);

		var cancel = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 42.0f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
		cancel.Pressed += () => CloseRequested?.Invoke();
		buttons.AddChild(cancel);
	}

	public void RefreshAll()
	{
		if (_recipientOption == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("mail.compose.title");
		_recipientLabel.Text = LocaleText.T("mail.compose.recipient");
		_attachLabel.Text = LocaleText.T("mail.compose.attached");
		_sendButton.Text = LocaleText.T("mail.button.send");

		List<string> recipients = NetworkManager.Instance?.GetOtherPlayerNames() ?? new List<string>();
		_recipientOption.Clear();
		foreach (string name in recipients)
		{
			_recipientOption.AddItem(name);
		}

		bool hasRecipients = recipients.Count > 0;
		_recipientOption.Visible = hasRecipients;
		_noPlayersLabel.Visible = !hasRecipients;
		_sendButton.Disabled = !hasRecipients;

		RebuildAttached();
		RebuildInventory();
	}

	private void RebuildInventory()
	{
		ClearChildren(_inventoryGrid);
		if (_player == null)
		{
			return;
		}

		var ids = new List<string>();
		foreach (KeyValuePair<string, int> entry in _player.InventoryItems)
		{
			if (entry.Value > 0 && !BuildCatalog.IsFreeItem(entry.Key))
			{
				ids.Add(entry.Key);
			}
		}

		ids.Sort((a, b) => string.Compare(GetItemName(a), GetItemName(b), System.StringComparison.CurrentCulture));
		foreach (string itemId in ids)
		{
			int available = _player.GetInventoryCount(itemId) - (_attachments.TryGetValue(itemId, out int attached) ? attached : 0);
			var button = new Button
			{
				Text = available > 0 ? $"x{available}" : "x0",
				CustomMinimumSize = new Vector2(64.0f, 66.0f),
				ClipText = true,
				Disabled = available <= 0,
				TooltipText = LocaleText.F("mail.compose.attach_tooltip", GetItemName(itemId)),
			};
			button.AddThemeFontSizeOverride("font_size", 12);
			ItemIconLibrary.Apply(button, itemId, 42);
			string captured = itemId;
			button.Pressed += () => Attach(captured);
			_inventoryGrid.AddChild(button);
		}
	}

	private void RebuildAttached()
	{
		ClearChildren(_attachedRow);
		if (_attachments.Count == 0)
		{
			var none = new Label { Text = LocaleText.T("mail.compose.none") };
			none.AddThemeFontSizeOverride("font_size", 13);
			none.AddThemeColorOverride("font_color", new Color(0.6f, 0.68f, 0.76f));
			_attachedRow.AddChild(none);
			return;
		}

		foreach (KeyValuePair<string, int> entry in _attachments)
		{
			var chip = new Button
			{
				Text = $"x{entry.Value}",
				CustomMinimumSize = new Vector2(58.0f, 40.0f),
				ClipText = true,
				TooltipText = LocaleText.F("mail.compose.remove_tooltip", GetItemName(entry.Key)),
			};
			chip.AddThemeFontSizeOverride("font_size", 12);
			ItemIconLibrary.Apply(chip, entry.Key, 28);
			string captured = entry.Key;
			chip.Pressed += () => Detach(captured);
			_attachedRow.AddChild(chip);
		}
	}

	private void Attach(string itemId)
	{
		if (_player == null)
		{
			return;
		}

		int attached = _attachments.TryGetValue(itemId, out int current) ? current : 0;
		if (_player.GetInventoryCount(itemId) - attached <= 0)
		{
			return;
		}

		_attachments[itemId] = attached + 1;
		RebuildAttached();
		RebuildInventory();
	}

	private void Detach(string itemId)
	{
		if (!_attachments.TryGetValue(itemId, out int current))
		{
			return;
		}

		if (current <= 1)
		{
			_attachments.Remove(itemId);
		}
		else
		{
			_attachments[itemId] = current - 1;
		}

		RebuildAttached();
		RebuildInventory();
	}

	private void OnSendPressed()
	{
		if (_player == null || _recipientOption.ItemCount == 0)
		{
			return;
		}

		int selected = _recipientOption.Selected;
		if (selected < 0)
		{
			selected = 0;
		}

		string recipient = _recipientOption.GetItemText(selected);
		if (_player.SendMail(recipient, _bodyEdit.Text, _attachments))
		{
			_attachments.Clear();
			_bodyEdit.Text = string.Empty;
			CloseRequested?.Invoke();
		}
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
