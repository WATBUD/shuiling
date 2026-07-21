using Godot;

// World Tier rules (docs/world_progression.md): every map supports Tiers 1-10.
// Map = biome/ecosystem, Tier = difficulty. Tier scales monster level, stats,
// AI ranges, rewards and drives the per-tier "evolution stage" display name.
public static class WorldTierCatalog
{
	public const int MinTier = 1;
	public const int MaxTier = 10;

	// Evolution stage name keys, formatted with the base species name as {0}
	// (e.g. "Elite {0}" / "精英{0}"). Index = tier - 1.
	private static readonly string[] StageNameKeys =
	{
		"tier.stage.1",
		"tier.stage.2",
		"tier.stage.3",
		"tier.stage.4",
		"tier.stage.5",
		"tier.stage.6",
		"tier.stage.7",
		"tier.stage.8",
		"tier.stage.9",
		"tier.stage.10",
	};

	public static int ClampTier(int tier)
	{
		return Mathf.Clamp(tier, MinTier, MaxTier);
	}

	// Monster level band per tier: T1 = 2-10, each tier shifts the band up by 8.
	public static (int Min, int Max) GetMonsterLevelRange(int tier)
	{
		tier = ClampTier(tier);
		return (2 + (tier - 1) * 8, 10 + (tier - 1) * 8);
	}

	// Player level required to enter a tier. T1 is always open (1); higher tiers
	// gate a little below the band's floor so you can enter once you're roughly
	// on par with its monsters. Shown with a lock icon when unmet.
	public static int GetRequiredPlayerLevel(int tier)
	{
		tier = ClampTier(tier);
		if (tier <= MinTier)
		{
			return 1;
		}

		(int min, _) = GetMonsterLevelRange(tier);
		return Mathf.Max(min - 2, 1);
	}

	// Multiplier applied on top of the level-based stat curve so higher tiers
	// feel meaningfully tougher, not just higher-leveled.
	public static float GetStatMultiplier(int tier)
	{
		return 1.0f + 0.15f * (ClampTier(tier) - 1);
	}

	// XP / gold reward multiplier — higher tiers must pay better.
	public static float GetRewardMultiplier(int tier)
	{
		return 1.0f + 0.25f * (ClampTier(tier) - 1);
	}

	// Bosses get an explicit level bonus (their stats are hand-authored, not
	// level-derived) plus a steeper stat multiplier than regular monsters.
	public static int GetBossLevelBonus(int tier)
	{
		return (ClampTier(tier) - 1) * 8;
	}

	public static float GetBossStatMultiplier(int tier)
	{
		return 1.0f + 0.35f * (ClampTier(tier) - 1);
	}

	// Gentle body-size growth per tier ("larger body size" evolution cue).
	public static float GetMonsterVisualScale(int tier)
	{
		return Mathf.Min(1.0f + 0.05f * (ClampTier(tier) - 1), 1.45f);
	}

	// AI sharpening per tier: wider awareness, longer chases, faster swings.
	public static float GetDetectionRadiusBonus(int tier)
	{
		return (ClampTier(tier) - 1) * 0.8f;
	}

	public static float GetChaseRadiusBonus(int tier)
	{
		return (ClampTier(tier) - 1) * 1.0f;
	}

	public static float GetAttackCooldownFactor(int tier)
	{
		return Mathf.Max(1.0f - 0.03f * (ClampTier(tier) - 1), 0.7f);
	}

	public static string GetStageNameKey(int tier)
	{
		return StageNameKeys[ClampTier(tier) - 1];
	}

	// "Young Wolf" / "幼年野狼" — species base name wrapped in the tier stage.
	public static string FormatMonsterName(int tier, string localizedBaseName)
	{
		return LocaleText.F(GetStageNameKey(tier), localizedBaseName);
	}
}
