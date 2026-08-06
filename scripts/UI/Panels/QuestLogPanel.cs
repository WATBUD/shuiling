using Godot;
using System.Collections.Generic;

// 任務清單面板（按 Q 開啟）：顯示目前已接受的招募任務與進度（收集道具數、親密度、狀態）。
public partial class QuestLogPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _questList = null!;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private CheckButton _trackerToggle = null!;
	private float _refreshUiRemaining;

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

	public override void _Process(double delta)
	{
		if (!Visible)
		{
			return;
		}

		_refreshUiRemaining -= (float)delta;
		if (_refreshUiRemaining <= 0.0f)
		{
			_refreshUiRemaining = 0.5f;
			RefreshAll();
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_questList != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_questList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("quest.log.title");
		_hintLabel.Text = LocaleText.T("quest.log.hint");
		if (_trackerToggle != null)
		{
			_trackerToggle.Text = LocaleText.T("quest.log.show_tracker");
			_trackerToggle.SetPressedNoSignal(_player?.ShowQuestTracker ?? false);
		}
		ClearChildren(_questList);

		if (_player == null)
		{
			return;
		}

		List<PlayerController.QuestLogEntry> entries = _player.GetAcceptedQuestEntries();
		if (entries.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("quest.log.empty");
			_questList.AddChild(empty);
			return;
		}

		foreach (PlayerController.QuestLogEntry entry in entries)
		{
			AddQuestRow(entry);
		}
	}

	private void BuildPanel()
	{
		Name = "QuestLogPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.16f;
		AnchorTop = 0.12f;
		AnchorRight = 0.84f;
		AnchorBottom = 0.88f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.86f, 0.78f, 0.46f, 0.96f),
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", style);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		margin.AddThemeConstantOverride("margin_right", 18);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		AddChild(margin);

		var root = new VBoxContainer();
		root.AddThemeConstantOverride("separation", 12);
		margin.AddChild(root);

		_titleLabel = MakeLabel(24, new Color(1.0f, 0.92f, 0.66f));
		root.AddChild(_titleLabel);

		_hintLabel = MakeLabel(15, new Color(0.72f, 0.82f, 0.88f));
		_hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(_hintLabel);

		_trackerToggle = new CheckButton { Text = LocaleText.T("quest.log.show_tracker") };
		_trackerToggle.Toggled += enabled => _player?.SetShowQuestTracker(enabled);
		root.AddChild(_trackerToggle);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(scroll);

		_questList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_questList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_questList);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private void AddQuestRow(PlayerController.QuestLogEntry entry)
	{
		var row = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		_questList.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		info.AddThemeConstantOverride("separation", 3);
		margin.AddChild(info);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		info.AddChild(header);

		var nameLabel = MakeLabel(18, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = LocaleText.F("quest.log.recruit", entry.NpcName);
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(nameLabel);

		var statusLabel = MakeLabel(15, StatusColor(entry.StatusKey));
		statusLabel.Text = LocaleText.T(entry.StatusKey);
		statusLabel.HorizontalAlignment = HorizontalAlignment.Right;
		header.AddChild(statusLabel);

		var itemLabel = MakeLabel(14, new Color(0.82f, 0.90f, 0.96f));
		itemLabel.Text = LocaleText.F("quest.log.items", entry.QuestItemName, entry.ItemCount, entry.ItemRequired);
		info.AddChild(itemLabel);

	}

	private static Color StatusColor(string statusKey)
	{
		return statusKey switch
		{
			"quest.status.ready_deliver" => new Color(0.62f, 1.0f, 0.68f),
			"quest.status.ready_invite" => new Color(1.0f, 0.90f, 0.42f),
			"quest.status.need_affinity" => new Color(1.0f, 0.72f, 0.58f),
			_ => new Color(0.78f, 0.84f, 0.90f),
		};
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label();
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
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
