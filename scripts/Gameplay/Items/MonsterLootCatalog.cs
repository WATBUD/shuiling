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
		new("loot.red_horn", "item.loot.red_horn", new Color(0.88f, 0.22f, 0.14f, 0.95f)),
		new("loot.venom_sac", "item.loot.venom_sac", new Color(0.54f, 0.88f, 0.22f, 0.95f)),
		new("loot.water_core", "item.loot.water_core", new Color(0.34f, 0.72f, 1.0f, 0.95f)),
		new("loot.dragon_scale", "item.loot.dragon_scale", new Color(0.86f, 0.34f, 0.18f, 0.95f)),
		new("loot.cracked_core", "item.loot.cracked_core", new Color(0.70f, 0.68f, 0.62f, 0.95f)),
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
		int index = PositiveModulo(StableStringHash(npcDisplayName), QuestMaterials.Length);
		return QuestMaterials[index].Id;
	}

	public static string PickPrimaryDropForMonster(string monsterDisplayName, bool isRangedCombatant, int level)
	{
		string lowerName = monsterDisplayName.ToLowerInvariant();
		if (lowerName.Contains("slime") || lowerName.Contains("史萊姆"))
		{
			return "loot.slime_mucus";
		}

		if (lowerName.Contains("water") || lowerName.Contains("水靈"))
		{
			return "loot.water_core";
		}

		if (lowerName.Contains("redhorn") || lowerName.Contains("紅角"))
		{
			return "loot.red_horn";
		}

		if (lowerName.Contains("imp") || lowerName.Contains("毒牙") || isRangedCombatant)
		{
			return "loot.venom_sac";
		}

		if (lowerName.Contains("dragon") || lowerName.Contains("龍") || level >= 8)
		{
			return "loot.dragon_scale";
		}

		if (lowerName.Contains("golem") || lowerName.Contains("core") || lowerName.Contains("晶"))
		{
			return "loot.cracked_core";
		}

		if (lowerName.Contains("wolf") || lowerName.Contains("beast") || lowerName.Contains("狼") || lowerName.Contains("獸"))
		{
			return "loot.beast_hide";
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
