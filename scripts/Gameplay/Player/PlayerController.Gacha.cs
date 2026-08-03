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

	public int GachaDrawCost => GachaConfig.DrawCost;

	private int RollGachaTier()
	{
		for (int tier = CoreEnhanceConfig.MaxOrbTier; tier >= 1; tier--)
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
				List<EquipmentDefinition> weapons = BuildCatalog.GetEquipmentDefinitions(EquipmentSlot.Weapon);
				return weapons.Count == 0
					? MonsterLootCatalog.GetEnhanceCrystalId(tier)
					: BuildCatalog.MakeRefinedEquipmentId(weapons[_gachaRng.RandiRange(0, weapons.Count - 1)].Id, tier);
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
						gear.Add(equipment.Id);
					}
				}

				return gear.Count == 0
					? MonsterLootCatalog.GetEnhanceCrystalId(tier)
					: BuildCatalog.MakeRefinedEquipmentId(gear[_gachaRng.RandiRange(0, gear.Count - 1)], tier);
			}
		}
	}

	// Performs up to `count` draws, stopping when gold is insufficient. Returns
	// the drawn item ids (with their star/tier applied) for the panel to show.
	public List<string> DrawGacha(int count)
	{
		var results = new List<string>();
		count = Mathf.Clamp(count, 1, 100);

		if (Gold < GachaConfig.DrawCost)
		{
			PostSystemMessage(LocaleText.T("gacha.not_enough_gold"), new Color(1.0f, 0.62f, 0.48f), GameMessageChannel.Loot);
			return results;
		}

		int affordable = Mathf.Min(count, Gold / GachaConfig.DrawCost);
		for (int i = 0; i < affordable; i++)
		{
			Gold -= GachaConfig.DrawCost;
			string itemId = RollGachaItem(RollGachaTier());
			AddInventoryItemSilently(itemId, 1);
			results.Add(itemId);
		}

		if (results.Count > 0)
		{
			PostSystemMessage(
				LocaleText.F("gacha.result_summary", results.Count, results.Count * GachaConfig.DrawCost),
				new Color(1.0f, 0.86f, 0.4f),
				GameMessageChannel.Loot);
			_inventoryPanel?.RefreshAll();
		}

		return results;
	}
}
