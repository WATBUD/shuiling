using Godot;
using System.Collections.Generic;

public partial class InventoryPanel : PanelContainer
{
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

	private void RefreshCompanionList()
	{
		ClearChildren(_companionList);
		if (_player == null)
		{
			return;
		}

		var playerButton = MakeButton($"玩家  {_player.PlayerName}");
		playerButton.Alignment = HorizontalAlignment.Left;
		playerButton.CustomMinimumSize = new Vector2(0.0f, 42.0f);
		playerButton.AddThemeColorOverride("font_color", _selectingPlayer ? new Color(1.0f, 0.94f, 0.62f) : new Color(0.92f, 0.96f, 1.0f));
		playerButton.Pressed += SelectPlayer;
		_companionList.AddChild(playerButton);

		foreach (SimpleActor actor in _player.ActiveParty)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !actor.IsAwaitingRecovery)
			{
				AddCompanionButton(actor, LocaleText.T("party.active"));
			}
		}

		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (IsInstanceValid(actor) && actor.IsCaptured && !actor.IsAwaitingRecovery && !_player.IsInActiveParty(actor))
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
		if (_selectingPlayer && _player != null)
		{
			CompanionBuildLoadout playerLoadout = _player.BuildLoadout;
			_helmetButton.Visible = true;
			_weaponButton.Visible = true;
			_armorButton.Visible = true;
			_bootsButton.Visible = true;
			_attributeButton.Visible = true;
			SetSlotsDisabled(false);
			SetSlotButton(_helmetButton, EquipTarget.Helmet, playerLoadout.HelmetId, BuildCatalog.GetEquipment(playerLoadout.HelmetId).NameKey);
			SetSlotButton(_weaponButton, EquipTarget.Weapon, playerLoadout.WeaponId, BuildCatalog.GetEquipment(playerLoadout.WeaponId).NameKey);
			SetSlotButton(_armorButton, EquipTarget.Armor, playerLoadout.ArmorId, BuildCatalog.GetEquipment(playerLoadout.ArmorId).NameKey);
			SetSlotButton(_bootsButton, EquipTarget.Boots, playerLoadout.BootsId, BuildCatalog.GetEquipment(playerLoadout.BootsId).NameKey);
			for (int index = 0; index < _accessoryButtons.Count; index++)
			{
				_accessoryButtons[index].Visible = true;
				SetAccessorySlotButton(_accessoryButtons[index], index, playerLoadout);
			}

			SetSlotButton(_attributeButton, EquipTarget.AttributeGem, playerLoadout.AttributeGemId, BuildCatalog.GetAttributeGem(playerLoadout.AttributeGemId).NameKey);
			int unlocked = BuildCatalog.GetUnlockedSupportCoreCount(_player.Level);
			int playerVisibleSupport = Mathf.Min(Mathf.Max(unlocked + 1, 2), _supportButtons.Count);
			for (int index = 0; index < _supportButtons.Count; index++)
			{
				_supportButtons[index].Visible = index < playerVisibleSupport;
				if (_supportButtons[index].Visible)
				{
					SetSupportSlotButton(_supportButtons[index], index, playerLoadout);
				}
			}
			return;
		}

		_helmetButton.Visible = true;
		_weaponButton.Visible = true;
		_armorButton.Visible = true;
		_bootsButton.Visible = true;
		foreach (Button accessoryButton in _accessoryButtons)
		{
			accessoryButton.Visible = true;
		}

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
		SetSlotButton(_bootsButton, EquipTarget.Boots, loadout.BootsId, BuildCatalog.GetEquipment(loadout.BootsId).NameKey);
		for (int index = 0; index < _accessoryButtons.Count; index++)
		{
			SetAccessorySlotButton(_accessoryButtons[index], index, loadout);
		}

		SetSlotButton(_attributeButton, EquipTarget.AttributeGem, loadout.AttributeGemId, BuildCatalog.GetAttributeGem(loadout.AttributeGemId).NameKey);

		// Show the unlocked support cores plus one locked preview of the next slot; the
		// rest stay hidden until the creature grows into them.
		int unlockedSupport = BuildCatalog.GetUnlockedSupportCoreCount(_selectedActor.Level);
		// Keep support core 1 as a locked placeholder so the three-column equipment
		// layout is stable and boots always remain directly below armor.
		int visibleSupport = Mathf.Min(Mathf.Max(unlockedSupport + 1, 2), _supportButtons.Count);
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
		string coreName = SupportSlotName(index);
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

	private void SetAccessorySlotButton(Button button, int index, CompanionBuildLoadout loadout)
	{
		string ringId = loadout.GetAccessoryId(index);
		button.Text = $"{LocaleText.F("build.slot.accessory_n", index + 1)}\n{LocaleText.T(BuildCatalog.GetEquipment(ringId).NameKey)}";
		ItemIconLibrary.Apply(button, ringId, 26);
		bool selected = _selectedTarget == EquipTarget.Accessory && _selectedAccessoryIndex == index;
		button.AddThemeColorOverride("font_color", selected ? new Color(1.0f, 0.92f, 0.50f) : new Color(0.92f, 0.96f, 1.0f));
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
		var buttons = new List<Button> { _helmetButton, _weaponButton, _armorButton, _bootsButton, _attributeButton };
		buttons.AddRange(_accessoryButtons);
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

		// Count reflects the currently-selected tab, not the whole bag.
		_bagCountLabel.Text = LocaleText.F("inventory.bag_count", itemIds.Count);
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
		var button = new InventoryItemDragButton
		{
			Text = string.Empty,
			DragItemId = BuildCatalog.GetItemKind(itemId) is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem
				? itemId
				: string.Empty,
		};
		ApplyButtonStyle(button);
		button.CustomMinimumSize = new Vector2(ItemIconLibrary.InventorySlotWidth, 58.0f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
		button.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
		button.Alignment = HorizontalAlignment.Center;
		button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
		ItemIconLibrary.Apply(button, itemId, 42);
		button.IconAlignment = HorizontalAlignment.Center;
		if (!BuildCatalog.IsFreeItem(itemId))
		{
			ItemIconLibrary.AddStackCountBadge(button, count);
		}
		// Bag items do not need a redundant "Bag Items" source label in their
		// tooltip; the concrete item type is enough.
		button.MouseEntered += () => ShowItemTooltip(itemId, string.Empty);
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

	private void RefreshCategoryButtons()
	{
		foreach (KeyValuePair<InventoryCategory, Button> entry in _categoryButtons)
		{
			entry.Value.ButtonPressed = entry.Key == _selectedCategory;
			entry.Value.AddThemeColorOverride("font_color", entry.Key == _selectedCategory ? new Color(1.0f, 0.92f, 0.54f) : new Color(0.86f, 0.91f, 0.96f));
		}
	}

	private void RefreshSortDirectionButton()
	{
		_sortDirectionButton.Text = _sortAscending ? "↑" : "↓";
		_sortDirectionButton.TooltipText = LocaleText.T(_sortAscending
			? "inventory.sort.ascending"
			: "inventory.sort.descending");
	}

	private void RefreshUpgradeButton()
	{
		int slot = SelectedSkillSlotIndex();
		if (_player == null || GetSelectedLoadout() == null || slot < 0 || !IsSlotUnlocked(_selectedTarget))
		{
			_upgradeSkillGemButton.Visible = false;
			return;
		}

		CompanionBuildLoadout loadout = GetSelectedLoadout()!;
		string gemId = loadout.GetSkillGemId(slot);
		if (!BuildCatalog.IsUpgradeableSkillGem(gemId))
		{
			_upgradeSkillGemButton.Visible = false;
			return;
		}

		_upgradeSkillGemButton.Visible = true;
		SkillGemUpgradeCost? upgradeCost = _selectingPlayer
			? _player.GetPlayerSkillGemUpgradeCost(slot)
			: _player.GetCompanionSkillGemUpgradeCost(_selectedActor!, slot);
		if (upgradeCost is not SkillGemUpgradeCost cost)
		{
			_upgradeSkillGemButton.Text = LocaleText.F("inventory.action.gem_maxed", loadout.GetSkillGemLevel(slot));
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

	private void RefreshSelectedItemDetails()
	{
		RefreshUpgradeButton();
		if (_player == null || string.IsNullOrEmpty(_selectedItemId) || !_player.HasInventoryItem(_selectedItemId))
		{
			_selectedItemId = string.Empty;
			_equipSelectedButton.Disabled = true;
			_useSelectedButton.Disabled = true;
			return;
		}

		_equipSelectedButton.Disabled = !CanEquipSelectedItem();
		_useSelectedButton.Disabled = BuildCatalog.GetItemKind(_selectedItemId) != InventoryItemKind.Consumable;
	}

	private void RefreshDetails()
	{
		if (_selectingPlayer && _player != null)
		{
			_companionInfoCard.SetPlayer(_player);
			_buildSummaryLabel.Text = LocaleText.F("build.core_chain", BuildCatalog.LocalizedSkillGems(_player.BuildLoadout));
			_buildSummaryLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.83f, 0.90f));
			return;
		}

		if (_selectedActor == null || !IsInstanceValid(_selectedActor))
		{
			_companionInfoCard.SetActor(null);
			_buildSummaryLabel.Text = string.Empty;
			return;
		}

		_companionInfoCard.SetActor(_selectedActor);
		string coreChain = _selectedActor.SupportCoreChain;
		_buildSummaryLabel.Text = LocaleText.F(
			"build.core_chain",
			string.IsNullOrEmpty(coreChain) ? LocaleText.T("gem.skill.none") : coreChain);
		_buildSummaryLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.83f, 0.90f));
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
		_sortLabel.Text = LocaleText.T("inventory.sort.label");
		_sortOption.SetItemText((int)InventorySortMode.Category, LocaleText.T("inventory.sort.category"));
		_sortOption.SetItemText((int)InventorySortMode.Name, LocaleText.T("inventory.sort.name"));
		_sortOption.SetItemText((int)InventorySortMode.Quantity, LocaleText.T("inventory.sort.quantity"));
		RefreshSortDirectionButton();
		_equipSelectedButton.Text = LocaleText.T("inventory.action.equip");
		_useSelectedButton.Text = LocaleText.T("inventory.action.use");
	}
}
