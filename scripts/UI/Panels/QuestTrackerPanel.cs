using Godot;
using System.Collections.Generic;

// 常駐任務追蹤 HUD（楓之谷風格）：貼在小地圖下方的右上角，顯示目前已接受任務的精簡進度。
// 純 HUD 顯示元素，不攔截滑鼠點擊；由 QuestLogPanel 的核取方塊開關，狀態隨存檔持久化。
public partial class QuestTrackerPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private VBoxContainer _list = null!;
	private float _refreshUiRemaining;

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += RefreshAll;
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
		if (_list != null)
		{
			RefreshAll();
		}
	}

	public void SetShown(bool shown)
	{
		Visible = shown;
		if (shown)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_list == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("quest.tracker.title");
		ClearChildren(_list);

		if (_player == null)
		{
			return;
		}

		List<PlayerController.QuestLogEntry> entries = _player.GetAcceptedQuestEntries();
		if (entries.Count == 0)
		{
			var empty = MakeLabel(12, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("quest.tracker.empty");
			empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_list.AddChild(empty);
			return;
		}

		foreach (PlayerController.QuestLogEntry entry in entries)
		{
			AddQuestRow(entry);
		}
	}

	private void BuildPanel()
	{
		Name = "QuestTrackerPanel";
		MouseFilter = MouseFilterEnum.Ignore;
		AnchorLeft = 1.0f;
		AnchorRight = 1.0f;
		AnchorTop = 0.0f;
		AnchorBottom = 0.0f;
		OffsetLeft = -238.0f;
		OffsetRight = -18.0f;
		OffsetTop = 290.0f;
		OffsetBottom = 520.0f;
		CustomMinimumSize = new Vector2(220.0f, 0.0f);

		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.026f, 0.032f, 0.040f, 0.82f),
			BorderColor = new Color(0.54f, 0.66f, 0.78f, 0.62f),
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(6);
		AddThemeStyleboxOverride("panel", panelStyle);

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		margin.AddThemeConstantOverride("margin_left", 10);
		margin.AddThemeConstantOverride("margin_right", 10);
		margin.AddThemeConstantOverride("margin_top", 7);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		AddChild(margin);

		var rows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		rows.AddThemeConstantOverride("separation", 5);
		margin.AddChild(rows);

		_titleLabel = MakeLabel(13, new Color(1.0f, 0.92f, 0.66f));
		_titleLabel.Text = LocaleText.T("quest.tracker.title");
		rows.AddChild(_titleLabel);

		// Pass (not Ignore) so the wheel scrolls the list while hovering the tracker;
		// unused clicks still propagate through to gameplay. The inner list stays
		// Ignore so the scroll container beneath it is what receives the wheel.
		var scroll = new ScrollContainer
		{
			MouseFilter = MouseFilterEnum.Pass,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		rows.AddChild(scroll);

		_list = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_list.AddThemeConstantOverride("separation", 5);
		scroll.AddChild(_list);

		RefreshAll();
	}

	private void AddQuestRow(PlayerController.QuestLogEntry entry)
	{
		var info = new VBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		info.AddThemeConstantOverride("separation", 1);
		_list.AddChild(info);

		var nameLabel = MakeLabel(14, new Color(1.0f, 0.94f, 0.72f));
		nameLabel.Text = entry.NpcName;
		nameLabel.ClipText = true;
		nameLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		info.AddChild(nameLabel);

		bool trackItems = entry.StatusKey is "quest.status.gathering" or "quest.status.ready_deliver";
		var progressLabel = MakeLabel(12, new Color(0.82f, 0.90f, 0.96f));
		progressLabel.Text = trackItems
			? LocaleText.F("quest.log.items", entry.QuestItemName, entry.ItemCount, entry.ItemRequired)
			: LocaleText.F("quest.log.affinity", entry.Affinity, entry.AffinityRequired);
		info.AddChild(progressLabel);

		var statusLabel = MakeLabel(11, StatusColor(entry.StatusKey));
		statusLabel.Text = LocaleText.T(entry.StatusKey);
		info.AddChild(statusLabel);
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
		var label = new Label { MouseFilter = MouseFilterEnum.Ignore };
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
