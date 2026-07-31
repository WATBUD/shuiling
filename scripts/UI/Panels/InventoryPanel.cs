using Godot;
using System;
using System.Collections.Generic;

// Drag/drop buttons moved to InventoryDragButtons.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

public partial class InventoryPanel : PanelContainer
{
	private enum EquipTarget
	{
		Helmet,
		Weapon,
		Armor,
		Boots,
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
		Consumables,
	}

	private enum InventorySortMode
	{
		Category,
		Name,
		Quantity,
	}

	private PlayerController? _player;
	private SimpleActor? _selectedActor;
	private bool _selectingPlayer;
	private EquipTarget _selectedTarget = EquipTarget.Weapon;
	private int _selectedSupportIndex;
	private InventoryCategory _selectedCategory = InventoryCategory.All;
	private InventorySortMode _selectedSortMode = InventorySortMode.Quantity;
	private bool _sortAscending;
	private string _selectedItemId = string.Empty;
	private readonly Dictionary<InventoryCategory, Button> _categoryButtons = new();
	private VBoxContainer _companionList = null!;
	private ScrollContainer _itemScroll = null!;
	private GridContainer _itemGrid = null!;
	private Label _titleLabel = null!;
	private Label _goldLabel = null!;
	private Label _buildSummaryLabel = null!;
	private CompanionInfoCard _companionInfoCard = null!;
	private Label _bagCountLabel = null!;
	private Label _sortLabel = null!;
	private OptionButton _sortOption = null!;
	private Button _sortDirectionButton = null!;
	private Label _itemDetailTitleLabel = null!;
	private Label _itemDetailBodyLabel = null!;
	private Button _equipSelectedButton = null!;
	private Button _useSelectedButton = null!;
	private Button _upgradeSkillGemButton = null!;
	private Button _helmetButton = null!;
	private Button _weaponButton = null!;
	private Button _armorButton = null!;
	private Button _bootsButton = null!;
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
			// Inventory equipment is player-first. Previously the panel silently
			// selected the first companion, so double-clicking Gravity Shoes could
			// equip the companion while the player kept the base jump power.
			_selectedActor = null;
			_selectingPlayer = true;
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
		_selectingPlayer = false;
		RefreshAll();
	}

	private void SelectPlayer()
	{
		_selectedActor = null;
		_selectingPlayer = true;
		RefreshAll();
	}

	// Item list / slot / detail refresh moved to InventoryPanel.Refresh.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Panel layout / builders moved to InventoryPanel.Layout.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// UI factories moved to InventoryPanel.Factories.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private void SelectSupportSlot(int index)
	{
		_selectedSupportIndex = index;
		SelectTarget(EquipTarget.SupportCore);
	}

	private void ShowSupportTooltip(int index)
	{
		CompanionBuildLoadout? loadout = GetSelectedLoadout();
		if (loadout == null)
		{
			return;
		}

		ShowItemTooltip(loadout.GetSkillGemId(index), SupportSlotName(index));
	}

	// Skill-core slot 0 is the single core-skill slot; the rest are support cores.
	private static string SupportSlotName(int index)
	{
		return index == 0
			? LocaleText.T("build.slot.main_core")
			: LocaleText.T("build.slot.support_core_plain");
	}

	private void EquipSupportCore(string itemId, int index)
	{
		if (_player == null || GetSelectedLoadout() == null
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
		CompanionBuildLoadout? loadout = GetSelectedLoadout();
		if (loadout == null)
		{
			return;
		}

		string displaced = loadout.GetSkillGemId(index);
		if (displaced == "gem.skill.none")
		{
			return;
		}

		// Slot 0 is always the core-skill slot, regardless of the core's skill type.
		// Removing it must leave an empty main slot; support cores never promote into it.
		bool isPrimary = index == 0;
		if (_selectingPlayer)
		{
			_player?.ClearSkillGemSlot(index);
		}
		else
		{
			_selectedActor!.ClearSkillGemSlot(index);
			if (!isPrimary)
			{
				_selectedActor.CompactSupportCores();
			}
		}

		_player?.ReturnInventoryItemFromUnequip(displaced);
		HideItemTooltip();
		RefreshAll();
	}

	private bool IsSupportSlotUnlocked(int index)
	{
		if (_selectingPlayer)
		{
			return BuildCatalog.GetUnlockedSupportCoreCount(_player?.Level ?? 1) > index;
		}
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

		if (index == 0)
		{
			return BuildCatalog.IsMainAttackCore(itemId);
		}

		CompanionBuildLoadout? loadout = GetSelectedLoadout();
		return BuildCatalog.IsSupportCore(itemId)
			&& loadout != null
			&& BuildCatalog.HasMainAttackCore(loadout)
			&& !(BuildCatalog.IsProjectileSupportGem(itemId)
				&& !BuildCatalog.HasProjectileActiveSkill(loadout));
	}

	// Double-clicking an equipped slot takes the item off and returns it to the bag
	// (equipping consumed it, so unequipping must give it back).
	private void UnequipSlot(EquipTarget target)
	{
		if (GetSelectedLoadout() == null)
		{
			return;
		}

		string displaced = GetEquippedItemId(target);
		switch (target)
		{
			case EquipTarget.Helmet:
			case EquipTarget.Weapon:
			case EquipTarget.Armor:
			case EquipTarget.Boots:
			case EquipTarget.Accessory:
				EquipmentSlot slot = ToEquipmentSlot(target);
				if (_selectingPlayer) _player?.EquipBuildEquipment(slot, GetEmptyEquipmentId(slot));
				else _selectedActor!.EquipBuildEquipment(slot, GetEmptyEquipmentId(slot));
				break;
			case EquipTarget.AttributeGem:
				if (_selectingPlayer) _player?.EquipAttributeGem("gem.attribute.none");
				else _selectedActor!.EquipAttributeGem("gem.attribute.none");
				break;
			case EquipTarget.SupportCore:
				if (_selectingPlayer) _player?.ClearSkillGemSlot(_selectedSupportIndex);
				else _selectedActor!.EquipSkillGem(_selectedSupportIndex, "gem.skill.none");
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
			EquipTarget.Boots => EquipmentSlot.Boots,
			_ => EquipmentSlot.Accessory,
		};
	}

	private void SelectDefaultActor()
	{
		if (_player == null)
		{
			return;
		}

		if (_selectingPlayer || (_selectedActor != null && IsInstanceValid(_selectedActor) && _selectedActor.IsCaptured && !_selectedActor.IsAwaitingRecovery))
		{
			return;
		}

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !actor.IsAwaitingRecovery)
			{
				_selectedActor = actor;
				return;
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !actor.IsAwaitingRecovery)
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

	// Core slots unlock with the creature's level. A locked slot shows the level it
	// needs and cannot hold a core yet.
	private bool IsSlotUnlocked(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.AttributeGem => BuildCatalog.IsMainCoreUnlocked(GetSelectedLevel()),
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

	private void UpdateResponsiveItemColumns()
	{
		if (_itemGrid == null || _itemScroll == null)
		{
			return;
		}

		ItemIconLibrary.UpdateResponsiveGridColumns(_itemGrid, _itemScroll);
	}

	// Double-clicking a bag item equips attack cores to slot 0 and extension cores to
	// the first free slot in the support-only area (slots 1..N).
	// If no slot accepts it, warn and leave the item alone.
	private void OnItemActivated(string itemId)
	{
		_selectedItemId = itemId;
		if (_player == null || !_player.HasInventoryItem(itemId))
		{
			return;
		}

		// Consumables are used on double-click instead of equipped.
		if (BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Consumable)
		{
			UseConsumable(itemId);
			return;
		}

		if (GetSelectedLoadout() == null)
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
		CompanionBuildLoadout? selectedLoadout = GetSelectedLoadout();
		if (MonsterLootCatalog.IsMonsterLoot(itemId) || selectedLoadout == null)
		{
			return false;
		}

		switch (BuildCatalog.GetItemKind(itemId))
		{
			case InventoryItemKind.Equipment:
				_selectedTarget = EquipTargetForSlot(BuildCatalog.GetEquipment(itemId).Slot);
				return true;
			case InventoryItemKind.AttributeGem:
				if (!BuildCatalog.IsMainCoreUnlocked(GetSelectedLevel()))
				{
					reasonKey = "inventory.warn.core_locked";
					return false;
				}

				_selectedTarget = EquipTarget.AttributeGem;
				return true;
			case InventoryItemKind.SkillGem:
				bool isMainCore = BuildCatalog.IsMainAttackCore(itemId);
				if (!isMainCore && !BuildCatalog.HasMainAttackCore(selectedLoadout))
				{
					reasonKey = "tooltip.requires_main_core";
					return false;
				}
				if (BuildCatalog.IsProjectileSupportGem(itemId) && !BuildCatalog.HasProjectileActiveSkill(selectedLoadout))
				{
					reasonKey = "tooltip.requires_ranged_skill";
					return false;
				}

				if (BuildCatalog.GetUnlockedSupportCoreCount(GetSelectedLevel()) <= 0)
				{
					reasonKey = "inventory.warn.core_locked";
					return false;
				}

				int open = isMainCore ? 0 : FindFirstOpenSupportSlot(selectedLoadout);
				if (open < 0)
				{
					reasonKey = "inventory.warn.not_equippable";
					return false;
				}
				_selectedSupportIndex = open;
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
			EquipmentSlot.Boots => EquipTarget.Boots,
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
		int unlocked = BuildCatalog.GetUnlockedSupportCoreCount(GetSelectedLevel());
		for (int index = 1; index < unlocked && index < loadout.SkillGemIds.Length; index++)
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
			EquipmentSlot.Boots => "equip.boots.none",
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

	private void OnSortModeSelected(long itemIndex)
	{
		if (_sortOption == null || itemIndex < 0 || itemIndex >= _sortOption.ItemCount)
		{
			return;
		}

		InventorySortMode nextMode = (InventorySortMode)_sortOption.GetItemId((int)itemIndex);
		if (_selectedSortMode != nextMode)
		{
			_selectedSortMode = nextMode;
			// RPG inventory convention: names/types read forward, while quantities
			// start with the largest stack. The arrow remains available to reverse it.
			_sortAscending = nextMode != InventorySortMode.Quantity;
			RefreshSortDirectionButton();
		}
		RefreshItemList();
	}

	private void ToggleSortDirection()
	{
		_sortAscending = !_sortAscending;
		RefreshSortDirectionButton();
		RefreshItemList();
	}

	private int SelectedSkillSlotIndex()
	{
		return _selectedTarget == EquipTarget.SupportCore ? _selectedSupportIndex : -1;
	}

	private void OnUpgradeSkillGemPressed()
	{
		int slot = SelectedSkillSlotIndex();
		if (_player == null || GetSelectedLoadout() == null || slot < 0)
		{
			return;
		}

		bool upgraded = _selectingPlayer
			? _player.TryUpgradePlayerSkillGem(slot)
			: _player.TryUpgradeCompanionSkillGem(_selectedActor!, slot);
		if (upgraded)
		{
			RefreshAll();
		}
	}

	private bool CanEquipSelectedItem()
	{
		return GetSelectedLoadout() != null
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
		CompanionBuildLoadout? loadout = GetSelectedLoadout();
		if (kind == InventoryItemKind.SkillGem
			&& BuildCatalog.IsProjectileSupportGem(itemId)
			&& (loadout == null || !BuildCatalog.HasProjectileActiveSkill(loadout)))
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
			case EquipTarget.Boots:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Boots;
			case EquipTarget.Accessory:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment && BuildCatalog.GetEquipment(itemId).Slot == EquipmentSlot.Accessory;
			case EquipTarget.AttributeGem:
				return BuildCatalog.GetItemKind(itemId) == InventoryItemKind.AttributeGem;
			case EquipTarget.SupportCore:
				return kind == InventoryItemKind.SkillGem && IsSupportCoreCompatible(itemId, _selectedSupportIndex);
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
			EquipTarget.Boots,
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

	// Item sorting moved to InventoryPanel.Sorting.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private bool EquipItem(string itemId)
	{
		if (_player == null || GetSelectedLoadout() == null || !_player.HasInventoryItem(itemId))
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
		if (_player == null || GetSelectedLoadout() == null)
		{
			return false;
		}

		string displaced = GetEquippedItemId(_selectedTarget);
		if (displaced == itemId)
		{
			return true;
		}

		var cascadedProjectileSupports = new List<string>();
		if (_selectedTarget == EquipTarget.SupportCore
			&& _selectedSupportIndex == 0
			&& BuildCatalog.IsMainAttackCore(itemId)
			&& !BuildCatalog.IsProjectileActiveSkillGem(itemId))
		{
			CompanionBuildLoadout loadout = GetSelectedLoadout()!;
			for (int index = 1; index < loadout.SkillGemIds.Length; index++)
			{
				string supportId = loadout.GetSkillGemId(index);
				if (BuildCatalog.IsProjectileSupportGem(supportId))
				{
					cascadedProjectileSupports.Add(supportId);
				}
			}
		}

		ApplyEquipToSelectedTarget(itemId);
		if (GetEquippedItemId(_selectedTarget) != itemId)
		{
			return false;
		}

		_player.ConsumeInventoryItemForEquip(itemId);
		_player.ReturnInventoryItemFromUnequip(displaced);
		foreach (string supportId in cascadedProjectileSupports)
		{
			_player.ReturnInventoryItemFromUnequip(supportId);
		}
		HideItemTooltip();
		RefreshAll();
		return true;
	}

	private void ApplyEquipToSelectedTarget(string itemId)
	{
		if (_selectingPlayer && _player != null)
		{
			switch (_selectedTarget)
			{
				case EquipTarget.Helmet:
					_player.EquipBuildEquipment(EquipmentSlot.Helmet, itemId);
					break;
				case EquipTarget.Weapon:
					_player.EquipBuildEquipment(EquipmentSlot.Weapon, itemId);
					break;
				case EquipTarget.Armor:
					_player.EquipBuildEquipment(EquipmentSlot.Armor, itemId);
					break;
				case EquipTarget.Boots:
					_player.EquipBuildEquipment(EquipmentSlot.Boots, itemId);
					break;
				case EquipTarget.Accessory:
					_player.EquipBuildEquipment(EquipmentSlot.Accessory, itemId);
					break;
				case EquipTarget.AttributeGem:
					_player.EquipAttributeGem(itemId);
					break;
				case EquipTarget.SupportCore:
					_player.EquipSkillGem(_selectedSupportIndex, itemId);
					break;
			}
			return;
		}

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
			case EquipTarget.Boots:
				_selectedActor!.EquipBuildEquipment(EquipmentSlot.Boots, itemId);
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
		if (_player == null || GetSelectedLoadout() == null
			|| !_player.HasInventoryItem(itemId) || !IsCompatibleItemForTarget(itemId, target))
		{
			return;
		}

		_selectedTarget = target;
		EquipItem(itemId);
	}

	private CompanionBuildLoadout? GetSelectedLoadout()
	{
		if (_selectingPlayer)
		{
			return _player?.BuildLoadout;
		}
		return _selectedActor != null && IsInstanceValid(_selectedActor) ? _selectedActor.BuildLoadout : null;
	}

	private int GetSelectedLevel()
	{
		return _selectingPlayer ? _player?.Level ?? 1 : _selectedActor?.Level ?? 1;
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
		if (_player != null && !string.IsNullOrEmpty(_selectedItemId)
			&& BuildCatalog.GetItemKind(_selectedItemId) == InventoryItemKind.Consumable)
		{
			UseConsumable(_selectedItemId);
		}

		RefreshSelectedItemDetails();
	}

	private void UseConsumable(string itemId)
	{
		if (_player == null)
		{
			return;
		}

		if (itemId == BuildCatalog.TownPortalScrollId)
		{
			_player.UseTownPortalScroll();
		}

		RefreshAll();
	}

	private void ShowTooltipForTarget(EquipTarget target)
	{
		if (GetSelectedLoadout() == null)
		{
			return;
		}

		ShowItemTooltip(GetEquippedItemId(target), GetTargetName(target));
	}

	private string GetEquippedItemId(EquipTarget target)
	{
		CompanionBuildLoadout? loadout = GetSelectedLoadout();
		if (loadout == null)
		{
			return string.Empty;
		}
		return target switch
		{
			EquipTarget.Helmet => loadout.HelmetId,
			EquipTarget.Weapon => loadout.WeaponId,
			EquipTarget.Armor => loadout.ArmorId,
			EquipTarget.Boots => loadout.BootsId,
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

		_tooltip.ShowTooltip(BuildItemTooltipTitle(itemId), BuildItemTooltipBody(itemId, slotName), this);
	}

	private void HideItemTooltip()
	{
		if (_tooltip != null)
		{
			_tooltip.HideTooltip();
		}
	}

	// Item tooltip formatting moved to InventoryPanel.Tooltips.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private string GetTargetName(EquipTarget target)
	{
		return target switch
		{
			EquipTarget.Helmet => LocaleText.T("build.slot.helmet"),
			EquipTarget.Weapon => LocaleText.T("build.slot.weapon"),
			EquipTarget.Armor => LocaleText.T("build.slot.armor"),
			EquipTarget.Boots => LocaleText.T("build.slot.boots"),
			EquipTarget.Accessory => LocaleText.T("build.slot.accessory"),
			EquipTarget.AttributeGem => LocaleText.T("build.slot.attribute"),
			EquipTarget.SupportCore => SupportSlotName(_selectedSupportIndex),
			_ => LocaleText.T("build.slot.attribute"),
		};
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

	// UI factories moved to InventoryPanel.Factories.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).
}
