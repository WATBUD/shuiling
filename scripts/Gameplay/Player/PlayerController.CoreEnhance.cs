using Godot;
using System.Collections.Generic;

// Core-enhancement merchant logic: star-enhance unequipped skill cores using
// element orbs + gold, and dismantle collected pets into element orbs.
// UI lives in CoreEnhancerPanel; this file is the pure model + mutations.
public partial class PlayerController : CharacterBody3D
{
	public readonly record struct CoreEnhanceQuote(
		string ItemId,
		string BaseId,
		int CurrentStars,
		int TargetStars,
		string OrbId,
		int OrbCount,
		int OrbHave,
		int Gold,
		bool IsMax);

	private static readonly Color CoreEnhanceGoodColor = new(1.0f, 0.86f, 0.4f);
	private static readonly Color CoreEnhanceBadColor = new(1.0f, 0.55f, 0.45f);

	// Unequipped, non-free skill cores in the bag that the merchant can enhance.
	public List<string> GetEnhanceableCoreIds()
	{
		var result = new List<string>();
		foreach (KeyValuePair<string, int> entry in _inventoryItems)
		{
			if (entry.Value <= 0)
			{
				continue;
			}

			if (BuildCatalog.IsFreeItem(entry.Key) || BuildCatalog.GetItemKind(entry.Key) != InventoryItemKind.SkillGem)
			{
				continue;
			}

			result.Add(entry.Key);
		}

		return result;
	}

	public CoreEnhanceQuote GetCoreEnhanceQuote(string itemId)
	{
		string baseId = BuildCatalog.GetBaseSkillCoreId(itemId);
		int current = BuildCatalog.GetSkillCoreStars(itemId);
		int target = current + 1;
		bool isMax = current >= CoreEnhanceConfig.MaxCoreStars;
		string element = BuildCatalog.GetSkillGem(baseId).DamageElementId;
		int tier = CoreEnhanceConfig.OrbTierForStar(target);
		string orbId = MonsterLootCatalog.GetCoreOrbId(element, tier);
		int orbCount = CoreEnhanceConfig.OrbsToReachStar(target);
		int gold = CoreEnhanceConfig.EnhanceGold(target);
		return new CoreEnhanceQuote(itemId, baseId, current, target, orbId, orbCount, GetInventoryCount(orbId), gold, isMax);
	}

	public bool CanAffordCoreEnhance(CoreEnhanceQuote quote)
	{
		return !quote.IsMax
			&& Gold >= quote.Gold
			&& GetInventoryCount(quote.OrbId) >= quote.OrbCount
			&& GetInventoryCount(quote.ItemId) > 0;
	}

	// Consumes orbs + gold and swaps the bag core for its next-star id.
	public bool TryEnhanceCore(string itemId)
	{
		if (GetInventoryCount(itemId) <= 0)
		{
			return false;
		}

		CoreEnhanceQuote quote = GetCoreEnhanceQuote(itemId);
		if (quote.IsMax)
		{
			PostSystemMessage(LocaleText.T("core_enhance.max"), CoreEnhanceBadColor);
			return false;
		}

		if (!CanAffordCoreEnhance(quote))
		{
			PostSystemMessage(LocaleText.T("core_enhance.cannot_afford"), CoreEnhanceBadColor);
			return false;
		}

		Gold -= quote.Gold;
		TryConsumeInventoryItem(quote.OrbId, quote.OrbCount);
		RemoveInventoryItemSilently(itemId, 1);
		string upgraded = BuildCatalog.MakeStarredSkillCoreId(quote.BaseId, quote.TargetStars);
		AddInventoryItemSilently(upgraded, 1);

		string coreName = LocaleText.T(BuildCatalog.GetItemNameKey(quote.BaseId));
		PostSystemMessage(LocaleText.F("core_enhance.success", coreName, quote.TargetStars), CoreEnhanceGoodColor, GameMessageChannel.Loot);
		_inventoryPanel?.RefreshAll();
		return true;
	}

	// Dismantles collected pets into element orbs (element from innate affinity,
	// tier from level, count = base + rarity). Returns how many were dismantled.
	public int DismantleCompanions(IEnumerable<SimpleActor> actors)
	{
		int dismantled = 0;
		int totalOrbs = 0;
		string lastOrbId = string.Empty;

		foreach (SimpleActor actor in new List<SimpleActor>(actors))
		{
			if (!IsInstanceValid(actor) || !_capturedCollection.Contains(actor))
			{
				continue;
			}

			string element = BuildCatalog.GetIdentity(actor).ElementAffinityId; // empty -> universal orb
			int tier = CoreEnhanceConfig.TierForLevel(actor.Level);
			int count = CoreEnhanceConfig.DismantleOrbBase + Mathf.Max(actor.Rarity, 0);
			string orbId = MonsterLootCatalog.GetCoreOrbId(element, tier);

			StoreCompanion(actor); // detach from party/mount/formation (keeps in collection)
			_capturedCollection.Remove(actor);
			actor.QueueFree();

			AddInventoryItem(orbId, count);
			totalOrbs += count;
			lastOrbId = orbId;
			dismantled++;
		}

		if (dismantled > 0)
		{
			ReassignFollowSlots();
			RecalculateFormationBonuses();
			string orbName = string.IsNullOrEmpty(lastOrbId) ? string.Empty : MonsterLootCatalog.GetCoreOrbDisplayName(lastOrbId);
			PostSystemMessage(LocaleText.F("core_enhance.dismantled", dismantled, totalOrbs, orbName), CoreEnhanceGoodColor, GameMessageChannel.Loot);
			_partyPanel?.RefreshParty();
			_warehousePanel?.RefreshAll();
			_formationPanel?.RefreshAll();
			_inventoryPanel?.RefreshAll();
			SaveCurrentGame();
		}

		return dismantled;
	}
}
