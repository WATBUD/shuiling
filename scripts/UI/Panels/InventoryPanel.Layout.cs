using Godot;

public partial class InventoryPanel : PanelContainer
{
	private void BuildPanel()
	{
		Name = "InventoryPanel";
		_categoryButtons.Clear();
		MouseFilter = MouseFilterEnum.Stop;
		SetAnchorsPreset(LayoutPreset.FullRect);
		OffsetLeft = 34.0f;
		OffsetRight = -34.0f;
		OffsetTop = 34.0f;
		OffsetBottom = -34.0f;
		CustomMinimumSize = new Vector2(920.0f, 560.0f);

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.034f, 0.040f, 0.050f, 0.96f),
			BorderColor = new Color(0.40f, 0.52f, 0.64f, 0.92f),
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

		_titleLabel = MakeLabel(26, new Color(1.0f, 1.0f, 1.0f));
		_titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(_titleLabel);

		_goldLabel = MakeLabel(16, new Color(1.0f, 0.84f, 0.34f));
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.CustomMinimumSize = new Vector2(160.0f, 36.0f);
		header.AddChild(_goldLabel);

		var closeButton = MakeButton(LocaleText.T("ui.close"));
		closeButton.CustomMinimumSize = new Vector2(96.0f, 36.0f);
		closeButton.Pressed += OnClosePressed;
		header.AddChild(closeButton);

		var content = new HBoxContainer();
		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		content.AddThemeConstantOverride("separation", 14);
		root.AddChild(content);

		// Far left: companion selector list.
		var companionSection = MakeSection(LocaleText.T("inventory.companions"), new Vector2(178.0f, 0.0f));
		content.AddChild(companionSection);
		_companionList = MakeScrollableList(companionSection);

		// Middle (merged panel): equipment slots on top, character / ability info below.
		var buildScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(330.0f, 0.0f),
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		content.AddChild(buildScroll);
		var buildSection = MakeSection(LocaleText.T("inventory.equipment_slots"), new Vector2(330.0f, 0.0f));
		buildSection.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		buildSection.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		buildScroll.AddChild(buildSection);

		_buildSummaryLabel = MakeLabel(13, new Color(0.74f, 0.83f, 0.90f));
		buildSection.AddChild(_buildSummaryLabel);

		var slotGrid = new GridContainer { Columns = 3 };
		slotGrid.AddThemeConstantOverride("h_separation", 8);
		slotGrid.AddThemeConstantOverride("v_separation", 8);
		buildSection.AddChild(slotGrid);

		// Gear first (helmet/weapon/armor/boots + four accessory rings), then the
		// main core and support cores.
		_supportButtons.Clear();
		_accessoryButtons.Clear();
		_helmetButton = AddSlotButton(slotGrid, EquipTarget.Helmet);
		_weaponButton = AddSlotButton(slotGrid, EquipTarget.Weapon);
		_armorButton = AddSlotButton(slotGrid, EquipTarget.Armor);
		_bootsButton = AddSlotButton(slotGrid, EquipTarget.Boots);
		_supportButtons.Add(AddSupportSlotButton(slotGrid, 0));
		_supportButtons.Add(AddSupportSlotButton(slotGrid, 1));
		for (int index = 0; index < BuildCatalog.AccessorySlotCount; index++)
		{
			_accessoryButtons.Add(AddAccessorySlotButton(slotGrid, index));
		}
		// Legacy attribute core remains in save/combat data for compatibility, but it is
		// no longer an equipment-grid slot. The visible core layout is exactly one main
		// core plus six freely chosen support-core slots.
		_attributeButton = new Button { Visible = false, Disabled = true };
		for (int index = 2; index < BuildCatalog.SupportCoreSlotCount; index++)
		{
			_supportButtons.Add(AddSupportSlotButton(slotGrid, index));
		}

		var infoHeader = MakeLabel(15, new Color(0.86f, 0.92f, 0.98f));
		infoHeader.Text = LocaleText.T("inventory.companion_info");
		buildSection.AddChild(infoHeader);

		_companionInfoCard = new CompanionInfoCard
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
		};
		buildSection.AddChild(_companionInfoCard);

		var itemSection = MakeSection(LocaleText.T("inventory.items"), new Vector2(320.0f, 0.0f));
		itemSection.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		content.AddChild(itemSection);

		var tabRow = new HBoxContainer();
		tabRow.AddThemeConstantOverride("separation", 6);
		itemSection.AddChild(tabRow);
		AddCategoryButton(tabRow, InventoryCategory.All, "inventory.tab.all");
		AddCategoryButton(tabRow, InventoryCategory.Equipment, "inventory.tab.equipment");
		AddCategoryButton(tabRow, InventoryCategory.Gems, "inventory.tab.gems");
		AddCategoryButton(tabRow, InventoryCategory.Materials, "inventory.tab.materials");
		AddCategoryButton(tabRow, InventoryCategory.Consumables, "inventory.tab.consumables");

		var sortRow = new HBoxContainer();
		sortRow.AddThemeConstantOverride("separation", 8);
		itemSection.AddChild(sortRow);
		_sortLabel = MakeLabel(13, new Color(0.76f, 0.84f, 0.90f));
		_sortLabel.VerticalAlignment = VerticalAlignment.Center;
		_sortLabel.CustomMinimumSize = new Vector2(52.0f, 32.0f);
		sortRow.AddChild(_sortLabel);
		_sortOption = new OptionButton
		{
			CustomMinimumSize = new Vector2(0.0f, 32.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_sortOption.AddThemeFontSizeOverride("font_size", 13);
		_sortOption.AddItem(LocaleText.T("inventory.sort.category"), (int)InventorySortMode.Category);
		_sortOption.AddItem(LocaleText.T("inventory.sort.name"), (int)InventorySortMode.Name);
		_sortOption.AddItem(LocaleText.T("inventory.sort.quantity"), (int)InventorySortMode.Quantity);
		_sortOption.Select((int)_selectedSortMode);
		_sortOption.ItemSelected += OnSortModeSelected;
		sortRow.AddChild(_sortOption);
		_sortDirectionButton = MakeButton(string.Empty);
		_sortDirectionButton.CustomMinimumSize = new Vector2(42.0f, 32.0f);
		_sortDirectionButton.AddThemeFontSizeOverride("font_size", 18);
		_sortDirectionButton.Pressed += ToggleSortDirection;
		sortRow.AddChild(_sortDirectionButton);

		_bagCountLabel = MakeLabel(13, new Color(0.72f, 0.80f, 0.86f));
		itemSection.AddChild(_bagCountLabel);

		_itemScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 270.0f),
		};
		itemSection.AddChild(_itemScroll);

		_itemGrid = new GridContainer
		{
			Columns = 1,
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
		};
		_itemGrid.AddThemeConstantOverride("h_separation", ItemIconLibrary.InventoryGridGap);
		_itemGrid.AddThemeConstantOverride("v_separation", ItemIconLibrary.InventoryGridGap);
		_itemScroll.AddChild(_itemGrid);
		_itemScroll.Resized += UpdateResponsiveItemColumns;
		UpdateResponsiveItemColumns();

		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 8);
		itemSection.AddChild(actionRow);
		_equipSelectedButton = MakeButton(LocaleText.T("inventory.action.equip"));
		_equipSelectedButton.CustomMinimumSize = new Vector2(120.0f, 34.0f);
		_equipSelectedButton.Pressed += OnEquipSelectedPressed;
		actionRow.AddChild(_equipSelectedButton);
		_useSelectedButton = MakeButton(LocaleText.T("inventory.action.use"));
		_useSelectedButton.CustomMinimumSize = new Vector2(120.0f, 34.0f);
		_useSelectedButton.Pressed += OnUseSelectedPressed;
		actionRow.AddChild(_useSelectedButton);
		_upgradeSkillGemButton = MakeButton(LocaleText.T("inventory.action.upgrade"));
		_upgradeSkillGemButton.CustomMinimumSize = new Vector2(190.0f, 34.0f);
		_upgradeSkillGemButton.Pressed += OnUpgradeSkillGemPressed;
		_upgradeSkillGemButton.Visible = false;
		actionRow.AddChild(_upgradeSkillGemButton);
		BuildTooltip();
		RefreshText();
	}

	private void AddCategoryButton(HBoxContainer parent, InventoryCategory category, string labelKey)
	{
		var button = MakeButton(LocaleText.T(labelKey));
		button.ToggleMode = true;
		button.CustomMinimumSize = new Vector2(76.0f, 34.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.Pressed += () => SelectCategory(category);
		parent.AddChild(button);
		_categoryButtons[category] = button;
	}

	private void BuildTooltip()
	{
		_tooltip = new FloatingTooltip
		{
			Name = "InventoryItemTooltip",
			MaxWidth = 360.0f,
			MinWidth = 180.0f,
			MaxWidthRatio = 0.36f,
			MaxHeightRatio = 0.50f,
			TopLevel = true,
			ZIndex = 100,
		};
		AddChild(_tooltip);
	}

	private Button AddSlotButton(GridContainer parent, EquipTarget target)
	{
		var button = new InventoryEquipDropButton
		{
			Text = string.Empty,
			CanAcceptItem = itemId => IsCompatibleItemForTarget(itemId, target),
			ItemDropped = itemId => EquipItemToTarget(itemId, target),
		};
		ApplyButtonStyle(button);
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.Pressed += () => SelectTarget(target);
		button.MouseEntered += () => ShowTooltipForTarget(target);
		button.MouseExited += HideItemTooltip;
		button.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true })
			{
				UnequipSlot(target);
				button.AcceptEvent();
			}
		};
		parent.AddChild(button);
		return button;
	}

	// Core buttons are addressed by their fixed index: 0 is main, 1..6 are support.
	private Button AddSupportSlotButton(GridContainer parent, int index)
	{
		var button = new InventoryEquipDropButton
		{
			Text = string.Empty,
			CanAcceptItem = itemId => IsSupportCoreCompatible(itemId, index),
			ItemDropped = itemId => EquipSupportCore(itemId, index),
		};
		ApplyButtonStyle(button);
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.Pressed += () => SelectSupportSlot(index);
		button.MouseEntered += () => ShowSupportTooltip(index);
		button.MouseExited += HideItemTooltip;
		button.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true })
			{
				UnequipSupportSlot(index);
				button.AcceptEvent();
			}
		};
		parent.AddChild(button);
		return button;
	}

	// One of the four accessory (ring) slots, addressed by index.
	private Button AddAccessorySlotButton(GridContainer parent, int index)
	{
		var button = new InventoryEquipDropButton
		{
			Text = string.Empty,
			CanAcceptItem = itemId => IsCompatibleItemForTarget(itemId, EquipTarget.Accessory),
			ItemDropped = itemId => EquipAccessoryToSlot(itemId, index),
		};
		ApplyButtonStyle(button);
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.Pressed += () => SelectAccessorySlot(index);
		button.MouseEntered += () => ShowAccessoryTooltip(index);
		button.MouseExited += HideItemTooltip;
		button.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true })
			{
				UnequipAccessorySlot(index);
				button.AcceptEvent();
			}
		};
		parent.AddChild(button);
		return button;
	}
}
