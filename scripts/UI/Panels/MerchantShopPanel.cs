using Godot;
using System.Collections.Generic;

public partial class MerchantShopPanel : PanelContainer
{
	private enum ItemCategory
	{
		Materials,
		Gems,
		Consumables,
		Special,
	}

	private PlayerController? _player;
	private PlayerController.MerchantShopKind _shopKind;
	private VBoxContainer _buySection = null!;
	private VBoxContainer _sellSection = null!;
	private VBoxContainer _buyList = null!;
	private VBoxContainer _sellList = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _noticeLabel = null!;
	private HBoxContainer _categoryTabs = null!;
	private HBoxContainer _tradeModeTabs = null!;
	private HBoxContainer _refreshRow = null!;
	private Label _refreshLabel = null!;
	private Button _refreshButton = null!;
	private Button _buyTabButton = null!;
	private Button _sellTabButton = null!;
	private FloatingTooltip _tooltip = null!;
	private readonly Dictionary<ItemCategory, Button> _categoryButtons = new();
	private ItemCategory _selectedCategory = ItemCategory.Materials;
	private bool _showSellList;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Visible || _shopKind != PlayerController.MerchantShopKind.ItemShop)
		{
			return;
		}

		if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Tab })
		{
			CycleItemCategory();
			GetViewport().SetInputAsHandled();
		}
	}

	public System.Action? CloseRequested { get; set; }

	public override void _Process(double delta)
	{
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}

		if (Visible && _player != null && _player.IsMerchantShopRefreshable(_shopKind))
		{
			_refreshLabel.Text = _player.GetMerchantRefreshCountdownText();
		}
	}

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

	public void Open(PlayerController.MerchantShopKind shopKind)
	{
		_shopKind = shopKind;
		_showSellList = false;
		_selectedCategory = ItemCategory.Materials;
		SetPanelVisible(true);
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
		if (_buyList == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T(_shopKind switch
		{
			PlayerController.MerchantShopKind.Blacksmith => "shop.blacksmith.trade",
			PlayerController.MerchantShopKind.PetShop => "shop.pet.trade",
			_ => "shop.item.trade",
		});
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		_noticeLabel.Visible = _shopKind == PlayerController.MerchantShopKind.PetShop;
		_noticeLabel.Text = _noticeLabel.Visible ? LocaleText.T("shop.pet.notice") : string.Empty;
		_categoryTabs.Visible = _shopKind == PlayerController.MerchantShopKind.ItemShop;
		RefreshCategoryTabs();
		bool canSell = _shopKind != PlayerController.MerchantShopKind.PetShop;
		if (!canSell)
		{
			_showSellList = false;
		}
		_tradeModeTabs.Visible = canSell;
		_buyTabButton.Text = LocaleText.T("shop.buy");
		_sellTabButton.Text = LocaleText.T("shop.sell");
		_buyTabButton.ButtonPressed = !_showSellList;
		_sellTabButton.ButtonPressed = _showSellList;
		bool canRefresh = _player != null && _player.IsMerchantShopRefreshable(_shopKind);
		_refreshRow.Visible = canRefresh;
		if (canRefresh)
		{
			_refreshLabel.Text = _player!.GetMerchantRefreshCountdownText();
			_refreshButton.Text = LocaleText.F("mercenary.button.refresh", _player.MerchantManualRefreshCost);
			_refreshButton.Disabled = _player.Gold < _player.MerchantManualRefreshCost;
		}
		_buySection.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_buySection.Visible = !_showSellList;
		_sellSection.Visible = _showSellList;
		ClearChildren(_buyList);
		ClearChildren(_sellList);
		HideTradeTooltip();

		if (_player == null)
		{
			return;
		}

		if (_showSellList)
		{
			foreach (PlayerController.ShopTradeEntry entry in _player.GetShopSellEntries(_shopKind))
			{
				if (!ShouldShowEntry(entry))
				{
					continue;
				}

				AddTradeRow(_sellList, entry, false);
			}

			if (_sellList.GetChildCount() == 0)
			{
				var empty = MakeLabel(15, new Color(0.70f, 0.76f, 0.82f));
				empty.Text = _shopKind == PlayerController.MerchantShopKind.ItemShop
					? LocaleText.T("shop.category.empty")
					: LocaleText.T("shop.sell.empty");
				_sellList.AddChild(empty);
			}
			return;
		}

		foreach (PlayerController.ShopTradeEntry entry in _player.GetShopBuyEntries(_shopKind))
		{
			if (!ShouldShowEntry(entry))
			{
				continue;
			}

			AddTradeRow(_buyList, entry, true);
		}

		if (_buyList.GetChildCount() == 0)
		{
			var empty = MakeLabel(15, new Color(0.70f, 0.76f, 0.82f));
			empty.Text = LocaleText.T("shop.category.empty");
			_buyList.AddChild(empty);
		}

	}

	private void BuildPanel()
	{
		Name = "MerchantShopPanel";
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.Center);
		OffsetLeft = -392.0f;
		OffsetRight = 392.0f;
		OffsetTop = -255.0f;
		OffsetBottom = 255.0f;
		CustomMinimumSize = new Vector2(784.0f, 510.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.042f, 0.048f, 0.058f, 0.97f),
			BorderColor = new Color(0.55f, 0.68f, 0.78f, 0.95f),
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

		_titleLabel = MakeLabel(24, new Color(1.0f, 0.94f, 0.78f));
		_titleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(18, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(170.0f, 0.0f);
		header.AddChild(_goldLabel);

		_noticeLabel = MakeLabel(15, new Color(0.76f, 0.88f, 1.0f));
		_noticeLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_noticeLabel.CustomMinimumSize = new Vector2(0.0f, 34.0f);
		root.AddChild(_noticeLabel);

		_categoryTabs = new HBoxContainer();
		_categoryTabs.AddThemeConstantOverride("separation", 8);
		root.AddChild(_categoryTabs);

		AddCategoryTab(ItemCategory.Materials);
		AddCategoryTab(ItemCategory.Gems);
		AddCategoryTab(ItemCategory.Consumables);
		AddCategoryTab(ItemCategory.Special);

		_tradeModeTabs = new HBoxContainer();
		_tradeModeTabs.AddThemeConstantOverride("separation", 8);
		root.AddChild(_tradeModeTabs);

		_buyTabButton = CreateModeTab("shop.buy");
		_buyTabButton.Pressed += () =>
		{
			_showSellList = false;
			RefreshAll();
		};
		_tradeModeTabs.AddChild(_buyTabButton);

		_sellTabButton = CreateModeTab("shop.sell");
		_sellTabButton.Pressed += () =>
		{
			_showSellList = true;
			RefreshAll();
		};
		_tradeModeTabs.AddChild(_sellTabButton);

		_refreshRow = new HBoxContainer();
		_refreshRow.AddThemeConstantOverride("separation", 10);
		root.AddChild(_refreshRow);

		_refreshLabel = MakeLabel(15, new Color(0.82f, 0.92f, 1.0f));
		_refreshLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_refreshRow.AddChild(_refreshLabel);

		_refreshButton = new Button
		{
			CustomMinimumSize = new Vector2(148.0f, 38.0f),
		};
		_refreshButton.Pressed += () =>
		{
			if (_player != null && _player.TryRefreshMerchantShopManually(_shopKind))
			{
				RefreshAll();
			}
		};
		_refreshRow.AddChild(_refreshButton);

		var columns = new HBoxContainer();
		columns.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		columns.SizeFlagsVertical = SizeFlags.ExpandFill;
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);

		_buyList = CreateTradeColumn(columns, "shop.buy", out _buySection);
		_sellList = CreateTradeColumn(columns, "shop.sell", out _sellSection);

		BuildTooltipPanel();

		var closeButton = new Button
		{
			Text = LocaleText.T("dialog.button.cancel"),
			CustomMinimumSize = new Vector2(0.0f, 42.0f),
		};
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
	}

	private void AddCategoryTab(ItemCategory category)
	{
		Button button = CreateCategoryTab(GetCategoryKey(category));
		button.Pressed += () =>
		{
			_selectedCategory = category;
			RefreshAll();
		};
		_categoryTabs.AddChild(button);
		_categoryButtons[category] = button;
	}

	private static Button CreateCategoryTab(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			ToggleMode = true,
			CustomMinimumSize = new Vector2(102.0f, 30.0f),
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		ApplyTagButtonStyle(button);
		return button;
	}

	private static void ApplyTagButtonStyle(Button button)
	{
		button.AddThemeFontSizeOverride("font_size", 13);
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

	private static Button CreateModeTab(string textKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(textKey),
			ToggleMode = true,
			CustomMinimumSize = new Vector2(102.0f, 30.0f),
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
		};
		ApplyTagButtonStyle(button);
		return button;
	}

	private VBoxContainer CreateTradeColumn(HBoxContainer parent, string titleKey, out VBoxContainer section)
	{
		section = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddThemeConstantOverride("separation", 8);
		parent.AddChild(section);

		var title = MakeLabel(18, new Color(0.82f, 0.92f, 1.0f));
		title.Text = LocaleText.T(titleKey);
		section.AddChild(title);

		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		section.AddChild(scroll);

		var list = new VBoxContainer();
		list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);
		return list;
	}

	private void AddTradeRow(VBoxContainer parent, PlayerController.ShopTradeEntry entry, bool isBuy)
	{
		var row = new PanelContainer();
		bool isPetBuyRow = _shopKind == PlayerController.MerchantShopKind.PetShop && isBuy;
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.CustomMinimumSize = new Vector2(0.0f, 64.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.075f, 0.085f, 0.10f, 0.94f),
			BorderColor = new Color(0.28f, 0.34f, 0.42f, 0.70f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(6);
		row.AddThemeStyleboxOverride("panel", style);
		row.MouseFilter = MouseFilterEnum.Stop;
		row.MouseEntered += () => ShowTradeTooltip(entry, isBuy);
		row.MouseExited += HideTradeTooltip;
		parent.AddChild(row);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", isPetBuyRow ? 14 : 10);
		margin.AddThemeConstantOverride("margin_right", isPetBuyRow ? 14 : 10);
		margin.AddThemeConstantOverride("margin_top", isPetBuyRow ? 12 : 8);
		margin.AddThemeConstantOverride("margin_bottom", isPetBuyRow ? 12 : 8);
		row.AddChild(margin);

		var line = new HBoxContainer();
		line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line.AddThemeConstantOverride("separation", isPetBuyRow ? 16 : 10);
		margin.AddChild(line);

		Texture2D? itemIcon = ItemIconLibrary.Get(entry.ItemId);
		if (itemIcon != null)
		{
			line.AddChild(ItemIconLibrary.CreateRect(entry.ItemId, 46.0f));
		}

		var name = MakeLabel(isPetBuyRow ? 18 : 16, new Color(0.96f, 0.98f, 1.0f));
		name.Text = entry.DisplayName;
		name.AutowrapMode = TextServer.AutowrapMode.Off;
		name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		name.ClipText = true;
		name.VerticalAlignment = VerticalAlignment.Center;
		name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		name.SizeFlagsVertical = SizeFlags.ExpandFill;
		line.AddChild(name);

		if (!isPetBuyRow && _player != null)
		{
			var countLabel = MakeLabel(14, new Color(0.70f, 0.80f, 0.88f));
			countLabel.Text = LocaleText.F("shop.owned_count", _player.GetInventoryCount(entry.ItemId));
			countLabel.AutowrapMode = TextServer.AutowrapMode.Off;
			countLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
			countLabel.ClipText = true;
			countLabel.HorizontalAlignment = HorizontalAlignment.Right;
			countLabel.VerticalAlignment = VerticalAlignment.Center;
			countLabel.CustomMinimumSize = new Vector2(64.0f, 0.0f);
			countLabel.SizeFlagsVertical = SizeFlags.ExpandFill;
			line.AddChild(countLabel);
		}

		var action = new Button
		{
			Text = isBuy ? LocaleText.F("shop.button.buy", entry.Price) : LocaleText.F("shop.button.sell", entry.Price),
			CustomMinimumSize = new Vector2(isPetBuyRow ? 132.0f : 118.0f, 48.0f),
			Disabled = _player == null || (isBuy && _player.Gold < entry.Price),
		};
		action.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		action.MouseEntered += () => ShowTradeTooltip(entry, isBuy);
		action.MouseExited += HideTradeTooltip;
		action.Pressed += () =>
		{
			if (_player == null)
			{
				return;
			}
			bool changed = isBuy
				? _player.TryBuyShopItem(_shopKind, entry.ItemId, entry.Price)
				: _player.TrySellShopItem(_shopKind, entry.ItemId, entry.Price);
			if (changed)
			{
				RefreshAll();
			}
		};
		line.AddChild(action);
	}

	private void RefreshCategoryTabs()
	{
		foreach (KeyValuePair<ItemCategory, Button> pair in _categoryButtons)
		{
			pair.Value.Text = LocaleText.T(GetCategoryKey(pair.Key));
			pair.Value.ButtonPressed = pair.Key == _selectedCategory;
		}
	}

	private bool ShouldShowEntry(PlayerController.ShopTradeEntry entry)
	{
		return _shopKind != PlayerController.MerchantShopKind.ItemShop || GetEntryCategory(entry.ItemId) == _selectedCategory;
	}

	private static ItemCategory GetEntryCategory(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return ItemCategory.Materials;
		}

		InventoryItemKind kind = BuildCatalog.GetItemKind(itemId);
		return kind switch
		{
			InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem => ItemCategory.Gems,
			_ => ItemCategory.Special,
		};
	}

	private void CycleItemCategory()
	{
		_selectedCategory = _selectedCategory switch
		{
			ItemCategory.Materials => ItemCategory.Gems,
			ItemCategory.Gems => ItemCategory.Consumables,
			ItemCategory.Consumables => ItemCategory.Special,
			_ => ItemCategory.Materials,
		};
		RefreshAll();
	}

	private static string GetCategoryKey(ItemCategory category)
	{
		return category switch
		{
			ItemCategory.Materials => "shop.category.materials",
			ItemCategory.Gems => "shop.category.gems",
			ItemCategory.Consumables => "shop.category.consumables",
			_ => "shop.category.special",
		};
	}

	private void BuildTooltipPanel()
	{
		_tooltip = new FloatingTooltip
		{
			Name = "MerchantTradeTooltip",
			MaxWidth = 420.0f,
			MinWidth = 220.0f,
			MaxWidthRatio = 0.55f,
			MaxHeightRatio = 0.50f,
			MinBodyHeight = 56.0f,
		};
		AddChild(_tooltip);
	}

	private void ShowTradeTooltip(PlayerController.ShopTradeEntry entry, bool isBuy)
	{
		string body = _shopKind == PlayerController.MerchantShopKind.PetShop
			? entry.Detail
			: InventoryPanel.BuildItemTooltipBody(entry.ItemId, LocaleText.T(isBuy ? "shop.buy" : "shop.sell"));
		if (_player != null)
		{
			body += $"\n{LocaleText.F("shop.owned_count", _player.GetInventoryCount(entry.ItemId))}  /  {LocaleText.F(isBuy ? "shop.button.buy" : "shop.button.sell", entry.Price)}";
		}
		_tooltip.ShowTooltip(entry.DisplayName, body, this);
	}

	private void HideTradeTooltip()
	{
		if (_tooltip != null)
		{
			_tooltip.HideTooltip();
		}
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
