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
	};

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
		string nameKey = monsterDisplayName;
		string lowerName = monsterDisplayName.ToLowerInvariant();

		if (nameKey == "name.monster.slime" || lowerName.Contains("slime") || lowerName.Contains("史萊姆"))
		{
			return "loot.slime_mucus";
		}

		if (nameKey == "name.monster.water_spirit" || lowerName.Contains("water") || lowerName.Contains("水靈"))
		{
			return "loot.water_core";
		}

		if (nameKey == "name.monster.redhorn" || lowerName.Contains("redhorn") || lowerName.Contains("紅角"))
		{
			return "loot.red_horn";
		}

		if (nameKey is "name.monster.bee" or "name.monster.caterpillar"
			|| lowerName.Contains("bee") || lowerName.Contains("caterpillar") || lowerName.Contains("毒蜂") || lowerName.Contains("毛蟲"))
		{
			return "loot.insect_wing";
		}

		if (nameKey == "name.monster.imp" || lowerName.Contains("imp") || lowerName.Contains("小鬼"))
		{
			return "loot.venom_sac";
		}

		if (nameKey == "name.monster.dragon" || lowerName.Contains("dragon") || lowerName.Contains("幼龍"))
		{
			return "loot.dragon_scale";
		}

		if (lowerName.Contains("golem") || lowerName.Contains("core") || lowerName.Contains("魔核"))
		{
			return "loot.cracked_core";
		}

		if (nameKey is "name.monster.rat" or "name.monster.bunny" or "name.monster.deer" or "name.monster.beaver"
			|| lowerName.Contains("rat") || lowerName.Contains("bunny") || lowerName.Contains("deer") || lowerName.Contains("beaver")
			|| lowerName.Contains("街鼠") || lowerName.Contains("跳兔") || lowerName.Contains("林鹿") || lowerName.Contains("河狸"))
		{
			return level >= 6 ? "loot.small_bone" : "loot.soft_fur";
		}

		if (nameKey is "name.monster.fox" or "name.monster.boar" or "name.monster.wolf" or "name.monster.lion" or "name.monster.tiger" or "name.monster.bear"
			|| lowerName.Contains("wolf") || lowerName.Contains("beast") || lowerName.Contains("fox") || lowerName.Contains("boar") || lowerName.Contains("lion") || lowerName.Contains("tiger") || lowerName.Contains("bear"))
		{
			return "loot.beast_hide";
		}

		if (nameKey is "name.monster.crab" or "name.monster.fish" or "name.monster.elephant")
		{
			return level >= 7 ? "loot.cracked_core" : "loot.small_bone";
		}

		if (isRangedCombatant)
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
