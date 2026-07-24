using Godot;

// Shared XP growth curve + level-difference scaling for the player and every
// companion. Two goals:
//  1. Levels ramp up (quadratic) so high levels are a real investment.
//  2. Over-leveled kills give almost nothing (no easy leveling on weak mobs),
//     while a higher-level source (a strong boss) gives full/bonus XP.
public static class ExperienceTable
{
	// XP needed to advance FROM the given level to the next.
	public static int ToNextLevel(int level, int evolutionStage = 0)
	{
		int lv = Mathf.Max(level, 1);
		return 45 + lv * 10 + lv * lv * 4 + evolutionStage * 25;
	}

	// Multiplier applied to a reward based on earner vs. source level.
	public static float LevelDifferenceMultiplier(int earnerLevel, int sourceLevel)
	{
		int diff = earnerLevel - sourceLevel;
		if (diff <= 0)
		{
			// Source is equal or higher level: full XP, up to +50% for tougher foes.
			return Mathf.Min(1.0f + -diff * 0.05f, 1.5f);
		}

		// Over-leveled: ~12% falloff per level, down to a 4% floor.
		return Mathf.Max(1.0f - diff * 0.12f, 0.04f);
	}

	// Scaled reward. Far-over-leveled kills can round to 0 (no free levels);
	// equal-or-tougher sources always grant at least 1.
	public static int ScaleReward(int baseAmount, int earnerLevel, int sourceLevel)
	{
		int scaled = Mathf.RoundToInt(baseAmount * LevelDifferenceMultiplier(earnerLevel, sourceLevel));
		return sourceLevel >= earnerLevel ? Mathf.Max(scaled, 1) : Mathf.Max(scaled, 0);
	}
}
