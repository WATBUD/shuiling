// Gacha (扭蛋) merchant tunables. Each draw costs DrawCost gold and rolls a tier
// via a top-down cascade: starting at T10, each tier has TierStopPercent[tier]
// chance to be the result; if none hit, T1 is the floor. Higher tiers are rare
// (T10 1% ... T1 89%). A drawn tier becomes the star level of the reward.
public static class GachaConfig
{
	public const int DrawCost = 500;

	// Indexed by tier (1..10); index 0 unused.
	public static readonly int[] TierStopPercent = { 0, 89, 55, 34, 21, 13, 8, 5, 3, 2, 1 };
}
