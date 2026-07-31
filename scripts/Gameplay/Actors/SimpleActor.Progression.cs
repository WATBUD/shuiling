using Godot;

public partial class SimpleActor : CharacterBody3D
{
	// Applied at spawn: scale base stats + rewards by rarity, then refresh.
	public void ApplyRarity(int rarity)
	{
		Rarity = rarity;
		if (rarity <= MonsterRarity.Common)
		{
			return;
		}

		float stat = MonsterRarity.StatMultiplier(rarity);
		float reward = MonsterRarity.RewardMultiplier(rarity);
		MaxHealth = Mathf.RoundToInt(MaxHealth * stat);
		CurrentHealth = MaxHealth;
		Attack = Mathf.RoundToInt(Attack * stat);
		Defense = Mathf.RoundToInt(Defense * stat);
		ExperienceReward = Mathf.RoundToInt(ExperienceReward * reward);
		GoldReward = Mathf.RoundToInt(GoldReward * reward);
		_buildConfigured = false;
		_buildStatsDirty = true;
		RefreshNameplate();
	}

	private bool HasLevelOneStats()
	{
		return LevelOneMaxHealth > 0 && LevelOneAttack > 0 && LevelOneDefense >= 0;
	}

	public void ConfigureLevelOneStats(int maxHealth, int attack, int defense)
	{
		LevelOneMaxHealth = Mathf.Max(maxHealth, 1);
		LevelOneAttack = Mathf.Max(attack, 1);
		LevelOneDefense = Mathf.Max(defense, 0);
	}

	private void EnsureLevelOneStats()
	{
		if (HasLevelOneStats())
		{
			return;
		}

		// Legacy saves did not preserve the level-1 baseline. Every completed
		// rebirth in those saves incorrectly retained a full level-100 growth
		// cycle, so remove 99 level gains for each historic rebirth, plus the
		// gains made in the current cycle. New companions take this snapshot
		// once when captured and never need this migration path again.
		int historicLevelGains = Mathf.Max(Level - 1, 0) + Mathf.Max(RebirthCount, 0) * (MaxCompanionLevel - 1);
		int rebirthBonus = RebirthTotalStatBonus;
		// Legacy saves were raised with the original evolution-only formula.
		// Keep that formula for reconstructing their level-one baseline.
		int healthPerLevel = 14 + EvolutionStage * 4;
		int attackPerLevel = 3 + EvolutionStage;
		int defensePerLevel = 2 + EvolutionStage;
		LevelOneMaxHealth = Mathf.Max(MaxHealth - rebirthBonus - historicLevelGains * healthPerLevel, 40);
		LevelOneAttack = Mathf.Max(Attack - rebirthBonus - historicLevelGains * attackPerLevel, 3);
		LevelOneDefense = Mathf.Max(Defense - rebirthBonus - historicLevelGains * defensePerLevel, 1);
	}

	public void GrantTraining(int amount)
	{
		// Companions stop gaining levels at the cap; excess XP is discarded until
		// the player rebirths them (轉生) to start climbing again.
		if (_isCaptured && Level >= MaxCompanionLevel)
		{
			Experience = 0;
			RefreshNameplate();
			return;
		}

		int levelBefore = Level;
		Experience += Mathf.Max(amount, 0);
		while (Experience >= ExperienceToNextLevel)
		{
			Experience -= ExperienceToNextLevel;
			LevelUp();
			if (_isCaptured && Level >= MaxCompanionLevel)
			{
				Experience = 0;
				break;
			}
		}

		if (Level > levelBefore)
		{
			ShowLevelUpFeedback();
		}

		RefreshNameplate();
	}

	// 升等文字提示 + 金色特效（每次獲得經驗只顯示一次，顯示最終等級）。
	private void ShowLevelUpFeedback()
	{
		SpawnCombatEffect(LocaleText.F("effect.level_up", Level), new Color(1.0f, 0.9f, 0.42f, 0.95f), GlobalPosition + new Vector3(0.0f, 1.75f, 0.0f), 0.95f, 1.1f);

		var effect = new LevelUpEffect();
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent != null)
		{
			parent.AddChild(effect);
			effect.GlobalPosition = new Vector3(GlobalPosition.X, GlobalPosition.Y + 0.05f, GlobalPosition.Z);
		}
	}

	// Reset to level 1 and bank a permanent +5 to every base stat (stackable).
	public bool TryRebirth()
	{
		if (!CanRebirth)
		{
			return false;
		}

		EnsureLevelOneStats();
		RebirthCount++;
		Level = 1;
		Experience = 0;
		int totalRebirthBonus = RebirthTotalStatBonus;
		MaxHealth = Mathf.Max(LevelOneMaxHealth + totalRebirthBonus, 1);
		Attack = Mathf.Max(LevelOneAttack + totalRebirthBonus, 1);
		Defense = Mathf.Max(LevelOneDefense + totalRebirthBonus, 0);
		MarkBaseStatsChanged();
		CurrentHealth = EffectiveMaxHealth;
		RefreshNameplate();
		return true;
	}

	public bool TryEvolve()
	{
		if (!CanEvolve)
		{
			return false;
		}

		EvolutionStage++;
		int healthIncrease = 36 + EvolutionStage * 14;
		int attackIncrease = 8 + EvolutionStage * 2;
		int defenseIncrease = 6 + EvolutionStage * 2;
		MaxHealth += healthIncrease;
		CurrentHealth = MaxHealth;
		Attack += attackIncrease;
		Defense += defenseIncrease;
		if (HasLevelOneStats())
		{
			LevelOneMaxHealth += healthIncrease;
			LevelOneAttack += attackIncrease;
			LevelOneDefense += defenseIncrease;
		}
		AbilityRank++;
		MarkBaseStatsChanged();
		ApplyEvolutionAppearance();
		CurrentHealth = EffectiveMaxHealth;
		RefreshNameplate();
		return true;
	}

	public void EnhanceAbility()
	{
		if (SpecialAbility == "ability.none")
		{
			SpecialAbility = ActorKind == "monster" ? "ability.monster.burst" : "ability.npc.tactics";
		}

		AbilityRank++;
		int attackIncrease = ActorKind == "monster" ? 2 : 1;
		int defenseIncrease = ActorKind == "monster" ? 1 : 2;
		Attack += attackIncrease;
		Defense += defenseIncrease;
		if (HasLevelOneStats())
		{
			LevelOneAttack += attackIncrease;
			LevelOneDefense += defenseIncrease;
		}
		MarkBaseStatsChanged();
		RefreshNameplate();
	}

	private void LevelUp()
	{
		Level++;
		MaxHealth += GetHealthGrowthPerLevel();
		CurrentHealth = MaxHealth;
		Attack += GetAttackGrowthPerLevel();
		Defense += GetDefenseGrowthPerLevel();
		MarkBaseStatsChanged();
		CurrentHealth = EffectiveMaxHealth;
	}

	// Companions never use the player's manual point pool. Their physique
	// (ability rank), rarity and evolution stage determine automatic growth.
	private int GetHealthGrowthPerLevel()
	{
		return 14 + EvolutionStage * 4 + Mathf.Max(Rarity, 0) * 3 + Mathf.Max(AbilityRank - 1, 0) * 2;
	}

	private int GetAttackGrowthPerLevel()
	{
		return 3 + EvolutionStage + Mathf.CeilToInt(Mathf.Max(Rarity, 0) * 0.5f) + Mathf.Max(AbilityRank - 1, 0) / 3;
	}

	private int GetDefenseGrowthPerLevel()
	{
		return 2 + EvolutionStage + Mathf.Max(Rarity, 0) / 2 + Mathf.Max(AbilityRank - 1, 0) / 4;
	}

	private void ApplyEvolutionAppearance()
	{
		float scale = 1.0f + EvolutionStage * 0.08f;
		Scale = Vector3.One * scale;
	}
}
