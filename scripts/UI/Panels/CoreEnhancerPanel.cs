using Godot;
using System.Collections.Generic;
using System.Linq;

// 核心強化師面板：兩個分頁。分頁 0 用屬性球＋金幣強化背包內未裝備的技能核心星數；
// 分頁 1 多選收藏中的寵物並分解成屬性球。資料與變動邏輯位於 PlayerController.CoreEnhance.cs。
public partial class CoreEnhancerPanel : PanelContainer
{
	private PlayerController? _player;
	private Label _titleLabel = null!;
	private HBoxContainer _tabBar = null!;
	private Button _tabEnhanceButton = null!;
	private Button _tabDismantleButton = null!;
	private VBoxContainer _list = null!;
	private VBoxContainer _yieldSummary = null!;
	private Button _dismantleButton = null!;
	private FloatingTooltip _tooltip = null!;
	private FloatingTooltip _costTooltip = null!;
	private ScrollContainer _scroll = null!;
	private GridContainer? _grid;
	private int _tab;
	private readonly HashSet<SimpleActor> _selectedPets = new();
	private const float EnhanceTileWidth = 98.0f;
	private const int EnhanceTileGap = 8;

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
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}
		if (_costTooltip != null && _costTooltip.Visible)
		{
			_costTooltip.PositionNearMouse(this);
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

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (!visible)
		{
			_tooltip?.HideTooltip();
			_costTooltip?.HideTooltip();
		}
		if (visible)
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

		_titleLabel.Text = LocaleText.T("core_enhancer.title");
		_tabEnhanceButton.Text = LocaleText.T("core_enhancer.tab_enhance");
		_tabDismantleButton.Text = LocaleText.T("core_enhancer.tab_dismantle");
		_tabEnhanceButton.ButtonPressed = _tab == 0;
		_tabDismantleButton.ButtonPressed = _tab == 1;
		_tooltip?.HideTooltip();
		_costTooltip?.HideTooltip();
		ClearChildren(_list);
		_grid = null;

		if (_player == null)
		{
			_dismantleButton.Visible = false;
			_yieldSummary.Visible = false;
			return;
		}

		if (_tab == 0)
		{
			_dismantleButton.Visible = false;
			_yieldSummary.Visible = false;
			RefreshEnhanceTab();
		}
		else
		{
			_yieldSummary.Visible = true;
			RefreshDismantleTab();
			RefreshYieldSummary();
		}
	}

	// Fills the "will get" section with the aggregated orbs from the current
	// selection (item name + quantity), so the player sees the dismantle result
	// below before confirming.
	private void RefreshYieldSummary()
	{
		if (_yieldSummary == null)
		{
			return;
		}

		ClearChildren(_yieldSummary);
		if (_player == null || _tab != 1)
		{
			return;
		}

		var title = MakeLabel(15, new Color(1.0f, 0.9f, 0.6f));
		Dictionary<string, int> yield = _player.GetDismantleYield(_selectedPets);
		if (yield.Count == 0)
		{
			title.Text = LocaleText.T("core_enhancer.dismantle_empty");
			_yieldSummary.AddChild(title);
			return;
		}

		title.Text = LocaleText.T("core_enhancer.dismantle_yield");
		_yieldSummary.AddChild(title);
		foreach (KeyValuePair<string, int> entry in yield)
		{
			var row = MakeLabel(14, new Color(0.85f, 0.93f, 1.0f));
			row.Text = $"{MonsterLootCatalog.GetCoreOrbDisplayName(entry.Key)} x{entry.Value}";
			_yieldSummary.AddChild(row);
		}
	}

	private void RefreshEnhanceTab()
	{
		if (_player == null)
		{
			return;
		}

		List<string> ids = _player.GetEnhanceableCoreIds();
		if (ids.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("core_enhancer.empty_cores");
			_list.AddChild(empty);
			return;
		}

		GridContainer grid = AddEnhanceGrid();
		foreach (string id in ids)
		{
			grid.AddChild(MakeEnhanceTile(id));
		}
	}

	private GridContainer AddEnhanceGrid()
	{
		var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", EnhanceTileGap);
		grid.AddThemeConstantOverride("v_separation", EnhanceTileGap);
		_list.AddChild(grid);
		_grid = grid;
		UpdateGridColumns();
		return grid;
	}

	private void UpdateGridColumns()
	{
		if (_grid == null || !IsInstanceValid(_grid) || _scroll == null)
		{
			return;
		}

		float available = Mathf.Max(_scroll.Size.X, EnhanceTileWidth);
		_grid.Columns = Mathf.Max(1, Mathf.FloorToInt((available + EnhanceTileGap) / (EnhanceTileWidth + EnhanceTileGap)));
	}

	private Control MakeEnhanceTile(string itemId)
	{
		PlayerController.CoreEnhanceQuote quote = _player!.GetCoreEnhanceQuote(itemId);
		string capturedId = itemId;
		var tile = new PanelContainer { CustomMinimumSize = new Vector2(EnhanceTileWidth, 132.0f) };
		tile.MouseEntered += () => ShowCoreInfo(capturedId);
		tile.MouseExited += () => _tooltip.HideTooltip();
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(6);
		tile.AddThemeStyleboxOverride("panel", style);

		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);
		tile.AddChild(box);

		TextureRect icon = ItemIconLibrary.CreateRect(itemId, 54.0f);
		icon.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.AddChild(icon);

		string coreName = LocaleText.T(BuildCatalog.GetItemNameKey(itemId)) + BuildCatalog.GetStarSuffix(itemId);
		var nameLabel = MakeLabel(13, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = coreName;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(nameLabel);

		var enhanceButton = new Button
		{
			Text = LocaleText.T("core_enhancer.enhance"),
			CustomMinimumSize = new Vector2(0.0f, 30.0f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Disabled = !_player.CanAffordCoreEnhance(quote),
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		enhanceButton.AddThemeFontSizeOverride("font_size", 13);
		enhanceButton.MouseEntered += () => _costTooltip.ShowTooltip(
			LocaleText.T("core_enhancer.enhance"), BuildEnhanceTip(capturedId), this);
		enhanceButton.MouseExited += () => _costTooltip.HideTooltip();
		enhanceButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.TryEnhanceCore(capturedId);
				RefreshAll();
			}
		};
		box.AddChild(enhanceButton);
		return tile;
	}

	private string BuildEnhanceTip(string itemId)
	{
		if (_player == null)
		{
			return string.Empty;
		}

		PlayerController.CoreEnhanceQuote quote = _player.GetCoreEnhanceQuote(itemId);
		if (quote.IsMax)
		{
			return LocaleText.T("core_enhancer.max_row");
		}

		return LocaleText.F(
			"core_enhancer.cost",
			MonsterLootCatalog.GetCoreOrbDisplayName(quote.OrbId),
			quote.OrbCount,
			quote.OrbHave,
			quote.Gold);
	}

	private void ShowCoreInfo(string itemId)
	{
		if (_player == null)
		{
			return;
		}

		string body = InventoryPanel.BuildItemTooltipBody(itemId, string.Empty);
		body += $"\n{LocaleText.F("shop.owned_count", _player.GetInventoryCount(itemId))}";
		_tooltip.ShowTooltip(InventoryPanel.BuildItemTooltipTitle(itemId), body, this);
	}

	private void RefreshDismantleTab()
	{
		if (_player == null)
		{
			return;
		}

		PruneInvalidSelection();

		int shown = 0;
		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			// Only undeployed / collection pets can be dismantled; a pet currently
			// fighting in the active party is excluded.
			if (!IsInstanceValid(actor) || !actor.IsCaptured || actor.IsDefeated || _player.IsInActiveParty(actor))
			{
				continue;
			}

			AddDismantleRow(actor);
			shown++;
		}

		if (shown == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("core_enhancer.empty_pets");
			_list.AddChild(empty);
		}

		_dismantleButton.Visible = true;
		_dismantleButton.Text = LocaleText.T("core_enhancer.dismantle");
		_dismantleButton.Disabled = _selectedPets.Count == 0;
	}

	private static StyleBoxFlat MakeRowStyle(Color background, Color border, int borderWidth)
	{
		var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(8);
		return style;
	}

	private void AddDismantleRow(SimpleActor actor)
	{
		SimpleActor capturedActor = actor;
		bool selectedInitially = _selectedPets.Contains(actor);

		// The whole row is a toggle button: clicking anywhere selects/deselects.
		// Godot swaps to the "pressed" (green) stylebox while selected.
		var row = new Button
		{
			ToggleMode = true,
			ButtonPressed = selectedInitially,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 54.0f),
			FocusMode = FocusModeEnum.None,
		};
		var idle = MakeRowStyle(new Color(0.08f, 0.09f, 0.105f, 0.94f), new Color(0.32f, 0.38f, 0.45f, 0.72f), 1);
		var selected = MakeRowStyle(new Color(0.10f, 0.17f, 0.12f, 0.96f), new Color(0.52f, 0.96f, 0.62f, 1.0f), 2);
		var hover = MakeRowStyle(new Color(0.11f, 0.13f, 0.16f, 0.96f), new Color(0.5f, 0.6f, 0.7f, 0.9f), 1);
		row.AddThemeStyleboxOverride("normal", idle);
		row.AddThemeStyleboxOverride("hover", hover);
		row.AddThemeStyleboxOverride("pressed", selected);
		row.AddThemeStyleboxOverride("hover_pressed", selected);
		row.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		_list.AddChild(row);

		// Content overlay: ignores the mouse so clicks fall through to the button.
		var content = new HBoxContainer
		{
			MouseFilter = MouseFilterEnum.Ignore,
		};
		content.SetAnchorsPreset(LayoutPreset.FullRect);
		content.AddThemeConstantOverride("separation", 12);
		content.OffsetLeft = 12;
		content.OffsetRight = -12;
		row.AddChild(content);

		var check = MakeLabel(18, new Color(0.62f, 1.0f, 0.72f));
		check.MouseFilter = MouseFilterEnum.Ignore;
		check.Text = selectedInitially ? "☑" : "☐";
		content.AddChild(check);

		var info = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
		content.AddChild(info);

		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameLabel.Text = $"{actor.LocalizedDisplayName}  Lv.{actor.Level}";
		info.AddChild(nameLabel);

		string element = BuildCatalog.GetIdentity(actor).ElementAffinityId;
		int tier = CoreEnhanceConfig.TierForLevel(actor.Level);
		string orbName = MonsterLootCatalog.GetCoreOrbDisplayName(MonsterLootCatalog.GetCoreOrbId(element, tier));

		var yieldLabel = MakeLabel(14, new Color(0.80f, 0.88f, 0.94f));
		yieldLabel.MouseFilter = MouseFilterEnum.Ignore;
		yieldLabel.Text = $"{orbName} x{PlayerController.DismantleOrbCount(actor)}";
		info.AddChild(yieldLabel);

		row.Toggled += pressed =>
		{
			if (pressed)
			{
				_selectedPets.Add(capturedActor);
			}
			else
			{
				_selectedPets.Remove(capturedActor);
			}
			check.Text = pressed ? "☑" : "☐";
			_dismantleButton.Disabled = _selectedPets.Count == 0;
			RefreshYieldSummary();
		};
	}

	private void PruneInvalidSelection()
	{
		var stale = new List<SimpleActor>();
		foreach (SimpleActor actor in _selectedPets)
		{
			if (!IsInstanceValid(actor) || !actor.IsCaptured || actor.IsDefeated
				|| _player == null || !_player.CapturedCollection.Contains(actor)
				|| _player.IsInActiveParty(actor))
			{
				stale.Add(actor);
			}
		}

		foreach (SimpleActor actor in stale)
		{
			_selectedPets.Remove(actor);
		}
	}

	private void BuildPanel()
	{
		Name = "CoreEnhancerPanel";
		MouseFilter = MouseFilterEnum.Stop;
		AnchorLeft = 0.12f;
		AnchorTop = 0.10f;
		AnchorRight = 0.88f;
		AnchorBottom = 0.90f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.045f, 0.052f, 0.064f, 0.97f),
			BorderColor = new Color(0.58f, 0.72f, 0.95f, 0.96f),
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

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.AddChild(_titleLabel);

		_tabBar = new HBoxContainer();
		_tabBar.AddThemeConstantOverride("separation", 8);
		root.AddChild(_tabBar);

		_tabEnhanceButton = CreateTabButton("core_enhancer.tab_enhance");
		_tabEnhanceButton.Pressed += () =>
		{
			_tab = 0;
			RefreshAll();
		};
		_tabBar.AddChild(_tabEnhanceButton);

		_tabDismantleButton = CreateTabButton("core_enhancer.tab_dismantle");
		_tabDismantleButton.Pressed += () =>
		{
			_tab = 1;
			RefreshAll();
		};
		_tabBar.AddChild(_tabDismantleButton);

		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_scroll.Resized += UpdateGridColumns;
		root.AddChild(_scroll);

		_list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_list.AddThemeConstantOverride("separation", 8);
		_scroll.AddChild(_list);

		// Live "will get" preview of the aggregated orbs from the current selection
		// (dismantle tab only).
		_yieldSummary = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_yieldSummary.AddThemeConstantOverride("separation", 2);
		root.AddChild(_yieldSummary);

		_dismantleButton = new Button
		{
			Text = LocaleText.T("core_enhancer.dismantle"),
			CustomMinimumSize = new Vector2(0.0f, 46.0f),
			Visible = false,
		};
		_dismantleButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.DismantleCompanions(new List<SimpleActor>(_selectedPets));
				_selectedPets.Clear();
				RefreshAll();
			}
		};
		root.AddChild(_dismantleButton);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);

		_tooltip = new FloatingTooltip
		{
			Name = "CoreEnhancerTooltip",
			MaxWidth = 460.0f,
			MinWidth = 240.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.58f,
			MinBodyHeight = 64.0f,
			ZIndex = 100,
		};
		AddChild(_tooltip);

		_costTooltip = new FloatingTooltip
		{
			Name = "CoreEnhancerCostTooltip",
			MaxWidth = 460.0f,
			MinWidth = 240.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.58f,
			MinBodyHeight = 48.0f,
			ZIndex = 101,
		};
		AddChild(_costTooltip);
	}

	private static Button CreateTabButton(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			ToggleMode = true,
			CustomMinimumSize = new Vector2(120.0f, 32.0f),
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		ApplyTagButtonStyle(button);
		return button;
	}

	private static void ApplyTagButtonStyle(Button button)
	{
		button.AddThemeFontSizeOverride("font_size", 14);
		button.AddThemeStyleboxOverride("normal", MakeTagStyle(new Color(0.075f, 0.086f, 0.105f, 0.92f), new Color(0.24f, 0.30f, 0.38f, 0.78f)));
		button.AddThemeStyleboxOverride("hover", MakeTagStyle(new Color(0.11f, 0.13f, 0.16f, 0.96f), new Color(0.42f, 0.55f, 0.66f, 0.90f)));
		button.AddThemeStyleboxOverride("pressed", MakeTagStyle(new Color(0.18f, 0.27f, 0.33f, 0.98f), new Color(0.70f, 0.90f, 1.0f, 0.98f)));
		button.AddThemeStyleboxOverride("hover_pressed", MakeTagStyle(new Color(0.20f, 0.31f, 0.38f, 1.0f), new Color(0.78f, 0.94f, 1.0f, 1.0f)));
		button.AddThemeColorOverride("font_color", new Color(0.78f, 0.86f, 0.92f));
		button.AddThemeColorOverride("font_pressed_color", new Color(1.0f, 0.96f, 0.78f));
		button.AddThemeColorOverride("font_hover_color", new Color(0.92f, 0.98f, 1.0f));
	}

	private static StyleBoxFlat MakeTagStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = 10.0f;
		style.ContentMarginRight = 10.0f;
		style.ContentMarginTop = 4.0f;
		style.ContentMarginBottom = 4.0f;
		return style;
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
