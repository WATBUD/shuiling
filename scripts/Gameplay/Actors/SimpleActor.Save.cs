using Godot;

public partial class SimpleActor : CharacterBody3D
{
	public ActorSaveData ExportSaveData()
	{
		CompanionBuildLoadout loadout = BuildLoadout;
		return new ActorSaveData
		{
			ActorKind = ActorKind,
			DisplayName = DisplayName,
			Level = Level,
			WorldTier = WorldTier,
			Rarity = Rarity,
			RebirthCount = RebirthCount,
			LevelOneMaxHealth = LevelOneMaxHealth,
			LevelOneAttack = LevelOneAttack,
			LevelOneDefense = LevelOneDefense,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			IsDefeated = _isDefeated,
			IsAwaitingRecovery = _isAwaitingRecovery,
			IsInWarehouseCollection = _isInWarehouseCollection,
			FallenMapId = _fallenMapId,
			WorldPosition = new SaveVector3 { X = GlobalPosition.X, Y = GlobalPosition.Y, Z = GlobalPosition.Z },
			Attack = Attack,
			Defense = Defense,
			MoveSpeed = MoveSpeed,
			ExperienceReward = ExperienceReward,
			GoldReward = GoldReward,
			Experience = Experience,
			EvolutionStage = EvolutionStage,
			SpecialAbility = SpecialAbility,
			AbilityRank = AbilityRank,
			CombatRole = CombatRole,
			Personality = Personality,
			PassiveAbility = PassiveAbility,
			Affinity = Affinity,
			MoodStateId = MoodStateId,
			AttackModeId = AttackModeId,
			BuildLoadout = new CompanionBuildSaveData
			{
				HelmetId = loadout.HelmetId,
				WeaponId = loadout.WeaponId,
				ArmorId = loadout.ArmorId,
				BootsId = loadout.BootsId,
				AccessoryId = loadout.AccessoryId,
				AttributeGemId = "gem.attribute.none",
				SkillGemIds = MakeSkillGemIdArray(loadout),
				SkillGemLevels = MakeSkillGemLevelArray(loadout),
			},
		};
	}

	public void ApplySaveData(ActorSaveData data)
	{
		ActorKind = data.ActorKind;
		DisplayName = data.DisplayName;
		Level = Mathf.Max(data.Level, 1);
		WorldTier = WorldTierCatalog.ClampTier(data.WorldTier);
		Rarity = data.Rarity;
		RebirthCount = Mathf.Max(data.RebirthCount, 0);
		LevelOneMaxHealth = Mathf.Max(data.LevelOneMaxHealth, 0);
		LevelOneAttack = Mathf.Max(data.LevelOneAttack, 0);
		LevelOneDefense = Mathf.Max(data.LevelOneDefense, 0);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		_isDefeated = data.IsDefeated || data.CurrentHealth <= 0;
		_isAwaitingRecovery = _isDefeated && data.IsAwaitingRecovery;
		_isInWarehouseCollection = data.IsInWarehouseCollection;
		_fallenMapId = data.FallenMapId;
		CurrentHealth = _isDefeated ? 0 : Mathf.Clamp(data.CurrentHealth, 1, MaxHealth);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		MoveSpeed = Mathf.Clamp(data.MoveSpeed, 0.3f, 20.0f);
		ExperienceReward = Mathf.Max(data.ExperienceReward, 0);
		GoldReward = Mathf.Max(data.GoldReward, 0);
		Experience = Mathf.Max(data.Experience, 0);
		EvolutionStage = Mathf.Clamp(data.EvolutionStage, 0, 3);
		SpecialAbility = data.SpecialAbility;
		AbilityRank = Mathf.Max(data.AbilityRank, 1);
		CombatRole = string.IsNullOrWhiteSpace(data.CombatRole) ? "DPS" : data.CombatRole;
		Personality = string.IsNullOrWhiteSpace(data.Personality) ? "personality.calm" : data.Personality;
		PassiveAbility = string.IsNullOrWhiteSpace(data.PassiveAbility) ? "ability.none" : data.PassiveAbility;
		Affinity = Mathf.Clamp(data.Affinity, -100, 100);
		MoodStateId = data.MoodStateId;
		AttackModeId = string.IsNullOrWhiteSpace(data.AttackModeId)
			? BuildCatalog.GetDefaultAttackModeId(this)
			: BuildCatalog.GetAttackMode(data.AttackModeId).Id;
		bool isLegacyStarterShowcase = DisplayName == "name.monster.bunny"
			&& Level >= MaxCompanionLevel
			&& RebirthCount == 0
			&& MaxHealth == 2600
			&& Attack == 260
			&& Defense == 150;
		if (isLegacyStarterShowcase)
		{
			// Only the old hand-authored starter has this exact stat signature.
			// Convert it to the new level-1, one-rebirth starter format.
			ConfigureLevelOneStats(124, 18, 9);
			RebirthCount = 1;
			Level = 1;
			Experience = 0;
			MaxHealth = LevelOneMaxHealth + RebirthStatBonus;
			Attack = LevelOneAttack + RebirthStatBonus;
			Defense = LevelOneDefense + RebirthStatBonus;
			CurrentHealth = MaxHealth;
		}
		EnsureLevelOneStats();
		_buildLoadout = new CompanionBuildLoadout
		{
			HelmetId = data.BuildLoadout.HelmetId,
			WeaponId = data.BuildLoadout.WeaponId,
			ArmorId = data.BuildLoadout.ArmorId,
			BootsId = string.IsNullOrWhiteSpace(data.BuildLoadout.BootsId) ? "equip.boots.traveler" : data.BuildLoadout.BootsId,
			AccessoryId = data.BuildLoadout.AccessoryId,
			// Migrate legacy elemental gems to the element carried by the active core.
			AttributeGemId = "gem.attribute.none",
			SkillGemIds = data.BuildLoadout.SkillGemIds is { Length: > 0 } savedIds
				? (string[])savedIds.Clone()
				: new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
			SkillGemLevels = data.BuildLoadout.SkillGemLevels is { Length: > 0 } savedLevels
				? (int[])savedLevels.Clone()
				: new[] { 1, 1, 1 },
		};
		_buildLoadout.EnsureSkillSlots();
		NormalizeSkillCoreSlots();
		RemoveUnsupportedProjectileGems();
		_buildConfigured = true;
		_buildStatsDirty = true;
		RecalculateBuildStats();
		CurrentHealth = _isDefeated ? 0 : Mathf.Clamp(data.CurrentHealth, 1, EffectiveMaxHealth);
		RefreshNameplate();
	}

	private static string[] MakeSkillGemIdArray(CompanionBuildLoadout loadout)
	{
		var ids = new string[BuildCatalog.SupportCoreSlotCount];
		for (int index = 0; index < ids.Length; index++)
		{
			ids[index] = loadout.GetSkillGemId(index);
		}

		return ids;
	}

	private static int[] MakeSkillGemLevelArray(CompanionBuildLoadout loadout)
	{
		var levels = new int[BuildCatalog.SupportCoreSlotCount];
		for (int index = 0; index < levels.Length; index++)
		{
			levels[index] = loadout.GetSkillGemLevel(index);
		}

		return levels;
	}
}
