using Godot;
using System.Collections.Generic;

// 精煉 NPC 面板：三個分頁。分頁 0 精煉背包裝備星等；分頁 1 分解星等裝備換回強化水晶；
// 分頁 2 於相鄰階級間交換強化水晶（10:1 合成 / 1:10 拆解）。
public partial class RefinementPanel : PanelContainer
{
	private PlayerController? _player;
	private VBoxContainer _itemList = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _hintLabel = null!;
	private HBoxContainer _tabBar = null!;
	private Button _tabRefineButton = null!;
	private Button _tabDismantleButton = null!;
	private Button _tabExchangeButton = null!;
	private FloatingTooltip _itemInfo = null!;
	private ScrollContainer _scroll = null!;
	private GridContainer? _grid;
	private PanelContainer _detailPanel = null!;
	private Label _detailLabel = null!;
	private Button _confirmButton = null!;
	private string _selectedRefineId = string.Empty;
	private string _selectedDismantleId = string.Empty;
	private int _tab;
	private const float RefineTileWidth = 98.0f;
	private const int RefineTileGap = 8;

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
		if (_itemInfo != null && _itemInfo.Visible)
		{
			_itemInfo.PositionNearMouse(this);
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (_itemInfo == null || !_itemInfo.Visible
			|| inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_itemInfo.ScrollDetail(-48);
			GetViewport().SetInputAsHandled();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_itemInfo.ScrollDetail(48);
			GetViewport().SetInputAsHandled();
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_itemList != null)
		{
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (!visible)
		{
			_itemInfo?.HideTooltip();
		}
		if (visible)
		{
			RefreshAll();
		}
	}

	public void RefreshAll()
	{
		if (_itemList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("refine.title");
		_tabRefineButton.Text = LocaleText.T("refine.tab_refine");
		_tabDismantleButton.Text = LocaleText.T("refine.tab_dismantle");
		_tabExchangeButton.Text = LocaleText.T("refine.tab_exchange");
		_tabRefineButton.ButtonPressed = _tab == 0;
		_tabDismantleButton.ButtonPressed = _tab == 1;
		_tabExchangeButton.ButtonPressed = _tab == 2;
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		_hintLabel.Text = LocaleText.T(_tab switch
		{
			1 => "refine.hint",
			2 => "refine.exchange.hint",
			_ => "refine.hint",
		});
		_itemInfo?.HideTooltip();
		ClearChildren(_itemList);
		_grid = null;
		_detailPanel.Visible = false;

		if (_player == null)
		{
			return;
		}

		switch (_tab)
		{
			case 1:
				RefreshDismantleTab();
				break;
			case 2:
				RefreshExchangeTab();
				break;
			default:
				RefreshRefineTab();
				break;
		}
	}

	private void RefreshRefineTab()
	{
		if (_player == null)
		{
			return;
		}

		List<string> ids = _player.GetRefinableBagEquipmentIds();
		if (!ids.Contains(_selectedRefineId))
		{
			_selectedRefineId = string.Empty;
		}

		if (ids.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("refine.empty");
			_itemList.AddChild(empty);
			_detailPanel.Visible = false;
			return;
		}

		GridContainer grid = AddItemGrid();
		foreach (string id in ids)
		{
			string capturedId = id;
			grid.AddChild(MakeItemTile(id, _selectedRefineId == id, () =>
			{
				_selectedRefineId = capturedId;
				RefreshAll();
			}));
		}

		UpdateRefineDetail();
	}

	private void UpdateRefineDetail()
	{
		if (_player == null)
		{
			return;
		}

		_detailPanel.Visible = true;
		_confirmButton.Text = LocaleText.T("refine.button");
		if (string.IsNullOrEmpty(_selectedRefineId))
		{
			_detailLabel.Text = LocaleText.T("refine.select_hint");
			_confirmButton.Disabled = true;
			return;
		}

		PlayerController.RefinementQuote quote = _player.GetRefinementQuote(_selectedRefineId);
		int owned = _player.GetInventoryCount(_selectedRefineId);
		string baseName = LocaleText.T(BuildCatalog.GetItemNameKey(_selectedRefineId)) + BuildCatalog.GetStarSuffix(_selectedRefineId);
		if (quote.CanRefine)
		{
			int ownedCrystals = _player.GetInventoryCount(quote.CrystalId);
			string crystalName = LocaleText.T(MonsterLootCatalog.GetNameKey(quote.CrystalId));
			_detailLabel.Text = $"{baseName}\n" + LocaleText.F(
				"refine.row.detail",
				quote.CurrentStars,
				quote.TargetStars,
				quote.SuccessPercent,
				quote.Gold,
				crystalName,
				quote.CrystalCount,
				ownedCrystals);
			_confirmButton.Disabled = owned <= 0;
		}
		else
		{
			_detailLabel.Text = $"{baseName}\n" + LocaleText.T("refine.row.max");
			_confirmButton.Disabled = true;
		}
	}

	private void RefreshDismantleTab()
	{
		if (_player == null)
		{
			return;
		}

		List<string> ids = _player.GetDismantlableEquipmentIds();
		if (!ids.Contains(_selectedDismantleId))
		{
			_selectedDismantleId = string.Empty;
		}

		if (ids.Count == 0)
		{
			var empty = MakeLabel(16, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("refine.dismantle.empty");
			_itemList.AddChild(empty);
			_detailPanel.Visible = false;
			return;
		}

		GridContainer grid = AddItemGrid();
		foreach (string id in ids)
		{
			string capturedId = id;
			grid.AddChild(MakeItemTile(id, _selectedDismantleId == id, () =>
			{
				_selectedDismantleId = capturedId;
				RefreshAll();
			}));
		}

		UpdateDismantleDetail();
	}

	private void UpdateDismantleDetail()
	{
		if (_player == null)
		{
			return;
		}

		_detailPanel.Visible = true;
		_confirmButton.Text = LocaleText.T("refine.dismantle.button");
		if (string.IsNullOrEmpty(_selectedDismantleId))
		{
			_detailLabel.Text = LocaleText.T("refine.dismantle.select_hint");
			_confirmButton.Disabled = true;
			return;
		}

		int owned = _player.GetInventoryCount(_selectedDismantleId);
		int stars = BuildCatalog.GetEquipmentStars(_selectedDismantleId);
		string baseName = LocaleText.T(BuildCatalog.GetItemNameKey(_selectedDismantleId)) + BuildCatalog.GetStarSuffix(_selectedDismantleId);
		string crystalName = LocaleText.T(MonsterLootCatalog.GetNameKey(MonsterLootCatalog.GetEnhanceCrystalId(stars)));
		_detailLabel.Text = $"{baseName}\n" + LocaleText.F("refine.dismantle.yield", _player.GetEquipmentDismantleYield(_selectedDismantleId), crystalName);
		_confirmButton.Disabled = owned <= 0;
	}

	private void OnConfirmPressed()
	{
		if (_player == null)
		{
			return;
		}

		if (_tab == 0 && !string.IsNullOrEmpty(_selectedRefineId))
		{
			// Follow the selection onto the refined result (star +1 on success), so
			// the player can keep refining the same piece without reselecting it.
			string refiningId = _selectedRefineId;
			PlayerController.RefinementQuote quote = _player.GetRefinementQuote(refiningId);
			bool success = _player.TryRefineBagEquipment(refiningId);
			_selectedRefineId = success
				? BuildCatalog.MakeRefinedEquipmentId(quote.BaseId, quote.TargetStars)
				: refiningId;
			RefreshAll();
		}
		else if (_tab == 1 && !string.IsNullOrEmpty(_selectedDismantleId))
		{
			_player.TryDismantleEquipment(_selectedDismantleId);
			RefreshAll();
		}
	}

	// A responsive icon grid inside the scroll, matching the gacha panel's layout.
	private GridContainer AddItemGrid()
	{
		var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", RefineTileGap);
		grid.AddThemeConstantOverride("v_separation", RefineTileGap);
		_itemList.AddChild(grid);
		_grid = grid;
		UpdateGridColumns();
		return grid;
	}

	// Fit as many tile columns as the scroll allows. On first open the scroll has
	// no width yet (→ 1 column); the scroll's Resized signal re-runs this once the
	// layout settles so the grid fills out.
	private void UpdateGridColumns()
	{
		if (_grid == null || !IsInstanceValid(_grid) || _scroll == null)
		{
			return;
		}

		float available = Mathf.Max(_scroll.Size.X, RefineTileWidth);
		_grid.Columns = Mathf.Max(1, Mathf.FloorToInt((available + RefineTileGap) / (RefineTileWidth + RefineTileGap)));
	}

	// Icon thumbnail tile: click selects (drives the detail panel), hover shows the
	// full stats tooltip. Selected tile gets a gold border.
	private Control MakeItemTile(string itemId, bool selected, System.Action onClick)
	{
		int owned = _player?.GetInventoryCount(itemId) ?? 0;
		var tile = new PanelContainer { CustomMinimumSize = new Vector2(RefineTileWidth, 118.0f) };
		string capturedId = itemId;
		tile.MouseEntered += () => ShowItemInfo(capturedId);
		tile.MouseExited += HideItemInfo;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.09f, 0.105f, 0.94f),
			BorderColor = selected ? new Color(1.0f, 0.86f, 0.4f, 0.98f) : new Color(0.32f, 0.38f, 0.45f, 0.72f),
		};
		style.SetBorderWidthAll(selected ? 3 : 1);
		style.SetCornerRadiusAll(6);
		style.SetContentMarginAll(6);
		tile.AddThemeStyleboxOverride("panel", style);
		tile.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				tile.AcceptEvent();
				onClick();
			}
		};

		var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);
		tile.AddChild(box);

		TextureRect icon = ItemIconLibrary.CreateRect(itemId, 58.0f);
		icon.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		box.AddChild(icon);

		var nameLabel = MakeLabel(13, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = $"{LocaleText.T(BuildCatalog.GetItemNameKey(itemId))}{BuildCatalog.GetStarSuffix(itemId)}";
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(nameLabel);

		var countLabel = MakeLabel(12, new Color(0.78f, 0.86f, 0.94f));
		countLabel.Text = $"×{owned}";
		countLabel.HorizontalAlignment = HorizontalAlignment.Center;
		countLabel.MouseFilter = MouseFilterEnum.Ignore;
		box.AddChild(countLabel);

		return tile;
	}

	private void RefreshExchangeTab()
	{
		if (_player == null)
		{
			return;
		}

		for (int n = 1; n <= 9; n++)
		{
			AddExchangeRow(n);
		}
	}

	private void AddExchangeRow(int tier)
	{
		if (_player == null)
		{
			return;
		}

		string lowId = MonsterLootCatalog.GetEnhanceCrystalId(tier);
		string highId = MonsterLootCatalog.GetEnhanceCrystalId(tier + 1);
		int lowCount = _player.GetInventoryCount(lowId);
		int highCount = _player.GetInventoryCount(highId);
		string lowName = LocaleText.T(MonsterLootCatalog.GetNameKey(lowId));
		string highName = LocaleText.T(MonsterLootCatalog.GetNameKey(highId));

		CreateRowShell(out HBoxContainer content);

		// Left: the crystal pair + counts on one compact column.
		var info = new VBoxContainer { CustomMinimumSize = new Vector2(190.0f, 0.0f) };
		content.AddChild(info);

		var nameLabel = MakeLabel(17, new Color(0.96f, 0.98f, 1.0f));
		nameLabel.Text = $"{lowName}  ⇄  {highName}";
		info.AddChild(nameLabel);

		var detailLabel = MakeLabel(14, new Color(0.80f, 0.88f, 0.94f));
		detailLabel.Text = $"{lowName} ×{lowCount}  ·  {highName} ×{highCount}";
		info.AddChild(detailLabel);

		int capturedTier = tier;

		// Middle: draggable quantity bar (single row) sitting between the text and
		// the right-hand merge/split buttons.
		int maxUnits = Mathf.Max(1, Mathf.Max(_player.MaxUpgradeUnits(tier), _player.MaxDowngradeUnits(tier + 1)));
		var qtySlider = new HSlider
		{
			MinValue = 1,
			MaxValue = maxUnits,
			Step = 1,
			Value = 1,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(120.0f, 0.0f),
		};
		var qtyValue = MakeLabel(14, new Color(1.0f, 0.9f, 0.6f));
		qtyValue.CustomMinimumSize = new Vector2(64.0f, 0.0f);
		qtyValue.VerticalAlignment = VerticalAlignment.Center;
		qtyValue.Text = LocaleText.F("refine.exchange.qty", 1);
		qtySlider.ValueChanged += value => qtyValue.Text = LocaleText.F("refine.exchange.qty", (int)value);
		content.AddChild(qtySlider);
		content.AddChild(qtyValue);

		var upButton = new Button
		{
			Text = LocaleText.T("refine.exchange.up"),
			CustomMinimumSize = new Vector2(140.0f, 48.0f),
			Disabled = _player.MaxUpgradeUnits(tier) < 1,
		};
		upButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.TryUpgradeCrystals(capturedTier, (int)qtySlider.Value);
				RefreshAll();
			}
		};
		content.AddChild(upButton);

		var downButton = new Button
		{
			Text = LocaleText.T("refine.exchange.down"),
			CustomMinimumSize = new Vector2(140.0f, 48.0f),
			Disabled = _player.MaxDowngradeUnits(tier + 1) < 1,
		};
		downButton.Pressed += () =>
		{
			if (_player != null)
			{
				_player.TryDowngradeCrystals(capturedTier + 1, (int)qtySlider.Value);
				RefreshAll();
			}
		};
		content.AddChild(downButton);
	}

	private PanelContainer CreateRowShell(out HBoxContainer content)
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
		_itemList.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		row.AddChild(margin);

		content = new HBoxContainer();
		content.AddThemeConstantOverride("separation", 12);
		margin.AddChild(content);
		return row;
	}

	private void BuildPanel()
	{
		Name = "RefinementPanel";
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

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		root.AddChild(header);

		_titleLabel = MakeLabel(24, new Color(0.82f, 0.90f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(160.0f, 0.0f);
		header.AddChild(_goldLabel);

		_tabBar = new HBoxContainer();
		_tabBar.AddThemeConstantOverride("separation", 8);
		root.AddChild(_tabBar);

		_tabRefineButton = CreateTabButton("refine.tab_refine");
		_tabRefineButton.Pressed += () =>
		{
			_tab = 0;
			RefreshAll();
		};
		_tabBar.AddChild(_tabRefineButton);

		_tabDismantleButton = CreateTabButton("refine.tab_dismantle");
		_tabDismantleButton.Pressed += () =>
		{
			_tab = 1;
			RefreshAll();
		};
		_tabBar.AddChild(_tabDismantleButton);

		_tabExchangeButton = CreateTabButton("refine.tab_exchange");
		_tabExchangeButton.Pressed += () =>
		{
			_tab = 2;
			RefreshAll();
		};
		_tabBar.AddChild(_tabExchangeButton);

		_hintLabel = MakeLabel(15, new Color(0.72f, 0.82f, 0.88f));
		_hintLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		root.AddChild(_hintLabel);

		_scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		_scroll.Resized += UpdateGridColumns;
		root.AddChild(_scroll);

		_itemList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_itemList.AddThemeConstantOverride("separation", 8);
		_scroll.AddChild(_itemList);

		// Fixed detail + confirm strip for the grid tabs (refine / dismantle): the
		// selected item's cost/yield shows here and the player confirms the action.
		_detailPanel = new PanelContainer { Visible = false };
		var detailStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.07f, 0.08f, 0.10f, 0.96f),
			BorderColor = new Color(0.40f, 0.50f, 0.62f, 0.82f),
		};
		detailStyle.SetBorderWidthAll(1);
		detailStyle.SetCornerRadiusAll(6);
		detailStyle.SetContentMarginAll(10);
		_detailPanel.AddThemeStyleboxOverride("panel", detailStyle);
		root.AddChild(_detailPanel);

		var detailRow = new HBoxContainer();
		detailRow.AddThemeConstantOverride("separation", 12);
		_detailPanel.AddChild(detailRow);

		_detailLabel = MakeLabel(15, new Color(0.86f, 0.92f, 0.98f));
		_detailLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_detailLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		detailRow.AddChild(_detailLabel);

		_confirmButton = new Button
		{
			Text = LocaleText.T("refine.button"),
			CustomMinimumSize = new Vector2(150.0f, 48.0f),
			SizeFlagsVertical = SizeFlags.ShrinkCenter,
		};
		_confirmButton.Pressed += OnConfirmPressed;
		detailRow.AddChild(_confirmButton);

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);

		_itemInfo = new FloatingTooltip
		{
			Name = "RefinementItemInfo",
			MaxWidth = 460.0f,
			MinWidth = 240.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.58f,
			MinBodyHeight = 64.0f,
			ZIndex = 100,
		};
		AddChild(_itemInfo);
	}

	private void ShowItemInfo(string itemId)
	{
		if (_itemInfo == null || _player == null)
		{
			return;
		}

		string title = InventoryPanel.BuildItemTooltipTitle(itemId);
		string body = InventoryPanel.BuildItemTooltipBody(itemId, string.Empty);
		body += $"\n{LocaleText.F("shop.owned_count", _player.GetInventoryCount(itemId))}";
		_itemInfo.ShowTooltip(title, body, this);
	}

	private void HideItemInfo()
	{
		_itemInfo?.HideTooltip();
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
