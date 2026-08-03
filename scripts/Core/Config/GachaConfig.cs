// Gacha (扭蛋) merchant tunables. A draw rolls a tier via a top-down cascade
// starting at the player-chosen cap: each tier has TierStopPercent[tier] chance
// to be the result; if none hit, T1 is the floor. Higher tiers are rare. A drawn
// tier becomes the star level of the reward.
//
// Each save owns its own merchant level (starts at 1). Drawing grants merchant
// EXP scaled by the drawn star, which levels the merchant up. The merchant level
// unlocks higher draw caps: the highest tier a draw can yield is level + 1. The
// player picks the cap they draw at; a higher cap costs more (DrawCostPerTier
// gold per cap tier: T1 = 100, T9 = 900, ...).
public static class GachaConfig
{
	public const int MaxTier = 10;

	// Gold per draw scales with the chosen cap tier.
	public const int DrawCostPerTier = 100;

	// Merchant progression.
	public const int MerchantStartLevel = 1;
	public const int MerchantMaxLevel = 9; // Lv9 unlocks the T10 cap.

	// Indexed by tier (1..10); index 0 unused.
	public static readonly int[] TierStopPercent = { 0, 89, 55, 34, 21, 13, 8, 5, 3, 2, 1 };

	// Highest tier a draw can yield at the given merchant level (level + 1, capped).
	public static int UnlockedMaxTier(int merchantLevel)
	{
		return System.Math.Clamp(merchantLevel + 1, 1, MaxTier);
	}

	// Gold cost of a single draw whose cap is `tierCap`.
	public static int DrawCost(int tierCap)
	{
		return System.Math.Clamp(tierCap, 1, MaxTier) * DrawCostPerTier;
	}

	// EXP a single draw grants, scaled by the star of the item it yielded.
	public static int DrawExp(int star)
	{
		return System.Math.Max(star, 1);
	}

	// EXP required to advance from `level` to `level + 1`. Returns 0 at max level.
	public static int ExpToLevel(int level)
	{
		return level >= MerchantMaxLevel ? 0 : 30 * System.Math.Max(level, 1);
	}

	// Actual chance a single draw yields this tier, following the maxTier→T1
	// cascade: reach a tier only if every higher tier failed its stop roll; T1 is
	// the floor (whatever probability mass survives to the bottom). Kept as a
	// computed helper so the displayed odds stay in sync with TierStopPercent.
	public static float TierProbability(int tier, int maxTier)
	{
		maxTier = System.Math.Clamp(maxTier, 1, MaxTier);
		tier = System.Math.Clamp(tier, 1, maxTier);
		float reach = 1.0f; // probability the cascade descends to the current tier
		for (int t = maxTier; t >= 1; t--)
		{
			float stop = t <= 1 ? 1.0f : TierStopPercent[t] / 100.0f;
			if (t == tier)
			{
				return reach * stop;
			}

			reach *= 1.0f - stop;
		}

		return 0.0f;
	}
}
