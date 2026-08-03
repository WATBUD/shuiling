// Equipment acquisition and progression balance.
public static class EquipmentConfig
{
	// Player jump tuning.
	// Final velocity = PlayerBaseJumpVelocity * (final jump power / BaseJumpPower).
	public const int BaseJumpPower = 100;
	public const float PlayerBaseJumpVelocity = 5.2f;
	public const int MaximumPlayerJumpPower = 250;
	public const bool EquipmentStarsAffectJumpPower = false;

	// Normal monsters: 1% chance to drop a single piece of equipment.
	public const float MonsterDropChance = 0.01f;
	// Bosses: 20% chance to drop equipment; when they do, 1..6 pieces at once.
	public const float BossDropChance = 0.20f;
	public const int BossMaxDropCount = 6;
	public const int MaxStars = 10;
	public const float StarBonusPerStar = 0.08f;
	public const int MinimumWeaponAttackSpeed = 1;
	public const int MaximumWeaponAttackSpeed = 99;
	public const int NeutralWeaponAttackSpeed = 50;
	public const float WeaponAttackSpeedToCooldownReduction = 0.01f;
	public const float AttackScoreWeight = 3.0f;
	public const float DefenseScoreWeight = 2.0f;
	public const float MoveSpeedScoreWeight = 120.0f;
	public const float AttackSpeedScoreWeight = 150.0f;
	public const float AttackRangeScoreWeight = 12.0f;
	public const float CriticalChanceScoreWeight = 180.0f;
}
