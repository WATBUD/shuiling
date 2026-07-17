using Godot;
using System;
using System.Collections.Generic;

public partial class InventoryItemDragButton : Button
{
	public string DragItemId { get; set; } = string.Empty;

	public override Variant _GetDragData(Vector2 atPosition)
	{
		if (string.IsNullOrEmpty(DragItemId))
		{
			return default;
		}

		var preview = new Button
		{
			Text = Text,
			Icon = Icon,
			CustomMinimumSize = new Vector2(64.0f, 72.0f),
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.88f),
		};
		SetDragPreview(preview);
		return DragItemId;
	}
}

public partial class InventoryEquipDropButton : Button
{
	public Func<string, bool>? CanAcceptItem { get; set; }
	public Action<string>? ItemDropped { get; set; }

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.VariantType == Variant.Type.String
			&& CanAcceptItem?.Invoke(data.AsString()) == true;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (data.VariantType == Variant.Type.String)
		{
			ItemDropped?.Invoke(data.AsString());
		}
	}
}

public partial class InventoryPanel : PanelContainer
{
	private enum EquipTarget
	{
		Helmet,
		Weapon,
		Armor,
		Accessory,
		AttributeGem,
		SupportCore,
	}

	private enum InventoryCategory
	{
		All,
		Equipment,
		Gems,
		Materials,
	}

	private PlayerController? _player;
	private SimpleActor? _selectedActor;
	private EquipTarget _selectedTarget = EquipTarget.Weapon;
	private int _selectedSupportIndex;
	private InventoryCategory _selectedCategory = InventoryCategory.All;
	private string _selectedItemId = string.Empty;
	private readonly Dictionary<InventoryCategory, Button> _categoryButtons = new();
	private VBoxContainer _companionList = null!;
	private GridContainer _itemGrid = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _selectedSlotLabel = null!;
	private Label _buildSummaryLabel = null!;
	private CompanionInfoCard _companionInfoCard = null!;
	private Label _bagCountLabel = null!;
	private Label _itemDetailTitleLabel = null!;
	private Label _itemDetailBodyLabel = null!;
	private Button _equipSelectedButton = null!;
	private Button _useSelectedButton = null!;
	private Button _upgradeSkillGemButton = null!;
	private Button _helmetButton = null!;
	private Button _weaponButton = null!;
	private Button _armorButton = null!;
	private Button _accessoryButton = null!;
	private Button _attributeButton = null!;
	private readonly List<Button> _supportButtons = new();
	private FloatingTooltip _tooltip = null!;
	private AcceptDialog? _warningDialog;

	public System.Action? CloseRequested { get; set; }

	public override void _Ready()
	{
		BuildPanel();
		LocaleText.LanguageChanged += OnLanguageChanged;
		SetPanelVisible(false);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= OnLanguageChanged;
	}

	public override void _Process(double delta)
	{
		if (_tooltip != null && _tooltip.Visible)
		{
			_tooltip.PositionNearMouse(this);
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		if (_companionList != null)
		{
			SelectDefaultActor();
			RefreshAll();
		}
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (!visible)
		{
			HideItemTooltip();
		}

		if (visible)
		{
			SelectDefaultActor();
			RefreshAll();
		}
	}

	public void SelectActor(SimpleActor actor)
	{
		if (!IsInstanceValid(actor) || !actor.IsCaptured)
		{
			return;
		}

		_selectedActor = actor;
		RefreshAll();
	}

	public void RefreshAll()
	{
		if (_player == null || _companionList == null)
		{
			return;
		}

		RefreshText();
		RefreshCompanionList();
		RefreshSlotButtons();
		RefreshItemList();
		RefreshDetails();
		RefreshSelectedItemDetails();
	}

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

		var slotGrid = new GridContainer { Columns = 2 };
		slotGrid.AddThemeConstantOverride("h_separation", 8);
		slotGrid.AddThemeConstantOverride("v_separation", 8);
		buildSection.AddChild(slotGrid);

		_helmetButton = AddSlotButton(slotGrid, EquipTarget.Helmet);
		_weaponButton = AddSlotButton(slotGrid, EquipTarget.Weapon);
		_armorButton = AddSlotButton(slotGrid, EquipTarget.Armor);
		_accessoryButton = AddSlotButton(slotGrid, EquipTarget.Accessory);
		_attributeButton = AddSlotButton(slotGrid, EquipTarget.AttributeGem);
		_supportButtons.Clear();
		for (int index = 0; index < BuildCatalog.SupportCoreSlotCount; index++)
		{
			_supportButtons.Add(AddSupportSlotButton(slotGrid, index));
		}

		_selectedSlotLabel = MakeLabel(14, new Color(0.98f, 0.98f, 0.98f));
		buildSection.AddChild(_selectedSlotLabel);

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

		_bagCountLabel = MakeLabel(13, new Color(0.72f, 0.80f, 0.86f));
		itemSection.AddChild(_bagCountLabel);

		var itemScroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(0.0f, 270.0f),
		};
		itemSection.AddChild(itemScroll);

		_itemGrid = new GridContainer
		{
			Columns = 5,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
		};
		_itemGrid.AddThemeConstantOverride("h_separation", 6);
		_itemGrid.AddThemeConstantOverride("v_separation", 6);
		itemScroll.AddChild(_itemGrid);

		var detailPanel = MakeInfoPanel(new Vector2(0.0f, 145.0f));
		itemSection.AddChild(detailPanel);

		var detailMargin = new MarginContainer();
		detailMargin.AddThemeConstantOverride("margin_left", 10);
		detailMargin.AddThemeConstantOverride("margin_right", 10);
		detailMargin.AddThemeConstantOverride("margin_top", 8);
		detailMargin.AddThemeConstantOverride("margin_bottom", 8);
		detailPanel.AddChild(detailMargin);

		var detailRows = new VBoxContainer();
		detailRows.AddThemeConstantOverride("separation", 6);
		detailMargin.AddChild(detailRows);

		_itemDetailTitleLabel = MakeLabel(16, new Color(1.0f, 0.92f, 0.58f));
		detailRows.AddChild(_itemDetailTitleLabel);

		_itemDetailBodyLabel = MakeLabel(12, new Color(0.82f, 0.88f, 0.94f));
		_itemDetailBodyLabel.CustomMinimumSize = new Vector2(0.0f, 58.0f);
		detailRows.AddChild(_itemDetailBodyLabel);

		var actionRow = new HBoxContainer();
		actionRow.AddThemeConstantOverride("separation", 8);
		detailRows.AddChild(actionRow);
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

	private VBoxContainer MakeSection(string title, Vector2 minSize)
	{
		var section = new VBoxContainer
		{
			CustomMinimumSize = minSize,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		section.AddThemeConstantOverride("separation", 10);

		var label = MakeLabel(17, new Color(0.86f, 0.92f, 0.98f));
		label.Text = title;
		section.AddChild(label);
		return section;
	}

	private static PanelContainer MakeInfoPanel(Vector2 minSize)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = minSize,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.018f, 0.024f, 0.032f, 0.78f),
			BorderColor = new Color(0.22f, 0.30f, 0.38f, 0.8f),
		};
		style.SetBorderWidthAll(1);
		style.SetCornerRadiusAll(5);
		panel.AddThemeStyleboxOverride("panel", style);
		return panel;
	}

	private static VBoxContainer MakeScrollableList(VBoxContainer section)
	{
		var scroll = new ScrollContainer
		{
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};
		section.AddChild(scroll);

		var list = new VBoxContainer();
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);
		return list;
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

	// One support core slot, addressed by index instead of a fixed enum value so the
	// number of slots can grow with the creature.
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

	private void SelectSupportSlot(int index)
	{
		_selectedSupportIndex = index;
		SelectTarget(EquipTarget.SupportCore);
	}

	private void ShowSupportTooltip(int index)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return;
		}

		ShowItemTooltip(_selectedActor.BuildLoadout.GetSkillGemId(index), LocaleText.F("build.slot.support_core", index + 1));
	}

	private void EquipSupportCore(string itemId, int index)
	{
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor)
			|| !_player.HasInventoryItem(itemId) || !IsSupportCoreCompatible(itemId, index))
		{
			return;
		}

		_selectedSupportIndex = index;
		_selectedTarget = EquipTarget.SupportCore;
		PerformEquip(itemId);
	}

	private void UnequipSupportSlot(int index)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return;
		}

		string displaced = _selectedActor.BuildLoadout.GetSkillGemId(index);
		_selectedActor.EquipSkillGem(index, "gem.skill.none");
		_player?.ReturnInventoryItemFromUnequip(displaced);
		HideItemTooltip();
		RefreshAll();
	}

	private bool IsSupportSlotUnlocked(int index)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return true;
		}

		return BuildCatalog.GetUnlockedSupportCoreCount(_selectedActor.Level) > index;
	}

	private bool IsSupportCoreCompatible(string itemId, int index)
	{
		if (!IsSupportSlotUnlocked(index) || MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return false;
		}

		if (BuildCatalog.GetItemKind(itemId) != InventoryItemKind.SkillGem)
		{
			return false;
		}

		return !(BuildCatalog.IsProjectileSupportGem(itemId)
			&& (_selectedActor == null || !IsInstanceValid(_selectedActor) || !BuildCatalog.HasRangedActiveSkill(_selectedActor.BuildLoadout)));
	}

	// Double-clicking an equipped slot takes the item off and returns it to the bag
	// (equipping consumed it, so unequipping must give it back).
	private void UnequipSlot(EquipTarget target)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return;
		}

		string displaced = GetEquippedItemId(target);
		switch (target)
		{
			case EquipTarget.Helmet:
			case EquipTarget.Weapon:
			case EquipTarget.Armor:
			case EquipTarget.Accessory:
				EquipmentSlot slot = ToEquipmentSlot(target);
				_selectedActor.EquipBuildEquipment(slot, GetEmptyEquipmentId(slot));
				break;
			case EquipTarget.AttributeGem:
				_selectedActor.EquipAttributeGem("gem.attribute.none");
				break;
			case EquipTarget.SupportCore:
				_selectedActor.EquipSkillGem(_selectedSupportIndex, "gem.skill.none");
				break;
		}

		_player?.ReturnInventoryItemFromUnequip(displaced);
		HideItemTooltip();
		RefreshAll();
	}

	private static EquipmentSlot ToEquipmentSlot(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.Helmet => EquipmentSlot.Helmet,
			EquipTarget.Weapon => EquipmentSlot.Weapon,
			EquipTarget.Armor => EquipmentSlot.Armor,
			_ => EquipmentSlot.Accessory,
		};
	}

	private void SelectDefaultActor()
	{
		if (_player == null)
		{
			return;
		}

		if (_selectedActor != null && IsInstanceValid(_selectedActor) && _selectedActor.IsCaptured)
		{
			return;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				_selectedActor = actor;
				return;
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				_selectedActor = actor;
				return;
			}
		}
	}

	private void SelectTarget(EquipTarget target)
	{
		HideItemTooltip();
		_selectedTarget = target;
		RefreshSlotButtons();
		RefreshItemList();
		RefreshDetails();
		RefreshSelectedItemDetails();
	}

	private void SelectCategory(InventoryCategory category)
	{
		_selectedCategory = category;
		if (!string.IsNullOrEmpty(_selectedItemId) && !ShouldShowItemInCategory(_selectedItemId, category))
		{
			_selectedItemId = string.Empty;
		}

		HideItemTooltip();
		RefreshItemList();
		RefreshSelectedItemDetails();
	}

	private void RefreshCompanionList()
	{
		ClearChildren(_companionList);
		if (_player == null)
		{
			return;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured)
			{
				AddCompanionButton(actor, LocaleText.T("party.active"));
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !_player.IsInActiveParty(actor))
			{
				AddCompanionButton(actor, LocaleText.T("party.collection"));
			}
		}

		if (_companionList.GetChildCount() == 0)
		{
			var empty = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
			empty.Text = LocaleText.T("inventory.no_companions");
			_companionList.AddChild(empty);
		}
	}

	private void AddCompanionButton(SimpleActor actor, string groupLabel)
	{
		var button = MakeButton($"{groupLabel}  {actor.LocalizedDisplayName}");
		button.Alignment = HorizontalAlignment.Left;
		button.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		button.AddThemeColorOverride("font_color", actor == _selectedActor ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.92f, 0.96f, 1.0f));
		button.Pressed += () => SelectActor(actor);
		_companionList.AddChild(button);
	}

	private void RefreshSlotButtons()
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			SetSlotsDisabled(true);
			return;
		}

		SetSlotsDisabled(false);
		CompanionBuildLoadout loadout = _selectedActor.BuildLoadout;
		SetSlotButton(_helmetButton, EquipTarget.Helmet, loadout.HelmetId, BuildCatalog.GetEquipment(loadout.HelmetId).NameKey);
		SetSlotButton(_weaponButton, EquipTarget.Weapon, loadout.WeaponId, BuildCatalog.GetEquipment(loadout.WeaponId).NameKey);
		SetSlotButton(_armorButton, EquipTarget.Armor, loadout.ArmorId, BuildCatalog.GetEquipment(loadout.ArmorId).NameKey);
		SetSlotButton(_accessoryButton, EquipTarget.Accessory, loadout.AccessoryId, BuildCatalog.GetEquipment(loadout.AccessoryId).NameKey);
		SetSlotButton(_attributeButton, EquipTarget.AttributeGem, loadout.AttributeGemId, BuildCatalog.GetAttributeGem(loadout.AttributeGemId).NameKey);

		// Show the unlocked support cores plus one locked preview of the next slot; the
		// rest stay hidden until the creature grows into them.
		int unlockedSupport = BuildCatalog.GetUnlockedSupportCoreCount(_selectedActor.Level);
		int visibleSupport = Mathf.Min(unlockedSupport + 1, _supportButtons.Count);
		for (int index = 0; index < _supportButtons.Count; index++)
		{
			Button button = _supportButtons[index];
			button.Visible = index < visibleSupport;
			if (button.Visible)
			{
				SetSupportSlotButton(button, index, loadout);
			}
		}

		if (_selectedTarget == EquipTarget.SupportCore && _selectedSupportIndex >= visibleSupport)
		{
			_selectedTarget = EquipTarget.Weapon;
		}
	}

	private void SetSlotButton(Button button, EquipTarget target, string itemId, string itemNameKey)
	{
		if (ShowLockedSlot(button, target))
		{
			return;
		}

		button.Text = $"{GetTargetName(target)}\n{LocaleText.T(itemNameKey)}";
		ItemIconLibrary.Apply(button, itemId, 26);
		button.AddThemeColorOverride("font_color", target == _selectedTarget ? new Color(1.0f, 0.92f, 0.50f) : new Color(0.92f, 0.96f, 1.0f));
	}

	private void SetSupportSlotButton(Button button, int index, CompanionBuildLoadout loadout)
	{
		string coreName = LocaleText.F("build.slot.support_core", index + 1);
		if (!IsSupportSlotUnlocked(index))
		{
			button.Text = $"{coreName}\n{LocaleText.F("inventory.core_locked", BuildCatalog.GetSupportCoreUnlockLevel(index))}";
			button.Icon = null;
			button.AddThemeColorOverride("font_color", new Color(0.52f, 0.55f, 0.60f));
			return;
		}

		string gemId = loadout.GetSkillGemId(index);
		string gemName = LocaleText.T(BuildCatalog.GetSkillGem(gemId).NameKey);
		if (BuildCatalog.IsUpgradeableSkillGem(gemId))
		{
			gemName = LocaleText.F("inventory.gem_level", gemName, loadout.GetSkillGemLevel(index));
		}

		button.Text = $"{coreName}\n{gemName}";
		ItemIconLibrary.Apply(button, gemId, 26);
		bool selected = _selectedTarget == EquipTarget.SupportCore && _selectedSupportIndex == index;
		button.AddThemeColorOverride("font_color", selected ? new Color(1.0f, 0.92f, 0.50f) : new Color(0.92f, 0.96f, 1.0f));
	}

	// Core slots unlock with the creature's level. A locked slot shows the level it
	// needs and cannot hold a core yet.
	private bool IsSlotUnlocked(EquipTarget target)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return true;
		}

		return target switch
		{
			EquipTarget.AttributeGem => BuildCatalog.IsMainCoreUnlocked(_selectedActor.Level),
			EquipTarget.SupportCore => IsSupportSlotUnlocked(_selectedSupportIndex),
			_ => true,
		};
	}

	private int SlotUnlockLevel(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.AttributeGem => BuildCatalog.MainCoreUnlockLevel,
			EquipTarget.SupportCore => BuildCatalog.GetSupportCoreUnlockLevel(_selectedSupportIndex),
			_ => 0,
		};
	}

	private bool ShowLockedSlot(Button button, EquipTarget target)
	{
		if (IsSlotUnlocked(target))
		{
			return false;
		}

		button.Text = $"{GetTargetName(target)}\n{LocaleText.F("inventory.core_locked", SlotUnlockLevel(target))}";
		button.Icon = null;
		button.AddThemeColorOverride("font_color", new Color(0.52f, 0.55f, 0.60f));
		return true;
	}

	private void SetSlotsDisabled(bool disabled)
	{
		var buttons = new List<Button> { _helmetButton, _weaponButton, _armorButton, _accessoryButton, _attributeButton };
		buttons.AddRange(_supportButtons);
		foreach (Button button in buttons)
		{
			button.Disabled = disabled;
			if (disabled)
			{
				button.Text = "-";
				button.Icon = null;
			}
		}
	}

	private void RefreshItemList()
	{
		ClearChildren(_itemGrid);
		RefreshCategoryButtons();
		if (_player == null)
		{
			AddItemListMessage("inventory.no_items");
			return;
		}

		List<string> itemIds = GetVisibleInventoryItems();
		int added = 0;
		foreach (string itemId in itemIds)
		{
			AddItemSlotButton(itemId);
			added++;
		}

		if (added == 0)
		{
			AddItemListMessage("inventory.no_items");
		}

		int totalCount = 0;
		foreach (KeyValuePair<string, int> item in _player.InventoryItems)
		{
			if (item.Value > 0)
			{
				totalCount++;
			}
		}

		_bagCountLabel.Text = LocaleText.F("inventory.bag_count", totalCount);
	}

	private List<string> GetVisibleInventoryItems()
	{
		var ids = new List<string>();
		if (_player == null)
		{
			return ids;
		}

		foreach (KeyValuePair<string, int> item in _player.InventoryItems)
		{
			if (item.Value > 0 && ShouldShowItemInCategory(item.Key, _selectedCategory))
			{
				ids.Add(item.Key);
			}
		}

		SortItemIds(ids);
		return ids;
	}

	private void AddItemSlotButton(string itemId)
	{
		int count = _player?.GetInventoryCount(itemId) ?? 0;
		string countText = BuildCatalog.IsFreeItem(itemId) ? string.Empty : $"x{count}";
		var button = new InventoryItemDragButton
		{
			Text = countText,
			DragItemId = BuildCatalog.GetItemKind(itemId) is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem
				? itemId
				: string.Empty,
		};
		ApplyButtonStyle(button);
		button.CustomMinimumSize = new Vector2(64.0f, 72.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		button.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		button.Alignment = HorizontalAlignment.Center;
		button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		button.AddThemeFontSizeOverride("font_size", 11);
		ItemIconLibrary.Apply(button, itemId, 44);
		button.AddThemeColorOverride("font_color", itemId == _selectedItemId ? new Color(1.0f, 0.92f, 0.50f) : new Color(0.92f, 0.96f, 1.0f));
		button.MouseEntered += () => ShowItemTooltip(itemId, LocaleText.T("inventory.items"));
		button.MouseExited += HideItemTooltip;
		button.Pressed += () => SelectInventoryItem(itemId);
		button.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true })
			{
				OnItemActivated(itemId);
				button.AcceptEvent();
			}
		};
		_itemGrid.AddChild(button);
	}

	// Double-clicking a bag item equips it to the first valid slot in priority order
	// (equipment → its slot; gem → main core; support core → first empty unlocked slot).
	// If no slot accepts it, warn and leave the item alone.
	private void OnItemActivated(string itemId)
	{
		_selectedItemId = itemId;
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor) || !_player.HasInventoryItem(itemId))
		{
			return;
		}

		if (!ResolveFirstValidTarget(itemId, out string reasonKey))
		{
			ShowEquipWarning(reasonKey);
			return;
		}

		if (!PerformEquip(itemId))
		{
			ShowEquipWarning("inventory.warn.not_equippable");
		}
	}

	// Picks the first slot that can accept the item, preferring an empty support slot.
	// Returns false (with a reason key) when the item cannot be equipped anywhere.
	private bool ResolveFirstValidTarget(string itemId, out string reasonKey)
	{
		reasonKey = "inventory.warn.not_equippable";
		if (MonsterLootCatalog.IsMonsterLoot(itemId) || _selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return false;
		}

		switch (BuildCatalog.GetItemKind(itemId))
		{
			case InventoryItemKind.Equipment:
				_selectedTarget = EquipTargetForSlot(BuildCatalog.GetEquipment(itemId).Slot);
				return true;
			case InventoryItemKind.AttributeGem:
				if (!BuildCatalog.IsMainCoreUnlocked(_selectedActor.Level))
				{
					reasonKey = "inventory.warn.core_locked";
					return false;
				}

				_selectedTarget = EquipTarget.AttributeGem;
				return true;
			case InventoryItemKind.SkillGem:
				if (BuildCatalog.IsProjectileSupportGem(itemId) && !BuildCatalog.HasRangedActiveSkill(_selectedActor.BuildLoadout))
				{
					reasonKey = "tooltip.requires_ranged_skill";
					return false;
				}

				if (BuildCatalog.GetUnlockedSupportCoreCount(_selectedActor.Level) <= 0)
				{
					reasonKey = "inventory.warn.core_locked";
					return false;
				}

				int open = FindFirstOpenSupportSlot(_selectedActor.BuildLoadout);
				_selectedSupportIndex = open >= 0 ? open : 0;
				_selectedTarget = EquipTarget.SupportCore;
				return true;
			default:
				return false;
		}
	}

	private static EquipTarget EquipTargetForSlot(EquipmentSlot slot)
	{
		return slot switch
		{
			EquipmentSlot.Helmet => EquipTarget.Helmet,
			EquipmentSlot.Weapon => EquipTarget.Weapon,
			EquipmentSlot.Armor => EquipTarget.Armor,
			_ => EquipTarget.Accessory,
		};
	}

	private void ShowEquipWarning(string reasonKey)
	{
		if (_warningDialog == null)
		{
			_warningDialog = new AcceptDialog { Title = LocaleText.T("inventory.warn.title") };
			AddChild(_warningDialog);
		}

		_warningDialog.Title = LocaleText.T("inventory.warn.title");
		_warningDialog.DialogText = LocaleText.T(reasonKey);
		_warningDialog.PopupCentered();
	}

	// First empty support slot that is already unlocked for the selected creature.
	private int FindFirstOpenSupportSlot(CompanionBuildLoadout loadout)
	{
		int unlocked = _selectedActor != null && IsInstanceValid(_selectedActor)
			? BuildCatalog.GetUnlockedSupportCoreCount(_selectedActor.Level)
			: 0;
		for (int index = 0; index < unlocked && index < loadout.SkillGemIds.Length; index++)
		{
			if (loadout.GetSkillGemId(index) == "gem.skill.none")
			{
				return index;
			}
		}

		return -1;
	}

	private static string GetEmptyEquipmentId(EquipmentSlot slot)
	{
		return slot switch
		{
			EquipmentSlot.Helmet => "equip.helmet.none",
			EquipmentSlot.Weapon => "equip.weapon.none",
			EquipmentSlot.Armor => "equip.armor.none",
			_ => "equip.accessory.none",
		};
	}

	private static string GetInventoryItemName(string itemId)
	{
		return MonsterLootCatalog.IsMonsterLoot(itemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(itemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(itemId));
	}

	private void SelectInventoryItem(string itemId)
	{
		_selectedItemId = itemId;
		HideItemTooltip();
		RefreshItemList();
		RefreshSelectedItemDetails();
	}

	private void RefreshCategoryButtons()
	{
		foreach (KeyValuePair<InventoryCategory, Button> entry in _categoryButtons)
		{
			entry.Value.ButtonPressed = entry.Key == _selectedCategory;
			entry.Value.AddThemeColorOverride("font_color", entry.Key == _selectedCategory ? new Color(1.0f, 0.92f, 0.54f) : new Color(0.86f, 0.91f, 0.96f));
		}
	}

	private int SelectedSkillSlotIndex()
	{
		return _selectedTarget == EquipTarget.SupportCore ? _selectedSupportIndex : -1;
	}

	private void RefreshUpgradeButton()
	{
		int slot = SelectedSkillSlotIndex();
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor) || slot < 0 || !IsSlotUnlocked(_selectedTarget))
		{
			_upgradeSkillGemButton.Visible = false;
			return;
		}

		string gemId = _selectedActor.BuildLoadout.SkillGemIds[slot];
		if (!BuildCatalog.IsUpgradeableSkillGem(gemId))
		{
			_upgradeSkillGemButton.Visible = false;
			return;
		}

		_upgradeSkillGemButton.Visible = true;
		if (_player.GetCompanionSkillGemUpgradeCost(_selectedActor, slot) is not SkillGemUpgradeCost cost)
		{
			_upgradeSkillGemButton.Text = LocaleText.F("inventory.action.gem_maxed", _selectedActor.GetSkillGemLevel(slot));
			_upgradeSkillGemButton.Disabled = true;
			return;
		}

		_upgradeSkillGemButton.Text = LocaleText.F(
			"inventory.action.upgrade_gem",
			cost.NextLevel,
			cost.Gold,
			cost.MaterialCount,
			LocaleText.T(MonsterLootCatalog.GetNameKey(cost.MaterialId)));
		_upgradeSkillGemButton.Disabled = !_player.CanAffordSkillGemUpgrade(cost);
	}

	private void OnUpgradeSkillGemPressed()
	{
		int slot = SelectedSkillSlotIndex();
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor) || slot < 0)
		{
			return;
		}

		if (_player.TryUpgradeCompanionSkillGem(_selectedActor, slot))
		{
			RefreshAll();
		}
	}

	private void RefreshSelectedItemDetails()
	{
		RefreshUpgradeButton();
		if (_player == null || string.IsNullOrEmpty(_selectedItemId) || !_player.HasInventoryItem(_selectedItemId))
		{
			_selectedItemId = string.Empty;
			_itemDetailTitleLabel.Text = LocaleText.T("inventory.detail.empty_title");
			_itemDetailBodyLabel.Text = LocaleText.T("inventory.detail.empty_body");
			_equipSelectedButton.Disabled = true;
			_useSelectedButton.Disabled = true;
			return;
		}

		int count = _player.GetInventoryCount(_selectedItemId);
		_itemDetailTitleLabel.Text = $"{GetInventoryItemName(_selectedItemId)} x{count}";
		_itemDetailBodyLabel.Text = BuildItemTooltipBody(_selectedItemId, LocaleText.T("inventory.items"));
		_equipSelectedButton.Disabled = !CanEquipSelectedItem();
		_useSelectedButton.Disabled = true;
	}

	private bool CanEquipSelectedItem()
	{
		return _selectedActor != null
			&& IsInstanceValid(_selectedActor)
			&& !string.IsNullOrEmpty(_selectedItemId)
			&& IsCompatibleItemForTarget(_selectedItemId, _selectedTarget);
	}

	private bool IsCompatibleItemForTarget(string itemId, EquipTarget target)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return false;
		}

		// Cannot slot a core into a slot the creature has not unlocked yet.
		if (!IsSlotUnlocked(target))
		{
			return false;
		}

		InventoryItemKind kind = BuildCatalog.GetItemKind(itemId);
		if (kind == InventoryItemKind.SkillGem
			&& BuildCatalog.IsProjectileSupportGem(itemId)
			&& (_selectedActor == null || !IsInstanceValid(_selectedActor) || !BuildCatalog.HasRangedActiveSkill(_selectedActor.BuildLoadout)))
		{
			return false;
		}

		switch (target)
		{
			case EquipTarget.Helmet:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Helmet;
			case EquipTarget.Weapon:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Weapon;
			case EquipTarget.Armor:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Armor;
			case EquipTarget.Accessory:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Accessory;
			case EquipTarget.AttributeGem:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.AttributeGem;
			case EquipTarget.SupportCore:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.SkillGem;
			default:
				return false;
		}
	}

	private bool TrySelectCompatibleTarget(string itemId)
	{
		foreach (EquipTarget target in new[]
		{
			EquipTarget.Helmet,
			EquipTarget.Weapon,
			EquipTarget.Armor,
			EquipTarget.Accessory,
			EquipTarget.AttributeGem,
		})
		{
			if (IsCompatibleItemForTarget(itemId, target))
			{
				_selectedTarget = target;
				return true;
			}
		}

		for (int index = 0; index < _supportButtons.Count; index++)
		{
			if (IsSupportCoreCompatible(itemId, index))
			{
				_selectedSupportIndex = index;
				_selectedTarget = EquipTarget.SupportCore;
				return true;
			}
		}

		return false;
	}

	private static bool ShouldShowItemInCategory(string itemId, InventoryCategory category)
	{
		if (category == InventoryCategory.All)
		{
			return true;
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return category == InventoryCategory.Materials;
		}

		InventoryItemKind kind = BuildCatalog.GetItemKind(itemId);
		return category switch
		{
			InventoryCategory.Equipment => kind == InventoryItemKind.Equipment,
			InventoryCategory.Gems => kind is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem,
			_ => false,
		};
	}

	private static void SortItemIds(List<string> itemIds)
	{
		itemIds.Sort((left, right) =>
		{
			int categoryCompare = GetSortCategory(left).CompareTo(GetSortCategory(right));
			return categoryCompare != 0 ? categoryCompare : string.Compare(GetInventoryItemName(left), GetInventoryItemName(right), System.StringComparison.Ordinal);
		});
	}

	private static int GetSortCategory(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return 3;
		}

		return BuildCatalog.GetItemKind(itemId) switch
		{
			InventoryItemKind.Equipment => 0,
			InventoryItemKind.AttributeGem => 1,
			InventoryItemKind.SkillGem => 2,
			_ => 9,
		};
	}

	private bool EquipItem(string itemId)
	{
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor) || !_player.HasInventoryItem(itemId))
		{
			return false;
		}

		if (!IsCompatibleItemForTarget(itemId, _selectedTarget) && !TrySelectCompatibleTarget(itemId))
		{
			RefreshAll();
			return false;
		}

		return PerformEquip(itemId);
	}

	// Applies itemId to the already-resolved _selectedTarget (+ _selectedSupportIndex),
	// consumes one from the bag, and returns whatever it displaced. No consume when the
	// slot already holds this exact item. Returns true if something was equipped.
	private bool PerformEquip(string itemId)
	{
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return false;
		}

		string displaced = GetEquippedItemId(_selectedTarget);
		if (displaced == itemId)
		{
			return true;
		}

		ApplyEquipToSelectedTarget(itemId);
		if (GetEquippedItemId(_selectedTarget) != itemId)
		{
			return false;
		}

		_player.ConsumeInventoryItemForEquip(itemId);
		_player.ReturnInventoryItemFromUnequip(displaced);
		HideItemTooltip();
		RefreshAll();
		return true;
	}

	private void ApplyEquipToSelectedTarget(string itemId)
	{
		switch (_selectedTarget)
		{
			case EquipTarget.Helmet:
				_selectedActor!.EquipBuildEquipment(EquipmentSlot.Helmet, itemId);
				break;
			case EquipTarget.Weapon:
				_selectedActor!.EquipBuildEquipment(EquipmentSlot.Weapon, itemId);
				break;
			case EquipTarget.Armor:
				_selectedActor!.EquipBuildEquipment(EquipmentSlot.Armor, itemId);
				break;
			case EquipTarget.Accessory:
				_selectedActor!.EquipBuildEquipment(EquipmentSlot.Accessory, itemId);
				break;
			case EquipTarget.AttributeGem:
				_selectedActor!.EquipAttributeGem(itemId);
				break;
			case EquipTarget.SupportCore:
				_selectedActor!.EquipSkillGem(_selectedSupportIndex, itemId);
				break;
		}
	}

	private void EquipItemToTarget(string itemId, EquipTarget target)
	{
		if (_player == null || _selectedActor == null || !IsInstanceValid(_selectedActor)
			|| !_player.HasInventoryItem(itemId) || !IsCompatibleItemForTarget(itemId, target))
		{
			return;
		}

		_selectedTarget = target;
		EquipItem(itemId);
	}

	private void OnEquipSelectedPressed()
	{
		if (string.IsNullOrEmpty(_selectedItemId))
		{
			return;
		}

		EquipItem(_selectedItemId);
	}

	private void OnUseSelectedPressed()
	{
		RefreshSelectedItemDetails();
	}

	private void RefreshDetails()
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			_companionInfoCard.SetActor(null);
			_selectedSlotLabel.Text = string.Empty;
			_buildSummaryLabel.Text = string.Empty;
			return;
		}

		_companionInfoCard.SetActor(_selectedActor);
		string coreChain = _selectedActor.SupportCoreChain;
		string elementLine = $"{LocaleText.T("build.element")} {_selectedActor.BuildElementName}";
		_buildSummaryLabel.Text = string.IsNullOrEmpty(coreChain)
			? elementLine
			: $"{elementLine}\n{LocaleText.F("build.core_chain", coreChain)}";
		_buildSummaryLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.83f, 0.90f));
		_selectedSlotLabel.Text = LocaleText.F("inventory.selected_slot", GetTargetName(_selectedTarget));
	}

	private void ShowTooltipForTarget(EquipTarget target)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return;
		}

		ShowItemTooltip(GetEquippedItemId(target), GetTargetName(target));
	}

	private string GetEquippedItemId(EquipTarget target)
	{
		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			return string.Empty;
		}

		CompanionBuildLoadout loadout = _selectedActor.BuildLoadout;
		return target switch
		{
			EquipTarget.Helmet => loadout.HelmetId,
			EquipTarget.Weapon => loadout.WeaponId,
			EquipTarget.Armor => loadout.ArmorId,
			EquipTarget.Accessory => loadout.AccessoryId,
			EquipTarget.AttributeGem => loadout.AttributeGemId,
			EquipTarget.SupportCore => loadout.GetSkillGemId(_selectedSupportIndex),
			_ => string.Empty,
		};
	}

	private void ShowItemTooltip(string itemId, string slotName)
	{
		if (string.IsNullOrEmpty(itemId))
		{
			HideItemTooltip();
			return;
		}

		_tooltip.ShowTooltip(GetInventoryItemName(itemId), BuildItemTooltipBody(itemId, slotName), this);
	}

	private void HideItemTooltip()
	{
		if (_tooltip != null)
		{
			_tooltip.HideTooltip();
		}
	}

	public static string BuildItemTooltipBody(string itemId, string slotName)
	{
		var lines = new List<string>
		{
			LocaleText.F("tooltip.meta_line", slotName, LocaleText.T(GetItemKindKey(itemId))),
		};

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			lines.Add(LocaleText.T("inventory.material_hint"));
			return FormatTooltipLines(lines);
		}

		switch (BuildCatalog.GetItemKind(itemId))
		{
			case InventoryItemKind.Equipment:
				AppendEquipmentTooltip(lines, BuildCatalog.GetEquipment(itemId));
				break;
			case InventoryItemKind.AttributeGem:
				AppendAttributeGemTooltip(lines, BuildCatalog.GetAttributeGem(itemId));
				break;
			case InventoryItemKind.SkillGem:
				AppendSkillGemTooltip(lines, BuildCatalog.GetSkillGem(itemId));
				break;
		}

		return FormatTooltipLines(lines);
	}

	private static string FormatTooltipLines(List<string> lines)
	{
		if (lines.Count <= 3)
		{
			return string.Join("\n", lines);
		}

		var compactLines = new List<string>
		{
			lines[0],
			lines[1],
		};
		for (int index = 2; index < lines.Count; index += 3)
		{
			int count = Mathf.Min(3, lines.Count - index);
			compactLines.Add(string.Join("  /  ", lines.GetRange(index, count)));
		}

		return string.Join("\n", compactLines);
	}

	private static string GetItemKindKey(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return "tooltip.type.material";
		}

		return BuildCatalog.GetItemKind(itemId) switch
		{
			InventoryItemKind.Equipment => "tooltip.type.equipment",
			InventoryItemKind.AttributeGem => "tooltip.type.attribute",
			InventoryItemKind.SkillGem => "tooltip.type.skill",
			_ => "tooltip.type.skill",
		};
	}

	private static void AppendEquipmentTooltip(List<string> lines, EquipmentDefinition item)
	{
		AddSummaryLine(lines, item.SummaryKey);
		AddStatLine(lines, "stat.health", item.MaxHealthBonus);
		AddStatLine(lines, "stat.attack", item.AttackBonus);
		AddStatLine(lines, "stat.defense", item.DefenseBonus);
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus);
		AddPercentLine(lines, "tooltip.attack_cooldown", item.AttackCooldownReduction);
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus);
		AddStatLine(lines, "tooltip.socket_count", item.SocketCount);
	}

	private static void AppendAttributeGemTooltip(List<string> lines, AttributeGemDefinition item)
	{
		AddSummaryLine(lines, item.SummaryKey);
		lines.Add(LocaleText.F("tooltip.element", LocaleText.T(item.ElementNameKey)));
		AddStatLine(lines, "stat.attack", item.AttackBonus);
		AddStatLine(lines, "stat.defense", item.DefenseBonus);
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus);
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus);
		AddPercentLine(lines, "tooltip.life_steal", item.LifeStealPercent);
		AddPercentLine(lines, "tooltip.control_chance", item.ControlChance);
		AddDecimalLine(lines, "tooltip.knockback", item.KnockbackForce);
	}

	private static void AppendSkillGemTooltip(List<string> lines, SkillGemDefinition item)
	{
		lines.Add(LocaleText.F("tooltip.gem_category", LocaleText.T(BuildCatalog.GetSkillGemCategoryKey(item.Id))));
		AddSummaryLine(lines, item.SummaryKey);
		if (BuildCatalog.IsProjectileSupportGem(item.Id))
		{
			lines.Add(LocaleText.T("tooltip.requires_ranged_skill"));
		}
		AddStatLine(lines, "stat.health", item.MaxHealthBonus);
		AddStatLine(lines, "stat.attack", item.AttackBonus);
		AddStatLine(lines, "stat.defense", item.DefenseBonus);
		AddPercentLine(lines, "tooltip.move_speed", item.MoveSpeedBonus);
		AddPercentLine(lines, "tooltip.attack_cooldown", item.AttackCooldownReduction);
		AddDecimalLine(lines, "tooltip.attack_range", item.AttackRangeBonus);
		AddDecimalLine(lines, "tooltip.detection_radius", item.DetectionRadiusBonus);
		AddPercentLine(lines, "tooltip.follow_distance", item.FollowDistanceMultiplier - 1.0f);
		AddPercentLine(lines, "tooltip.crit_chance", item.CritChanceBonus);
		AddPercentLine(lines, "tooltip.life_steal", item.LifeStealPercent);
		AddFlagLine(lines, "tooltip.enable_heal", item.EnablesHeal);
		AddFlagLine(lines, "tooltip.enable_shield", item.EnablesShield);
	}

	private static void AddSummaryLine(List<string> lines, string summaryKey)
	{
		if (!string.IsNullOrEmpty(summaryKey))
		{
			lines.Add(LocaleText.T(summaryKey));
		}
	}

	private static void AddStatLine(List<string> lines, string labelKey, int value)
	{
		if (value != 0)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(value)));
		}
	}

	private static void AddDecimalLine(List<string> lines, string labelKey, float value)
	{
		if (Mathf.Abs(value) > 0.001f)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(value, "0.0")));
		}
	}

	private static void AddPercentLine(List<string> lines, string labelKey, float value)
	{
		if (Mathf.Abs(value) > 0.001f)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), Signed(Mathf.RoundToInt(value * 100.0f)) + "%"));
		}
	}

	private static void AddFlagLine(List<string> lines, string labelKey, bool enabled)
	{
		if (enabled)
		{
			lines.Add(LocaleText.F("tooltip.stat_line", LocaleText.T(labelKey), LocaleText.T("tooltip.enabled")));
		}
	}

	private static string Signed(int value)
	{
		return value > 0 ? $"+{value}" : value.ToString();
	}

	private static string Signed(float value, string format)
	{
		return value > 0.0f ? $"+{value.ToString(format)}" : value.ToString(format);
	}

	private string GetTargetName(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.Helmet => LocaleText.T("build.slot.helmet"),
			EquipTarget.Weapon => LocaleText.T("build.slot.weapon"),
			EquipTarget.Armor => LocaleText.T("build.slot.armor"),
			EquipTarget.Accessory => LocaleText.T("build.slot.accessory"),
			EquipTarget.AttributeGem => LocaleText.T("build.slot.attribute"),
			EquipTarget.SupportCore => LocaleText.F("build.slot.support_core", _selectedSupportIndex + 1),
			_ => LocaleText.T("build.slot.attribute"),
		};
	}

	private void AddItemListMessage(string key)
	{
		var label = MakeLabel(14, new Color(0.72f, 0.78f, 0.84f));
		label.Text = LocaleText.T(key);
		label.CustomMinimumSize = new Vector2(360.0f, 48.0f);
		_itemGrid.AddChild(label);
	}

	private void RefreshText()
	{
		if (_titleLabel == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("inventory.title");
		_goldLabel.Text = LocaleText.F("inventory.gold", _player?.Gold ?? 0);
		if (_categoryButtons.TryGetValue(InventoryCategory.All, out Button? allButton))
		{
			allButton.Text = LocaleText.T("inventory.tab.all");
		}
		if (_categoryButtons.TryGetValue(InventoryCategory.Equipment, out Button? equipmentButton))
		{
			equipmentButton.Text = LocaleText.T("inventory.tab.equipment");
		}
		if (_categoryButtons.TryGetValue(InventoryCategory.Gems, out Button? gemsButton))
		{
			gemsButton.Text = LocaleText.T("inventory.tab.gems");
		}
		if (_categoryButtons.TryGetValue(InventoryCategory.Materials, out Button? materialsButton))
		{
			materialsButton.Text = LocaleText.T("inventory.tab.materials");
		}
		_equipSelectedButton.Text = LocaleText.T("inventory.action.equip");
		_useSelectedButton.Text = LocaleText.T("inventory.action.use");
	}

	private void OnClosePressed()
	{
		if (CloseRequested != null)
		{
			CloseRequested();
			return;
		}

		SetPanelVisible(false);
	}

	private void OnLanguageChanged()
	{
		bool wasVisible = Visible;
		foreach (Node child in GetChildren())
		{
			RemoveChild(child);
			child.QueueFree();
		}

		BuildPanel();
		Visible = wasVisible;
		RefreshAll();
	}

	private static void ClearChildren(Node node)
	{
		foreach (Node child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	private static Label MakeLabel(int fontSize, Color color)
	{
		var label = new Label
		{
			VerticalAlignment = VerticalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button { Text = text };
		ApplyButtonStyle(button);
		return button;
	}

	private static void ApplyButtonStyle(Button button)
	{
		button.AddThemeFontSizeOverride("font_size", 13);
	}
}
