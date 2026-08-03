using Godot;
using System.Collections.Generic;

// Gacha merchant: pay gold per draw, roll a tier via a top-down cascade, then a
// random category (equipment / weapon / core / refinement material). The tier
// becomes the reward's star level (equipment/weapon/core) or the crystal tier
// (material). Batch draws stop early if gold runs out.
public partial class PlayerController
{
	private enum GachaCategory
	{
		Equipment,
		Weapon,
		Core,
		Material,
	}

	private readonly RandomNumberGenerator _gachaRng = new();

	// Per-save gacha merchant progression. The merchant starts at level 1; drawing
	// feeds it EXP (scaled by the drawn star) until it levels up, which raises the
	// draw cap the player may select. Persisted via SaveGameData.
	private int _gachaMerchantLevel = GachaConfig.MerchantStartLevel;
	private int _gachaMerchantExp;

	public int GachaMerchantLevel => _gachaMerchantLevel;
	public int GachaMerchantExp => _gachaMerchantExp;
	public int GachaMerchantExpToNext => GachaConfig.ExpToLevel(_gachaMerchantLevel);
	public bool GachaMerchantMaxed => _gachaMerchantLevel >= GachaConfig.MerchantMaxLevel;

	// Highest cap the player may currently draw at (merchant level + 1).
	public int GachaUnlockedMaxTier => GachaConfig.UnlockedMaxTier(_gachaMerchantLevel);

	// Gold cost of one draw at the given cap (clamped to what's unlocked).
	public int GachaDrawCost(int tierCap)
	{
		return GachaConfig.DrawCost(ClampGachaTierCap(tierCap));
	}

	private int ClampGachaTierCap(int tierCap)
	{
		return Mathf.Clamp(tierCap, 1, GachaUnlockedMaxTier);
	}

	// Load-time restore; clamps level to the valid range and drops leftover EXP that
	// exceeds the (possibly changed) threshold so a maxed merchant reads as 0 EXP.
	public void SetGachaMerchantProgress(int level, int exp)
	{
		_gachaMerchantLevel = Mathf.Clamp(level, GachaConfig.MerchantStartLevel, GachaConfig.MerchantMaxLevel);
		int toNext = GachaConfig.ExpToLevel(_gachaMerchantLevel);
		_gachaMerchantExp = toNext <= 0 ? 0 : Mathf.Clamp(exp, 0, toNext - 1);
	}

	private void AddGachaMerchantExp(int amount)
	{
		if (amount <= 0 || GachaMerchantMaxed)
		{
			return;
		}

		_gachaMerchantExp += amount;
		while (!GachaMerchantMaxed && _gachaMerchantExp >= GachaConfig.ExpToLevel(_gachaMerchantLevel))
		{
			_gachaMerchantExp -= GachaConfig.ExpToLevel(_gachaMerchantLevel);
			_gachaMerchantLevel++;
			PostSystemMessage(
				LocaleText.F("gacha.merchant_level_up", _gachaMerchantLevel, GachaUnlockedMaxTier),
				new Color(0.7f, 1.0f, 0.78f),
				GameMessageChannel.Loot);
		}

		if (GachaMerchantMaxed)
		{
			_gachaMerchantExp = 0;
		}
	}

	private int RollGachaTier(int maxTier)
	{
		for (int tier = Mathf.Clamp(maxTier, 1, GachaConfig.MaxTier); tier >= 1; tier--)
		{
			if (_gachaRng.Randf() * 100.0f < GachaConfig.TierStopPercent[tier])
			{
				return tier;
			}
		}

		return 1;
	}

	private string RollGachaItem(int tier)
	{
		var category = (GachaCategory)_gachaRng.RandiRange(0, 3);
		switch (category)
		{
			case GachaCategory.Material:
				return MonsterLootCatalog.GetEnhanceCrystalId(tier);
			case GachaCategory.Weapon:
			{
				var weapons = new List<string>();
				foreach (EquipmentDefinition weapon in BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Weapon))
				{
					// Skip the ".none" empty-slot placeholder, else the player can
					// draw a "未裝備" weapon.
					if (!BuildCatalog.IsFreeItem(weapon.Id))
					{
						weapons.Add(weapon.Id);
					}
				}

				return weapons.Count == 0
					? MonsterLootCatalog.GetEnhanceCrystalId(tier)
					: BuildCatalog.MakeRefinedEquipmentId(weapons[_gachaRng.RandiRange(0, weapons.Count - 1)], tier);
			}
			case GachaCategory.Core:
			{
				var cores = new List<string>();
				foreach (SkillGemDefinition gem in BuildCatalog.GetSkillGemDefinitions())
				{
					if (!BuildCatalog.IsFreeItem(gem.Id) && !BuildCatalog.IsRetiredSkillCore(gem.Id))
					{
						cores.Add(gem.Id);
					}
				}

				return cores.Count == 0
					? MonsterLootCatalog.GetEnhanceCrystalId(tier)
					: BuildCatalog.MakeStarredSkillCoreId(cores[_gachaRng.RandiRange(0, cores.Count - 1)], tier);
			}
			default:
			{
				var gear = new List<string>();
				foreach (EquipmentSlot slot in new[] { EquipmentSlot.Helmet, EquipmentSlot.Armor, EquipmentSlot.Boots, EquipmentSlot.Accessory })
				{
					foreach (EquipmentDefinition equipment in BuildCatalog.GetEquipmentDefinitions(slot))
					{
						// Skip ".none" empty-slot placeholders so no "未裝備" gear drops.
						if (!BuildCatalog.IsFreeItem(equipment.Id))
						{
							gear.Add(equipment.Id);
						}
					}
				}

				return gear.Count == 0
					? MonsterLootCatalog.GetEnhanceCrystalId(tier)
					: BuildCatalog.MakeRefinedEquipmentId(gear[_gachaRng.RandiRange(0, gear.Count - 1)], tier);
			}
		}
	}

	// Performs up to `count` draws at the given cap tier, stopping when gold runs
	// out. Each draw grants merchant EXP scaled by the star it yielded. Returns the
	// drawn item ids (with their star/tier applied) for the panel to show.
	public List<string> DrawGacha(int count, int tierCap)
	{
		var results = new List<string>();
		count = Mathf.Clamp(count, 1, 100);
		tierCap = ClampGachaTierCap(tierCap);
		int cost = GachaConfig.DrawCost(tierCap);

		if (Gold < cost)
		{
			PostSystemMessage(LocaleText.T("gacha.not_enough_gold"), new Color(1.0f, 0.62f, 0.48f), GameMessageChannel.Loot);
			return results;
		}

		int affordable = Mathf.Min(count, Gold / cost);
		int earnedExp = 0;
		for (int i = 0; i < affordable; i++)
		{
			Gold -= cost;
			int tier = RollGachaTier(tierCap);
			string itemId = RollGachaItem(tier);
			AddInventoryItemSilently(itemId, 1);
			results.Add(itemId);
			earnedExp += GachaConfig.DrawExp(tier);
		}

		if (results.Count > 0)
		{
			PostSystemMessage(
				LocaleText.F("gacha.result_summary", results.Count, results.Count * cost),
				new Color(1.0f, 0.86f, 0.4f),
				GameMessageChannel.Loot);
			AddGachaMerchantExp(earnedExp);
			_inventoryPanel?.RefreshAll();
		}

		return results;
	}
}
