using Godot;
using System;
using System.Collections.Generic;

public partial class InventoryPanel : PanelContainer
{
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
			InventoryCategory.Consumables => kind == InventoryItemKind.Consumable,
			_ => false,
		};
	}

	private void SortItemIds(List<string> itemIds)
	{
		itemIds.Sort((left, right) =>
		{
			int primaryCompare = _selectedSortMode switch
			{
				InventorySortMode.Name => CompareItemNames(left, right),
				InventorySortMode.Quantity => CompareItemQuantities(left, right),
				_ => CompareItemCategories(left, right),
			};
			int result = primaryCompare != 0 ? primaryCompare : CompareItemStable(left, right);
			return _sortAscending ? result : -result;
		});
	}

	private int CompareItemQuantities(string left, string right)
	{
		int leftCount = _player?.GetInventoryCount(left) ?? 0;
		int rightCount = _player?.GetInventoryCount(right) ?? 0;
		int quantityCompare = leftCount.CompareTo(rightCount);
		return quantityCompare != 0 ? quantityCompare : CompareItemCategories(left, right);
	}

	private static int CompareItemCategories(string left, string right)
	{
		int categoryCompare = GetSortCategory(left).CompareTo(GetSortCategory(right));
		if (categoryCompare != 0)
		{
			return categoryCompare;
		}

		int subcategoryCompare = GetSortSubcategory(left).CompareTo(GetSortSubcategory(right));
		return subcategoryCompare != 0 ? subcategoryCompare : CompareItemNames(left, right);
	}

	private static int CompareItemNames(string left, string right)
	{
		return string.Compare(GetInventoryItemName(left), GetInventoryItemName(right), StringComparison.CurrentCulture);
	}

	private static int CompareItemStable(string left, string right)
	{
		int categoryCompare = CompareItemCategories(left, right);
		return categoryCompare != 0 ? categoryCompare : string.Compare(left, right, StringComparison.Ordinal);
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
			InventoryItemKind.SkillGem when BuildCatalog.IsMainAttackCore(itemId) => 1,
			InventoryItemKind.SkillGem => 2,
			InventoryItemKind.AttributeGem => 2,
			InventoryItemKind.Consumable => 4,
			_ => 9,
		};
	}

	private static int GetSortSubcategory(string itemId)
	{
		if (MonsterLootCatalog.IsMonsterLoot(itemId) || BuildCatalog.GetItemKind(itemId) != InventoryItemKind.Equipment)
		{
			return 0;
		}

		return BuildCatalog.GetEquipment(itemId).Slot switch
		{
			EquipmentSlot.Helmet => 0,
			EquipmentSlot.Weapon => 1,
			EquipmentSlot.Armor => 2,
			EquipmentSlot.Boots => 3,
			EquipmentSlot.Accessory => 4,
			_ => 9,
		};
	}
}
