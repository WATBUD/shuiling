using Godot;

public enum PlayerAttribute
{
	Strength,
	Vitality,
	Agility,
	Intelligence,
}

public partial class PlayerController
{
	public const int AttributePointsPerLevel = 10;

	[Export] public int UnspentAttributePoints { get; private set; }
	[Export] public int Strength { get; private set; }
	[Export] public int Vitality { get; private set; }
	[Export] public int Agility { get; private set; }
	[Export] public int Intelligence { get; private set; }

	public int TotalAllocatedAttributePoints => Strength + Vitality + Agility + Intelligence;

	public void EnsurePlayerAttributePoints()
	{
		Strength = Mathf.Max(Strength, 0);
		Vitality = Mathf.Max(Vitality, 0);
		Agility = Mathf.Max(Agility, 0);
		Intelligence = Mathf.Max(Intelligence, 0);
		UnspentAttributePoints = Mathf.Max(UnspentAttributePoints, 0);

		// Old saves and test-mode characters also receive every point earned by
		// their current level. Already allocated points are never duplicated.
		int earnedPoints = Mathf.Max(Level - 1, 0) * AttributePointsPerLevel;
		int accountedPoints = TotalAllocatedAttributePoints + UnspentAttributePoints;
		if (accountedPoints < earnedPoints)
		{
			UnspentAttributePoints += earnedPoints - accountedPoints;
		}
	}

	public bool AllocateAttributePoint(PlayerAttribute attribute)
	{
		EnsurePlayerAttributePoints();
		if (UnspentAttributePoints <= 0)
		{
			return false;
		}

		switch (attribute)
		{
			case PlayerAttribute.Strength:
				Strength++;
				break;
			case PlayerAttribute.Vitality:
				Vitality++;
				break;
			case PlayerAttribute.Agility:
				Agility++;
				break;
			case PlayerAttribute.Intelligence:
				Intelligence++;
				break;
			default:
				return false;
		}

		UnspentAttributePoints--;
		MarkPlayerBuildStatsDirty();
		CurrentHealth = Mathf.Min(CurrentHealth, EffectiveMaxHealth);
		return true;
	}
}
