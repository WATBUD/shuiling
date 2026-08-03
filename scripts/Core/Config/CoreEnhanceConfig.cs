using Godot;

// Tunables for the core-enhancement merchant: star-enhancing unequipped skill
// cores (persistent "#N" star suffix, mirroring equipment refinement) and
// dismantling collected pets into per-element "core orbs". All balance numbers
// live here so they can be adjusted without touching logic.
public static class CoreEnhanceConfig
{
	// Star axis (mirrors EquipmentConfig.MaxStars / StarBonusPerStar).
	public const int MaxCoreStars = 10;
	public const float CoreStarBonusPerStar = 0.08f; // +8% to the core's bonuses per star
	public const int MaxOrbTier = 10;

	// Pet dismantle yield: DismantleOrbBase + Rarity orbs of the pet's innate
	// element (universal when the species has no affinity), at the pet's level tier.
	public const int DismantleOrbBase = 2;

	// Enhance cost S -> S+1 (target star T): OrbsToReachStar(T) orbs of the core's
	// element at OrbTierForStar(T), plus EnhanceGold(T) gold.
	public const int EnhanceGoldPerStar = 150;

	// Tier from a pet level: 1-10 -> T1, 11-20 -> T2, ... (clamped to MaxOrbTier).
	public static int TierForLevel(int level)
	{
		return Mathf.Clamp((Mathf.Max(level, 1) - 1) / 10 + 1, 1, MaxOrbTier);
	}

	public static int OrbsToReachStar(int targetStar)
	{
		return Mathf.Max(1, targetStar);
	}

	public static int OrbTierForStar(int targetStar)
	{
		return Mathf.Clamp(targetStar, 1, MaxOrbTier);
	}

	public static int EnhanceGold(int targetStar)
	{
		return EnhanceGoldPerStar * Mathf.Max(1, targetStar);
	}
}
