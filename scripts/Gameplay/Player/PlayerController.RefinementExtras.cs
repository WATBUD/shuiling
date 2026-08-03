using Godot;
using System.Collections.Generic;

// Extra refinement-merchant services: dismantle a starred equipment back into a
// share of its enhance crystals, and exchange enhance crystals between adjacent
// tiers at 10:1 (10x Tn -> 1x T(n+1), reversible).
public partial class PlayerController
{
	private static readonly Color RefineExtraGoodColor = new(0.7f, 1.0f, 0.78f);
	private static readonly Color RefineExtraBadColor = new(1.0f, 0.62f, 0.48f);

	// Starred bag equipment eligible for dismantle (0-star gear yields nothing,
	// so it is excluded).
	public List<string> GetDismantlableEquipmentIds()
	{
		var ids = new List<string>();
		foreach (KeyValuePair<string, int> entry in _inventoryItems)
		{
			if (entry.Value > 0
				&& BuildCatalog.GetItemKind(entry.Key) == InventoryItemKind.Equipment
				&& BuildCatalog.GetEquipmentStars(entry.Key) >= 1)
			{
				ids.Add(entry.Key);
			}
		}

		ids.Sort(System.StringComparer.Ordinal);
		return ids;
	}

	// Crystals returned by dismantling: 50% of the count that this tier's refine
	// step consumed (a T-star step costs T crystals of tier T).
	public int GetEquipmentDismantleYield(string itemId)
	{
		int stars = BuildCatalog.GetEquipmentStars(itemId);
		return Mathf.FloorToInt(stars * 0.5f);
	}

	public bool TryDismantleEquipment(string itemId)
	{
		int stars = BuildCatalog.GetEquipmentStars(itemId);
		if (GetInventoryCount(itemId) <= 0
			|| BuildCatalog.GetItemKind(itemId) != InventoryItemKind.Equipment
			|| stars < 1)
		{
			return false;
		}

		int yield = Mathf.FloorToInt(stars * 0.5f);
		string crystalId = MonsterLootCatalog.GetEnhanceCrystalId(stars);

		RemoveInventoryItemSilently(itemId, 1);
		if (yield > 0)
		{
			AddInventoryItemSilently(crystalId, yield);
		}

		PostSystemMessage(
			LocaleText.F("system.dismantle.done", GetInventoryItemDisplayName(itemId), GetInventoryItemDisplayName(crystalId), yield),
			RefineExtraGoodColor,
			GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return true;
	}

	// 10x T{fromTier} -> 1x T{fromTier+1}. Unit of 10; short amounts are refused.
	public bool TryUpgradeCrystals(int fromTier)
	{
		if (fromTier < 1 || fromTier >= CoreEnhanceConfig.MaxOrbTier)
		{
			return false;
		}

		string fromId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier);
		string toId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier + 1);
		if (GetInventoryCount(fromId) < 10)
		{
			PostSystemMessage(LocaleText.T("system.exchange.insufficient"), RefineExtraBadColor, GameMessageChannel.Loot);
			return false;
		}

		TryConsumeInventoryItem(fromId, 10);
		AddInventoryItemSilently(toId, 1);
		PostSystemMessage(
			LocaleText.F("system.exchange.done", $"{GetInventoryItemDisplayName(fromId)} x10", $"{GetInventoryItemDisplayName(toId)} x1"),
			RefineExtraGoodColor,
			GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return true;
	}

	// 1x T{fromTier} -> 10x T{fromTier-1}.
	public bool TryDowngradeCrystals(int fromTier)
	{
		if (fromTier <= 1 || fromTier > CoreEnhanceConfig.MaxOrbTier)
		{
			return false;
		}

		string fromId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier);
		string toId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier - 1);
		if (GetInventoryCount(fromId) < 1)
		{
			PostSystemMessage(LocaleText.T("system.exchange.insufficient"), RefineExtraBadColor, GameMessageChannel.Loot);
			return false;
		}

		TryConsumeInventoryItem(fromId, 1);
		AddInventoryItemSilently(toId, 10);
		PostSystemMessage(
			LocaleText.F("system.exchange.done", $"{GetInventoryItemDisplayName(fromId)} x1", $"{GetInventoryItemDisplayName(toId)} x10"),
			RefineExtraGoodColor,
			GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return true;
	}
}
