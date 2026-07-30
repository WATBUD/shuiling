// Equipment acquisition and progression balance.
public static class EquipmentConfig
{
	// Player jump tuning.
	// Final velocity = PlayerBaseJumpVelocity * (final jump power / BaseJumpPower).
	public const int BaseJumpPower = 100;
	public const float PlayerBaseJumpVelocity = 5.2f;
	public const int MaximumPlayerJumpPower = 250;
	public const bool EquipmentStarsAffectJumpPower = false;

	public const float MonsterDropChance = 0.05f;
	public const int BossGuaranteedDropCount = 2;
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
	public const float SocketScoreWeight = 10.0f;
}
