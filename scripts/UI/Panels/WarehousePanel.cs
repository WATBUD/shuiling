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
	}

	private PlayerController? _player;
	private Label _titleLabel = null!;
	private Label _hintLabel = null!;
	private GridContainer _bagGrid = null!;
	private GridContainer _storageGrid = null!;
	private HBoxContainer _categoryTabs = null!;
	private readonly Dictionary<ItemCategory, Button> _categoryButtons = new();
	private ItemCategory _selectedCategory = ItemCategory.All;
	private const ulong TransferDebounceMsec = 250;
	private ulong _lastTransferMsec;

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

	public void Bind(PlayerController player)
	{
		_player = player;
		RefreshAll();
	}

	public void SetPanelVisible(bool visible)
	{
		Visible = visible;
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

		var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		columns.AddThemeConstantOverride("separation", 14);
		root.AddChild(columns);
		_bagGrid = CreateColumn(columns, "warehouse.bag");
		_storageGrid = CreateColumn(columns, "warehouse.storage");

		var closeButton = new Button { Text = LocaleText.T("dialog.button.close"), CustomMinimumSize = new Vector2(0.0f, 40.0f) };
		closeButton.Pressed += () => CloseRequested?.Invoke();
		root.AddChild(closeButton);
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

		var grid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		grid.AddThemeConstantOverride("h_separation", 4);
		grid.AddThemeConstantOverride("v_separation", 4);
		scroll.AddChild(grid);
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
		_hintLabel.Text = LocaleText.T("warehouse.hint");
		foreach (KeyValuePair<ItemCategory, Button> pair in _categoryButtons)
		{
			pair.Value.ButtonPressed = pair.Key == _selectedCategory;
		}

		ClearChildren(_bagGrid);
		ClearChildren(_storageGrid);
		if (_player == null)
		{
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
			Text = count > 1 ? $"x{count}" : string.Empty,
			CustomMinimumSize = new Vector2(64.0f, 68.0f),
			ClipText = true,
			TooltipText = count > 1 ? $"{GetItemName(itemId)} x{count}" : GetItemName(itemId),
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		ItemIconLibrary.Apply(button, itemId, 44);

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

		// Debounce: rapid clicks rebuild+re-sort the grids, so a fast second
		// click would land on a different item. Ignore clicks that arrive too
		// soon after the last transfer.
		ulong now = Time.GetTicksMsec();
		if (now - _lastTransferMsec < TransferDebounceMsec)
		{
			return;
		}
		_lastTransferMsec = now;

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
