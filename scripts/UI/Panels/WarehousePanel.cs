using Godot;
using System.Collections.Generic;

// Warehouse (倉庫) — a two-column bag | storage screen like common RPG storage
// UIs. Category tabs filter both sides; double-click or middle-click an item to
// move it across (bag -> storage on the left, storage -> bag on the right).
public partial class WarehousePanel : PanelContainer
{
	private enum ItemCategory
	{
		All,
		Equipment,
		Gems,
		Materials,
		Consumables,
		Companions,
	}

	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private GridContainer _bagGrid = null!;
	private GridContainer _storageGrid = null!;
	private HBoxContainer _itemColumns = null!;
	private HBoxContainer _companionColumns = null!;
	private VBoxContainer _partyCompanionList = null!;
	private VBoxContainer _collectionCompanionList = null!;
	private FloatingTooltip _companionTooltip = null!;
	private HBoxContainer _categoryTabs = null!;
	private readonly Dictionary<ItemCategory, Button> _categoryButtons = new();
	private ItemCategory _selectedCategory = ItemCategory.All;
	private const ulong TransferDebounceMsec = 250;
	private ulong _lastTransferMsec;
	private string _lastTransferItem = string.Empty;

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
		if (_companionTooltip != null && _companionTooltip.Visible)
		{
			_companionTooltip.PositionNearMouse(this);
		}
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (_companionTooltip == null || !_companionTooltip.Visible
			|| inputEvent is not InputEventMouseButton { Pressed: true } mouseButton)
		{
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.WheelUp)
		{
			_companionTooltip.ScrollDetail(-48);
			GetViewport().SetInputAsHandled();
		}
		else if (mouseButton.ButtonIndex == MouseButton.WheelDown)
		{
			_companionTooltip.ScrollDetail(48);
			GetViewport().SetInputAsHandled();
		}
	}

	public void Bind(PlayerController player)
	{
		_player = player;
		RefreshAll();
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
		if (!visible)
		{
			_companionTooltip?.HideTooltip();
		}
		if (visible)
		{
			_selectedCategory = ItemCategory.All;
			RefreshAll();
		}
	}

	private void BuildPanel()
	{
		Name = "WarehousePanel";
		Visible = false;
		AnchorLeft = 0.5f;
		AnchorRight = 0.5f;
		AnchorTop = 0.5f;
		AnchorBottom = 0.5f;
		OffsetLeft = -380.0f;
		OffsetRight = 380.0f;
		OffsetTop = -285.0f;
		OffsetBottom = 285.0f;

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.05f, 0.07f, 0.09f, 0.96f),
			BorderColor = new Color(0.62f, 0.82f, 1.0f, 0.72f),
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

		_titleLabel = new Label { Text = LocaleText.T("warehouse.title"), HorizontalAlignment = HorizontalAlignment.Center };
		_titleLabel.AddThemeFontSizeOverride("font_size", 24);
		_titleLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.78f));
		root.AddChild(_titleLabel);

		_hintLabel = new Label { Text = LocaleText.T("warehouse.hint"), HorizontalAlignment = HorizontalAlignment.Center };
		_hintLabel.AddThemeFontSizeOverride("font_size", 13);
		_hintLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.78f, 0.9f));
		root.AddChild(_hintLabel);

		_categoryTabs = new HBoxContainer();
		_categoryTabs.AddThemeConstantOverride("separation", 6);
		root.AddChild(_categoryTabs);
		AddCategoryButton(ItemCategory.All, "inventory.tab.all");
		AddCategoryButton(ItemCategory.Equipment, "inventory.tab.equipment");
		AddCategoryButton(ItemCategory.Gems, "inventory.tab.gems");
		AddCategoryButton(ItemCategory.Materials, "inventory.tab.materials");
		AddCategoryButton(ItemCategory.Consumables, "inventory.tab.consumables");
		AddCategoryButton(ItemCategory.Companions, "warehouse.companions");

		_itemColumns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		_itemColumns.AddThemeConstantOverride("separation", 14);
		root.AddChild(_itemColumns);
		_bagGrid = CreateColumn(_itemColumns, "warehouse.bag");
		_storageGrid = CreateColumn(_itemColumns, "warehouse.storage");

		_companionColumns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill, Visible = false };
		_companionColumns.AddThemeConstantOverride("separation", 14);
		root.AddChild(_companionColumns);
		_partyCompanionList = CreateCompanionColumn(_companionColumns, "warehouse.party_companions");
		_collectionCompanionList = CreateCompanionColumn(_companionColumns, "warehouse.collection");

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);

		_companionTooltip = new FloatingTooltip
		{
			Name = "WarehouseCompanionTooltip",
			MaxWidthRatio = 0.42f,
			MaxWidth = 460.0f,
			MinWidth = 260.0f,
		};
		AddChild(_companionTooltip);
	}

	private VBoxContainer CreateCompanionColumn(HBoxContainer parent, string titleKey)
	{
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 6);
		parent.AddChild(column);

		var header = new Label { Text = LocaleText.T(titleKey), HorizontalAlignment = HorizontalAlignment.Center };
		header.AddThemeFontSizeOverride("font_size", 18);
		header.AddThemeColorOverride("font_color", new Color(0.72f, 0.92f, 1.0f));
		column.AddChild(header);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(340.0f, 380.0f),
		};
		column.AddChild(scroll);
		var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		list.AddThemeConstantOverride("separation", 7);
		scroll.AddChild(list);
		return list;
	}

	private GridContainer CreateColumn(HBoxContainer parent, string titleKey)
	{
		var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		column.AddThemeConstantOverride("separation", 6);
		parent.AddChild(column);

		var header = new Label { Text = LocaleText.T(titleKey), HorizontalAlignment = HorizontalAlignment.Center };
		header.AddThemeFontSizeOverride("font_size", 18);
		header.AddThemeColorOverride("font_color", new Color(0.72f, 0.92f, 1.0f));
		column.AddChild(header);

		var scroll = new ScrollContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(340.0f, 380.0f),
		};
		column.AddChild(scroll);

		var grid = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
		grid.AddThemeConstantOverride("h_separation", ItemIconLibrary.InventoryGridGap);
		grid.AddThemeConstantOverride("v_separation", ItemIconLibrary.InventoryGridGap);
		scroll.AddChild(grid);
		scroll.Resized += () => ItemIconLibrary.UpdateResponsiveGridColumns(grid, scroll);
		ItemIconLibrary.UpdateResponsiveGridColumns(grid, scroll);
		return grid;
	}

	private void AddCategoryButton(ItemCategory category, string labelKey)
	{
		var button = new Button
		{
			Text = LocaleText.T(labelKey),
			ToggleMode = true,
			CustomMinimumSize = new Vector2(0.0f, 32.0f),
		};
		button.Pressed += () => SelectCategory(category);
		_categoryTabs.AddChild(button);
		_categoryButtons[category] = button;
	}

	private void SelectCategory(ItemCategory category)
	{
		_selectedCategory = category;
		RefreshAll();
	}

	public void RefreshAll()
	{
		if (_bagGrid == null || _storageGrid == null)
		{
			return;
		}

		_titleLabel.Text = LocaleText.T("warehouse.title");
		bool companionMode = _selectedCategory == ItemCategory.Companions;
		_hintLabel.Text = LocaleText.T(companionMode ? "warehouse.companion_hint" : "warehouse.hint");
		_itemColumns.Visible = !companionMode;
		_companionColumns.Visible = companionMode;
		foreach (KeyValuePair<ItemCategory, Button> pair in _categoryButtons)
		{
			pair.Value.ButtonPressed = pair.Key == _selectedCategory;
		}

		ClearChildren(_bagGrid);
		ClearChildren(_storageGrid);
		_companionTooltip?.HideTooltip();
		ClearChildren(_partyCompanionList);
		ClearChildren(_collectionCompanionList);
		if (_player == null)
		{
			return;
		}

		if (companionMode)
		{
			RefreshCompanions();
			return;
		}

		foreach (string itemId in SortedFiltered(_player.InventoryItems))
		{
			_bagGrid.AddChild(MakeItemButton(itemId, _player.GetInventoryCount(itemId), true));
		}

		foreach (string itemId in SortedFiltered(_player.StorageItems))
		{
			_storageGrid.AddChild(MakeItemButton(itemId, _player.GetStorageCount(itemId), false));
		}
	}

	private void RefreshCompanions()
	{
		if (_player == null)
		{
			return;
		}

		int partyCount = 0;
		int collectionCount = 0;
		foreach (SimpleActor actor in _player.CapturedCollection)
		{
			if (!IsInstanceValid(actor) || !actor.IsCaptured)
			{
				continue;
			}

			if (actor.IsInWarehouseCollection)
			{
				_collectionCompanionList.AddChild(MakeCompanionRow(actor, false));
				collectionCount++;
			}
			else if (!actor.IsDefeated && !actor.IsAwaitingRecovery)
			{
				_partyCompanionList.AddChild(MakeCompanionRow(actor, true));
				partyCount++;
			}
		}

		if (partyCount == 0)
		{
			_partyCompanionList.AddChild(MakeEmptyLabel("warehouse.no_party_companions"));
		}
		if (collectionCount == 0)
		{
			_collectionCompanionList.AddChild(MakeEmptyLabel("warehouse.no_collection_companions"));
		}
	}

	private Control MakeCompanionRow(SimpleActor actor, bool deposit)
	{
		var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		row.AddThemeConstantOverride("separation", 8);
		var info = new Label
		{
			Text = $"{actor.LocalizedDisplayName}  Lv.{actor.Level}",
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Stop,
			MouseDefaultCursorShape = CursorShape.PointingHand,
		};
		info.AddThemeFontSizeOverride("font_size", 15);
		info.AddThemeColorOverride("font_color", new Color(0.9f, 0.94f, 1.0f));
		row.AddChild(info);
		row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

		bool blocked = deposit && (_player == null || _player.IsMountedCompanion(actor));
		var action = new Button
		{
			Text = LocaleText.T(deposit ? "warehouse.deposit_companion" : "warehouse.withdraw_companion"),
			CustomMinimumSize = new Vector2(92.0f, 44.0f),
			Disabled = blocked,
			TooltipText = blocked ? LocaleText.T("warehouse.mounted_blocked") : string.Empty,
		};
		info.MouseEntered += () => ShowCompanionTooltip(actor);
		info.MouseExited += () => _companionTooltip.HideTooltip();
		action.Pressed += () =>
		{
			if (_player == null)
			{
				return;
			}
			if (deposit)
			{
				_player.WarehouseDepositCompanion(actor);
			}
			else
			{
				_player.WarehouseWithdrawCompanion(actor);
			}
			RefreshAll();
		};
		row.AddChild(action);
		return row;
	}

	private void ShowCompanionTooltip(SimpleActor actor)
	{
		if (!IsInstanceValid(actor))
		{
			_companionTooltip.HideTooltip();
			return;
		}

		_companionTooltip.ShowTooltip(
			$"{actor.LocalizedDisplayName} - {LocaleText.F("inventory.info_header", actor.Level)}",
			BuildCompanionTooltipBody(actor),
			this);
	}

	private static string BuildCompanionTooltipBody(SimpleActor actor)
	{
		BuildStats stats = actor.CurrentBuildStats;
		float attackSpeed = 1.0f / Mathf.Max(actor.EffectiveAttackCooldown, 0.01f);
		string race = LocaleText.T(BuildCatalog.GetRaceNameKey(BuildCatalog.GetRaceId(actor)));
		var sections = new List<string>
		{
			$"{LocaleText.T("stat.experience")} {actor.Experience}/{actor.ExperienceToNextLevel}",
			string.Join("\n",
				LocaleText.F("stat.health_value", actor.CurrentHealth, actor.EffectiveMaxHealth),
				$"{LocaleText.T("stat.attack")} {actor.EffectiveAttack}",
				$"{LocaleText.T("stat.defense")} {actor.EffectiveDefense}",
				$"{LocaleText.T("stat.move_speed")} {actor.EffectiveMoveSpeed:0.0}",
				LocaleText.F("stat.attack_speed_value", attackSpeed.ToString("0.00")),
				$"{LocaleText.T("tooltip.attack_range")} {actor.EffectiveAttackRange:0.0}",
				$"{LocaleText.T("tooltip.detection_radius")} {actor.EffectiveDetectionRadius:0.0}",
				$"{LocaleText.T("tooltip.crit_chance")} {stats.CritChance * 100.0f:0.#}%",
				$"{LocaleText.T("tooltip.life_steal")} {stats.LifeStealPercent * 100.0f:0.#}%",
				$"{LocaleText.T("tooltip.control_chance")} {stats.ControlChance * 100.0f:0.#}%"),
			string.Join("\n",
				$"{LocaleText.T("stat.race")} {race} / {LocaleText.T("stat.element")} {actor.BuildElementName}",
				$"{LocaleText.T("stat.growth")} {actor.GrowthName}",
				$"{LocaleText.T("stat.affinity")} {actor.Affinity} / 100",
				$"{LocaleText.T("stat.mood")}：{actor.MoodName}",
				$"{LocaleText.T("build.slot.attack_mode")}: {actor.AttackModeName}"),
		};

		if (!string.IsNullOrEmpty(actor.TraitSummary))
		{
			sections.Add($"【{LocaleText.T("build.traits")}】\n{actor.TraitSummary}");
		}
		if (!string.IsNullOrEmpty(actor.BuildEquipmentSummary))
		{
			sections.Add($"【{LocaleText.T("build.equipment")}】\n{actor.BuildEquipmentSummary}");
		}
		if (!string.IsNullOrEmpty(actor.BuildSkillSummary))
		{
			sections.Add($"【{LocaleText.T("build.skill_gems")}】\n{actor.BuildSkillSummary}");
		}
		if (!string.IsNullOrEmpty(actor.FormationBonusSummary))
		{
			sections.Add(LocaleText.F("formation.bonus.active", actor.FormationBonusSummary));
		}

		return string.Join("\n", sections);
	}

	private static Label MakeEmptyLabel(string key)
	{
		var label = new Label
		{
			Text = LocaleText.T(key),
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		label.AddThemeFontSizeOverride("font_size", 14);
		label.AddThemeColorOverride("font_color", new Color(0.58f, 0.66f, 0.76f));
		return label;
	}

	private List<string> SortedFiltered(IReadOnlyDictionary<string, int> source)
	{
		var ids = new List<string>();
		foreach (KeyValuePair<string, int> entry in source)
		{
			if (entry.Value > 0 && !BuildCatalog.IsFreeItem(entry.Key) && MatchesCategory(entry.Key))
			{
				ids.Add(entry.Key);
			}
		}

		ids.Sort((a, b) => string.Compare(GetItemName(a), GetItemName(b), System.StringComparison.CurrentCulture));
		return ids;
	}

	private bool MatchesCategory(string itemId)
	{
		if (_selectedCategory == ItemCategory.All)
		{
			return true;
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return _selectedCategory == ItemCategory.Materials;
		}

		return _selectedCategory switch
		{
			ItemCategory.Equipment => BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Equipment,
			ItemCategory.Gems => BuildCatalog.GetItemKind(itemId) is InventoryItemKind.AttributeGem or InventoryItemKind.SkillGem,
			ItemCategory.Consumables => BuildCatalog.GetItemKind(itemId) == InventoryItemKind.Consumable,
			_ => false,
		};
	}

	private Button MakeItemButton(string itemId, int count, bool inBag)
	{
		var button = new Button
		{
			Text = string.Empty,
			CustomMinimumSize = new Vector2(ItemIconLibrary.InventorySlotWidth, 58.0f),
			ClipText = true,
			TooltipText = count > 1
				? $"{InventoryPanel.BuildItemTooltipTitle(itemId)} x{count}"
				: InventoryPanel.BuildItemTooltipTitle(itemId),
		};
		ItemIconLibrary.Apply(button, itemId, 42);
		button.IconAlignment = HorizontalAlignment.Center;
		ItemIconLibrary.AddStackCountBadge(button, count);

		// Double-click or middle-click transfers one across.
		button.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { Pressed: true } mouse
				&& ((mouse.ButtonIndex == MouseButton.Left && mouse.DoubleClick)
					|| mouse.ButtonIndex == MouseButton.Middle))
			{
				Transfer(itemId, inBag);
				button.AcceptEvent();
			}
		};
		return button;
	}

	private void Transfer(string itemId, bool fromBag)
	{
		if (_player == null)
		{
			return;
		}

		// Debounce only a repeated click on the SAME item: transferring the whole
		// stack empties it, so a fast echo on that now-gone slot would land on a
		// re-sorted neighbour. Consecutive transfers of DIFFERENT items must always
		// go through, otherwise depositing several items in a row silently drops all
		// but the first.
		ulong now = Time.GetTicksMsec();
		if (itemId == _lastTransferItem && now - _lastTransferMsec < TransferDebounceMsec)
		{
			return;
		}
		_lastTransferMsec = now;
		_lastTransferItem = itemId;

		// Move the whole stack in one action (deterministic; no need to spam).
		if (fromBag)
		{
			_player.WarehouseDeposit(itemId, int.MaxValue);
		}
		else
		{
			_player.WarehouseWithdraw(itemId, int.MaxValue);
		}

		RefreshAll();
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
