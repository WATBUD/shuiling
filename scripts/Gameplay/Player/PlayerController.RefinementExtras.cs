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
				&& !BuildCatalog.IsFreeItem(entry.Key)
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
	// How many upgrade/downgrade conversions the player can currently afford.
	public int MaxUpgradeUnits(int fromTier)
	{
		return fromTier < 1 || fromTier >= CoreEnhanceConfig.MaxOrbTier
			? 0
			: GetInventoryCount(MonsterLootCatalog.GetEnhanceCrystalId(fromTier)) / 10;
	}

	public int MaxDowngradeUnits(int fromTier)
	{
		return fromTier <= 1 || fromTier > CoreEnhanceConfig.MaxOrbTier
			? 0
			: GetInventoryCount(MonsterLootCatalog.GetEnhanceCrystalId(fromTier));
	}

	// Convert `units` lots of 10x T{fromTier} -> 1x T{fromTier+1}, clamped to what
	// the player can afford. Refuses (message) when nothing is affordable.
	public bool TryUpgradeCrystals(int fromTier, int units)
	{
		int actual = Mathf.Min(Mathf.Max(units, 1), MaxUpgradeUnits(fromTier));
		if (actual <= 0)
		{
			PostSystemMessage(LocaleText.T("system.exchange.insufficient"), RefineExtraBadColor, GameMessageChannel.Loot);
			return false;
		}

		string fromId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier);
		string toId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier + 1);
		TryConsumeInventoryItem(fromId, actual * 10);
		AddInventoryItemSilently(toId, actual);
		PostSystemMessage(
			LocaleText.F("system.exchange.done", $"{GetInventoryItemDisplayName(fromId)} x{actual * 10}", $"{GetInventoryItemDisplayName(toId)} x{actual}"),
			RefineExtraGoodColor,
			GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return true;
	}

	// 1x T{fromTier} -> 10x T{fromTier-1}.
	// Convert `units` lots of 1x T{fromTier} -> 10x T{fromTier-1}, clamped to stock.
	public bool TryDowngradeCrystals(int fromTier, int units)
	{
		int actual = Mathf.Min(Mathf.Max(units, 1), MaxDowngradeUnits(fromTier));
		if (actual <= 0)
		{
			PostSystemMessage(LocaleText.T("system.exchange.insufficient"), RefineExtraBadColor, GameMessageChannel.Loot);
			return false;
		}

		string fromId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier);
		string toId = MonsterLootCatalog.GetEnhanceCrystalId(fromTier - 1);
		TryConsumeInventoryItem(fromId, actual);
		AddInventoryItemSilently(toId, actual * 10);
		PostSystemMessage(
			LocaleText.F("system.exchange.done", $"{GetInventoryItemDisplayName(fromId)} x{actual}", $"{GetInventoryItemDisplayName(toId)} x{actual * 10}"),
			RefineExtraGoodColor,
			GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		_refinementPanel?.RefreshAll();
		return true;
	}
}
