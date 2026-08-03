// Gacha (扭蛋) merchant tunables. Each draw costs DrawCost gold and rolls a tier
// via a top-down cascade: starting at T10, each tier has TierStopPercent[tier]
// chance to be the result; if none hit, T1 is the floor. Higher tiers are rare
// (T10 1% ... T1 89%). A drawn tier becomes the star level of the reward.
public static class GachaConfig
{
	public const int DrawCost = 500;

	// Indexed by tier (1..10); index 0 unused.
	public static readonly int[] TierStopPercent = { 0, 89, 55, 34, 21, 13, 8, 5, 3, 2, 1 };

	public const int MaxTier = 10;

	// Actual chance a single draw yields this tier, following the T10→T1 cascade:
	// reach a tier only if every higher tier failed its stop roll; T1 is the floor
	// (whatever probability mass survives to the bottom). Kept as a computed helper
	// so the displayed odds stay in sync with TierStopPercent.
	public static float TierProbability(int tier)
	{
		tier = System.Math.Clamp(tier, 1, MaxTier);
		float reach = 1.0f; // probability the cascade descends to the current tier
		for (int t = MaxTier; t >= 1; t--)
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
