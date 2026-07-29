using Godot;

// Wild-monster rarity. Rolled at spawn; shown in the field via nameplate colour
// + star prefix + (for elite/alpha) a glowing aura and bigger body, so players
// can spot and hunt rare spawns. Rarity boosts stats and drops, and is kept on
// the captured companion (a rare capture is a real power spike).
public static class MonsterRarity
{
	public const int Common = 0;
	public const int Rare = 1;
	public const int Elite = 2;
	public const int Alpha = 3;

	public static int Roll(RandomNumberGenerator rng)
	{
		float roll = rng.Randf();
		if (roll < MonsterConfig.AlphaSpawnChance)
		{
			return Alpha;   // 1%
		}
		if (roll < MonsterConfig.EliteOrBetterSpawnChance)
		{
			return Elite;   // 4%
		}
		if (roll < MonsterConfig.RareOrBetterSpawnChance)
		{
			return Rare;    // 13%
		}

		return Common;      // 82%
	}

	public static float StatMultiplier(int rarity) => rarity switch
	{
		Rare => MonsterConfig.RareStatMultiplier,
		Elite => MonsterConfig.EliteStatMultiplier,
		Alpha => MonsterConfig.AlphaStatMultiplier,
		_ => 1.0f,
	};

	public static float RewardMultiplier(int rarity) => rarity switch
	{
		Rare => MonsterConfig.RareRewardMultiplier,
		Elite => MonsterConfig.EliteRewardMultiplier,
		Alpha => MonsterConfig.AlphaRewardMultiplier,
		_ => 1.0f,
	};

	public static float VisualScale(int rarity) => rarity switch
	{
		Rare => MonsterConfig.RareVisualScale,
		Elite => MonsterConfig.EliteVisualScale,
		Alpha => MonsterConfig.AlphaVisualScale,
		_ => 1.0f,
	};

	public static bool HasAura(int rarity) => rarity >= Elite;

	// Nameplate + aura colour by rarity (Common is unused — status colour wins).
	public static Color Color(int rarity) => rarity switch
	{
		Rare => new Color(0.38f, 0.64f, 1.0f, 0.96f),
		Elite => new Color(0.80f, 0.47f, 1.0f, 0.97f),
		Alpha => new Color(1.0f, 0.66f, 0.20f, 0.98f),
		_ => new Color(0.86f, 0.90f, 0.96f, 0.94f),
	};

	public static string NameKey(int rarity) => rarity switch
	{
		Rare => "rarity.rare",
		Elite => "rarity.elite",
		Alpha => "rarity.alpha",
		_ => string.Empty,
	};

	public static string ColorNameKey(int rarity) => rarity switch
	{
		Rare => "rarity.color.blue",
		Elite => "rarity.color.purple",
		Alpha => "rarity.color.orange",
		_ => "rarity.color.white",
	};

	// Star prefix for the nameplate (font-safe ★ / ✦).
	public static string StarPrefix(int rarity) => rarity switch
	{
		Rare => "★ ",
		Elite => "★★ ",
		Alpha => "✦ ",
		_ => string.Empty,
	};
}
