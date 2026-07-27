using Godot;
using System.Collections.Generic;

public readonly record struct MonsterLootDefinition(string Id, string NameKey, Color DropColor);

public static class MonsterLootCatalog
{
	private static readonly MonsterLootDefinition[] QuestMaterials =
	{
		new("loot.slime_mucus", "item.loot.slime_mucus", new Color(0.32f, 0.92f, 0.78f, 0.95f)),
		new("loot.beast_hide", "item.loot.beast_hide", new Color(0.62f, 0.38f, 0.22f, 0.95f)),
		new("loot.sharp_claw", "item.loot.sharp_claw", new Color(0.92f, 0.86f, 0.64f, 0.95f)),
		new("loot.soft_fur", "item.loot.soft_fur", new Color(0.82f, 0.70f, 0.58f, 0.95f)),
		new("loot.small_bone", "item.loot.small_bone", new Color(0.86f, 0.82f, 0.72f, 0.95f)),
		new("loot.insect_wing", "item.loot.insect_wing", new Color(0.72f, 0.92f, 0.76f, 0.95f)),
		new("loot.red_horn", "item.loot.red_horn", new Color(0.88f, 0.22f, 0.14f, 0.95f)),
		new("loot.venom_sac", "item.loot.venom_sac", new Color(0.54f, 0.88f, 0.22f, 0.95f)),
		new("loot.water_core", "item.loot.water_core", new Color(0.34f, 0.72f, 1.0f, 0.95f)),
		new("loot.dragon_scale", "item.loot.dragon_scale", new Color(0.86f, 0.34f, 0.18f, 0.95f)),
		new("loot.cracked_core", "item.loot.cracked_core", new Color(0.70f, 0.68f, 0.62f, 0.95f)),

		// 強化水晶 T1~T10：所有怪物依其世界階級（WorldTier）掉落，作為精煉裝備的材料。
		new("loot.enhance_crystal.t1", "item.loot.enhance_crystal.t1", new Color(0.70f, 0.74f, 0.80f, 0.96f)),
		new("loot.enhance_crystal.t2", "item.loot.enhance_crystal.t2", new Color(0.56f, 0.82f, 0.72f, 0.96f)),
		new("loot.enhance_crystal.t3", "item.loot.enhance_crystal.t3", new Color(0.42f, 0.84f, 0.58f, 0.96f)),
		new("loot.enhance_crystal.t4", "item.loot.enhance_crystal.t4", new Color(0.50f, 0.80f, 1.00f, 0.96f)),
		new("loot.enhance_crystal.t5", "item.loot.enhance_crystal.t5", new Color(0.36f, 0.62f, 1.00f, 0.96f)),
		new("loot.enhance_crystal.t6", "item.loot.enhance_crystal.t6", new Color(0.62f, 0.48f, 1.00f, 0.96f)),
		new("loot.enhance_crystal.t7", "item.loot.enhance_crystal.t7", new Color(0.82f, 0.42f, 0.98f, 0.96f)),
		new("loot.enhance_crystal.t8", "item.loot.enhance_crystal.t8", new Color(1.00f, 0.44f, 0.72f, 0.96f)),
		new("loot.enhance_crystal.t9", "item.loot.enhance_crystal.t9", new Color(1.00f, 0.58f, 0.30f, 0.96f)),
		new("loot.enhance_crystal.t10", "item.loot.enhance_crystal.t10", new Color(1.00f, 0.86f, 0.36f, 0.98f)),
	};

	private static readonly string[] EnhanceCrystalIds =
	{
		"loot.enhance_crystal.t1", "loot.enhance_crystal.t2", "loot.enhance_crystal.t3",
		"loot.enhance_crystal.t4", "loot.enhance_crystal.t5", "loot.enhance_crystal.t6",
		"loot.enhance_crystal.t7", "loot.enhance_crystal.t8", "loot.enhance_crystal.t9",
		"loot.enhance_crystal.t10",
	};

	// 對應世界階級 / 精煉目標星等的水晶 id（tier 1~10）。
	public static string GetEnhanceCrystalId(int tier)
	{
		return EnhanceCrystalIds[Mathf.Clamp(tier, 1, EnhanceCrystalIds.Length) - 1];
	}

	public static bool IsEnhanceCrystal(string itemId)
	{
		return !string.IsNullOrEmpty(itemId) && itemId.StartsWith("loot.enhance_crystal.t", System.StringComparison.Ordinal);
	}

	public static int GetEnhanceCrystalTier(string itemId)
	{
		if (!IsEnhanceCrystal(itemId))
		{
			return 0;
		}

		string suffix = itemId.Substring("loot.enhance_crystal.t".Length);
		return int.TryParse(suffix, out int tier) ? Mathf.Clamp(tier, 1, EnhanceCrystalIds.Length) : 0;
	}

	private static readonly string[] CommonQuestMaterialIds =
	{
		"loot.slime_mucus",
		"loot.beast_hide",
		"loot.sharp_claw",
		"loot.soft_fur",
		"loot.small_bone",
		"loot.insect_wing",
		"loot.venom_sac",
		"loot.water_core",
		"loot.cracked_core",
	};

	public static IReadOnlyList<MonsterLootDefinition> Materials => QuestMaterials;

	public static bool IsMonsterLoot(string itemId)
	{
		foreach (MonsterLootDefinition material in QuestMaterials)
		{
			if (material.Id == itemId)
			{
				return true;
			}
		}

		return false;
	}

	public static string GetNameKey(string itemId)
	{
		foreach (MonsterLootDefinition material in QuestMaterials)
		{
			if (material.Id == itemId)
			{
				return material.NameKey;
			}
		}

		return itemId;
	}

	public static Color GetDropColor(string itemId)
	{
		foreach (MonsterLootDefinition material in QuestMaterials)
		{
			if (material.Id == itemId)
			{
				return material.DropColor;
			}
		}

		return new Color(0.82f, 0.92f, 1.0f, 0.95f);
	}

	public static string GetQuestItemIdForNpc(string npcDisplayName)
	{
		int index = PositiveModulo(StableStringHash(npcDisplayName), CommonQuestMaterialIds.Length);
		return CommonQuestMaterialIds[index];
	}

	public static string PickPrimaryDropForMonster(string monsterDisplayName, bool isRangedCombatant, int level)
	{
		if (monsterDisplayName.StartsWith("name.monster.", System.StringComparison.Ordinal))
		{
			return MonsterSpeciesCatalog.Current.GetPrimaryLootId(monsterDisplayName, isRangedCombatant, level);
		}

		string lowerName = monsterDisplayName.ToLowerInvariant();
		if (lowerName.Contains("slime"))
		{
			return "loot.slime_mucus";
		}

		if (lowerName.Contains("water"))
		{
			return "loot.water_core";
		}

		if (lowerName.Contains("redhorn"))
		{
			return "loot.red_horn";
		}

		if (lowerName.Contains("dragon"))
		{
			return "loot.dragon_scale";
		}

		if (lowerName.Contains("bee") || lowerName.Contains("caterpillar"))
		{
			return "loot.insect_wing";
		}

		if (lowerName.Contains("imp") || isRangedCombatant)
		{
			return "loot.venom_sac";
		}

		return level % 2 == 0 ? "loot.sharp_claw" : "loot.beast_hide";
	}

	public static string PickSecondaryDropForMonster(string primaryItemId, int level)
	{
		return primaryItemId switch
		{
			"loot.slime_mucus" => level >= 5 ? "loot.water_core" : "loot.cracked_core",
			"loot.beast_hide" => "loot.sharp_claw",
			"loot.sharp_claw" => "loot.beast_hide",
			"loot.soft_fur" => "loot.small_bone",
			"loot.small_bone" => "loot.soft_fur",
			"loot.insect_wing" => "loot.venom_sac",
			"loot.red_horn" => "loot.sharp_claw",
			"loot.venom_sac" => "loot.sharp_claw",
			"loot.water_core" => "loot.slime_mucus",
			"loot.dragon_scale" => "loot.red_horn",
			_ => "loot.cracked_core",
		};
	}

	private static int PositiveModulo(int value, int divisor)
	{
		if (divisor <= 0)
		{
			return 0;
		}

		int result = value % divisor;
		return result < 0 ? result + divisor : result;
	}

	private static int StableStringHash(string value)
	{
		unchecked
		{
			int hash = 23;
			foreach (char character in value)
			{
				hash = hash * 31 + character;
			}

			return hash;
		}
	}
}
