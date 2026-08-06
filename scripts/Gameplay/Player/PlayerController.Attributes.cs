using Godot;

public enum PlayerAttribute
{
	Health,
	Attack,
	Defense,
	MoveSpeed,
	AttackSpeed,
	CritChance,
}

public partial class PlayerController
{
	public const int AttributePointsPerLevel = 10;
	public const int HealthPerPoint = 50;
	public const float AttackPerPoint = 0.1f;
	public const float DefensePerPoint = 0.1f;
	public const float MoveSpeedPerPoint = 0.01f;
	public const float AttackSpeedPerPoint = 0.01f;
	public const float CritChancePercentPerPoint = 0.1f;
	public const int MaxPlayerLevel = 100;
	public const int BasePlayerMaxHealth = 1500;
	public const int BasePlayerAttack = 16;
	public const int BasePlayerDefense = 10;
	public const int RebirthHealthBonus = 50;
	public const int RebirthAttackBonus = 1;
	public const int RebirthDefenseBonus = 1;

	[Export] public int UnspentAttributePoints { get; private set; }
	[Export] public int PlayerRebirthCount { get; private set; }
	[Export] public int HealthAttributePoints { get; private set; }
	[Export] public int AttackAttributePoints { get; private set; }
	[Export] public int DefenseAttributePoints { get; private set; }
	[Export] public int MoveSpeedAttributePoints { get; private set; }
	[Export] public int AttackSpeedAttributePoints { get; private set; }
	[Export] public int CritChanceAttributePoints { get; private set; }

	public int TotalAllocatedAttributePoints =>
		HealthAttributePoints + AttackAttributePoints + DefenseAttributePoints
		+ MoveSpeedAttributePoints + AttackSpeedAttributePoints + CritChanceAttributePoints;
	public bool CanPlayerRebirth => Level >= MaxPlayerLevel;

	public void EnsurePlayerAttributePoints()
	{
		HealthAttributePoints = Mathf.Max(HealthAttributePoints, 0);
		AttackAttributePoints = Mathf.Max(AttackAttributePoints, 0);
		DefenseAttributePoints = Mathf.Max(DefenseAttributePoints, 0);
		MoveSpeedAttributePoints = Mathf.Max(MoveSpeedAttributePoints, 0);
		AttackSpeedAttributePoints = Mathf.Max(AttackSpeedAttributePoints, 0);
		CritChanceAttributePoints = Mathf.Max(CritChanceAttributePoints, 0);
		UnspentAttributePoints = Mathf.Max(UnspentAttributePoints, 0);

		int earnedPoints = Mathf.Max(Level - 1, 0) * AttributePointsPerLevel;
		int accountedPoints = TotalAllocatedAttributePoints + UnspentAttributePoints;
		if (accountedPoints < earnedPoints)
		{
			UnspentAttributePoints += earnedPoints - accountedPoints;
		}
	}

	public int GetAllocatedAttributePoints(PlayerAttribute attribute)
	{
		return attribute switch
		{
			PlayerAttribute.Health => HealthAttributePoints,
			PlayerAttribute.Attack => AttackAttributePoints,
			PlayerAttribute.Defense => DefenseAttributePoints,
			PlayerAttribute.MoveSpeed => MoveSpeedAttributePoints,
			PlayerAttribute.AttackSpeed => AttackSpeedAttributePoints,
			PlayerAttribute.CritChance => CritChanceAttributePoints,
			_ => 0,
		};
	}

	public bool AllocateAttributePoint(PlayerAttribute attribute)
	{
		return AllocateAttributePoints(attribute, 1);
	}

	public bool AllocateAttributePoints(PlayerAttribute attribute, int amount)
	{
		EnsurePlayerAttributePoints();
		int requestedAmount = Mathf.Max(amount, 0);
		if (requestedAmount <= 0 || UnspentAttributePoints < requestedAmount)
		{
			return false;
		}

		int previousMaxHealth = EffectiveMaxHealth;
		switch (attribute)
		{
			case PlayerAttribute.Health: HealthAttributePoints += requestedAmount; break;
			case PlayerAttribute.Attack: AttackAttributePoints += requestedAmount; break;
			case PlayerAttribute.Defense: DefenseAttributePoints += requestedAmount; break;
			case PlayerAttribute.MoveSpeed: MoveSpeedAttributePoints += requestedAmount; break;
			case PlayerAttribute.AttackSpeed: AttackSpeedAttributePoints += requestedAmount; break;
			case PlayerAttribute.CritChance: CritChanceAttributePoints += requestedAmount; break;
			default: return false;
		}

		UnspentAttributePoints -= requestedAmount;
		MarkPlayerBuildStatsDirty();
		if (attribute == PlayerAttribute.Health)
		{
			CurrentHealth = Mathf.Min(CurrentHealth + (EffectiveMaxHealth - previousMaxHealth), EffectiveMaxHealth);
		}
		else
		{
			CurrentHealth = Mathf.Min(CurrentHealth, EffectiveMaxHealth);
		}
		return true;
	}

	public bool TryPlayerRebirth()
	{
		if (!CanPlayerRebirth)
		{
			return false;
		}

		PlayerRebirthCount++;
		Level = 1;
		Experience = 0;
		UnspentAttributePoints = 0;
		HealthAttributePoints = 0;
		AttackAttributePoints = 0;
		DefenseAttributePoints = 0;
		MoveSpeedAttributePoints = 0;
		AttackSpeedAttributePoints = 0;
		CritChanceAttributePoints = 0;
		MaxHealth = BasePlayerMaxHealth + PlayerRebirthCount * RebirthHealthBonus;
		Attack = BasePlayerAttack + PlayerRebirthCount * RebirthAttackBonus;
		Defense = BasePlayerDefense + PlayerRebirthCount * RebirthDefenseBonus;
		MarkPlayerBuildStatsDirty();
		CurrentHealth = EffectiveMaxHealth;
		// Level dropped back to 1, so the deploy cap shrank to 3 — bench any pets over it.
		EnforceActivePartyLimit();
		ShowPlayerLevelUpFeedback();
		PostSystemMessage(
			LocaleText.F("system.player_rebirth.done", PlayerRebirthCount, RebirthHealthBonus, RebirthAttackBonus, RebirthDefenseBonus),
			new Color(1.0f, 0.86f, 0.4f),
			GameMessageChannel.Party);
		return true;
	}
}
