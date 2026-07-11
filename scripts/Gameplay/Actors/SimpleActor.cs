using Godot;

public partial class SimpleActor : CharacterBody3D
{
	private enum SquadActivity
	{
		Follow,
		Guard,
		Scout,
		Gather,
		Roam,
		Rest,
	}

	[Export] public string ActorKind { get; set; } = "npc";
	[Export] public float MoveSpeed { get; set; } = 1.6f;
	[Export] public float WanderRadius { get; set; } = 10.0f;
	[Export] public float ChaseRadius { get; set; } = 20.0f;
	[Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;
	[Export] public string DisplayName { get; set; } = "name.actor.traveler";
	[Export] public int Level { get; set; } = 1;
	[Export] public int MaxHealth { get; set; } = 100;
	[Export] public int CurrentHealth { get; set; } = 100;
	[Export] public int Attack { get; set; } = 10;
	[Export] public int Defense { get; set; } = 6;
	[Export] public int ExperienceReward { get; set; } = 6;
	[Export] public int GoldReward { get; set; } = 2;
	[Export] public int Experience { get; set; }
	[Export] public int EvolutionStage { get; set; }
	[Export] public string SpecialAbility { get; set; } = "ability.none";
	[Export] public int AbilityRank { get; set; } = 1;
	[Export] public string CombatRole { get; set; } = "DPS";
	[Export] public string Personality { get; set; } = "personality.calm";
	[Export] public string PassiveAbility { get; set; } = "ability.none";
	[Export] public int Affinity { get; set; } = 50;
	[Export] public float DetectionRadius { get; set; } = 12.0f;
	[Export] public float AttackRange { get; set; } = 1.8f;
	[Export] public float AttackCooldown { get; set; } = 1.35f;

	private readonly RandomNumberGenerator _rng = new();
	private bool _isCaptured;
	private bool _isInActiveParty;
	private bool _isDefeated;
	private uint _defaultCollisionLayer;
	private uint _defaultCollisionMask;
	private PlayerController? _followTarget;
	private int _followSlot;
	private float _gravity;
	private Vector3 _targetPosition = Vector3.Zero;
	private float _waitTime;
	private float _attackCooldownRemaining;
	private float _footstepEffectRemaining;
	private float _movementAnimationPhase;
	private float _externalAnimationLockRemaining;
	private string _externalAnimationState = string.Empty;
	private Tween? _attackPoseTween;
	private Node3D? _attackPoseTarget;
	private Vector3 _attackPoseBaseScale = Vector3.One;
	private Node3D? _externalModelRootMotionNode;
	private SimpleActor? _combatTarget;
	private SimpleActor? _retaliationTarget;
	private float _retaliationTargetRemaining;
	private Label3D? _nameplate;
	private SquadActivity _squadActivity = SquadActivity.Follow;
	private Vector3 _squadActivityLocalOffset = Vector3.Zero;
	private float _squadActivityRemaining;
	private float _squadThinkRemaining;
	private CompanionBuildLoadout _buildLoadout = new();
	private BuildStats _buildStats = new();
	private bool _buildConfigured;
	private bool _buildStatsDirty = true;

	public bool CanBeCaptured => ActorKind == "monster" && !_isCaptured && !_isDefeated;
	public bool IsNpcRecruitCandidate => ActorKind == "npc" && !_isCaptured && !_isDefeated;
	public bool CanJoinByAffinity => IsNpcRecruitCandidate && Affinity >= 80;
	public bool IsCaptured => _isCaptured;
	public bool IsInActiveParty => _isInActiveParty;
	public bool IsDefeated => _isDefeated;
	public bool IsHostileToPlayer => !_isCaptured && !_isDefeated && ActorKind == "monster";
	public CompanionBuildLoadout BuildLoadout
	{
		get
		{
			EnsureBuildLoadout();
			return _buildLoadout;
		}
	}
	public BuildStats CurrentBuildStats
	{
		get
		{
			if (_buildStatsDirty)
			{
				RecalculateBuildStats();
			}

			return _buildStats;
		}
	}
	public int EffectiveMaxHealth => CurrentBuildStats.MaxHealth;
	public int EffectiveAttack => CurrentBuildStats.Attack;
	public int EffectiveDefense => CurrentBuildStats.Defense;
	public float EffectiveMoveSpeed => Mathf.Max(MoveSpeed * CurrentBuildStats.MoveSpeedMultiplier, 0.3f);
	public float EffectiveAttackRange => Mathf.Max(AttackRange + CurrentBuildStats.AttackRangeBonus, 0.75f);
	public float EffectiveDetectionRadius => Mathf.Max(DetectionRadius + CurrentBuildStats.DetectionRadiusBonus, 3.0f);
	public float EffectiveAttackCooldown => Mathf.Max(AttackCooldown * CurrentBuildStats.AttackCooldownMultiplier, 0.22f);
	public string TypeName => LocaleText.T(ActorKind == "monster" ? "actor.type.monster" : "actor.type.npc");
	public string StateName => _isDefeated
		? LocaleText.T("actor.state.defeated")
		: _isCaptured
			? _isInActiveParty ? LocaleText.T("actor.state.active") : LocaleText.T("actor.state.stored")
			: ActorKind == "monster" ? LocaleText.T("actor.state.hostile") : LocaleText.T("actor.state.neutral");
	public string GrowthName => EvolutionStage <= 0
		? LocaleText.T("actor.growth.base")
		: EvolutionStage == 1
			? LocaleText.T("actor.growth.evo1")
			: EvolutionStage == 2 ? LocaleText.T("actor.growth.evo2") : LocaleText.T("actor.growth.final");
	public string CombatRoleName => CombatRole switch
	{
		"Tank" => LocaleText.T("role.tank"),
		"Ranged" => LocaleText.T("role.ranged"),
		"Support" => LocaleText.T("role.support"),
		"Gatherer" => LocaleText.T("role.gatherer"),
		"Builder" => LocaleText.T("role.builder"),
		_ => LocaleText.T("role.dps"),
	};
	public string LocalizedDisplayName => LocaleText.T(DisplayName);
	public string LocalizedSpecialAbility => LocaleText.T(SpecialAbility);
	public string LocalizedPersonality => LocaleText.T(Personality);
	public string LocalizedPassiveAbility => LocaleText.T(PassiveAbility);
	public string IdentityName => LocaleText.T(CurrentBuildStats.IdentityNameKey);
	public string IdentityPassives => BuildCatalog.LocalizedList(CurrentBuildStats.PassiveKeys);
	public string IdentityUniqueSkills => BuildCatalog.LocalizedList(CurrentBuildStats.UniqueSkillKeys);
	public string BuildEquipmentSummary => BuildCatalog.LocalizedEquipmentSet(BuildLoadout);
	public string BuildSkillSummary => BuildCatalog.LocalizedSkillGems(BuildLoadout);
	public string BuildAttributeGemName => LocaleText.T(BuildCatalog.GetAttributeGem(BuildLoadout.AttributeGemId).NameKey);
	public string BuildAiGemName => LocaleText.T(BuildCatalog.GetAiGem(BuildLoadout.AiGemId).NameKey);
	public string BuildRareComboName => BuildCatalog.LocalizedRareCombo(CurrentBuildStats);
	public string BuildElementName => LocaleText.T(CurrentBuildStats.DamageElementNameKey);
	public bool IsRangedCombatant => CombatRole == "Ranged" || CombatRole == "Support" || EffectiveAttackRange > 3.0f;
	public string CombatRangeName => IsRangedCombatant ? "Ranged" : "Melee";
	public string CombatSummary => $"{LocaleText.F("combat.summary", CombatRoleName, LocalizedPersonality, Affinity)} / {CombatRangeName} / {LocaleText.F("stat.affinity_value", Affinity)}";
	public Color AttackFxColor => GetAttackColor();
	public int ExperienceToNextLevel => 35 + Level * 18 + EvolutionStage * 20;
	public bool CanEvolve => EvolutionStage < 3 && Level >= (EvolutionStage + 1) * 5;
	public float HealthRatio => EffectiveMaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)EffectiveMaxHealth, 0.0f, 1.0f);

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_rng.Seed = Time.GetTicksUsec() + GetInstanceId();
		_defaultCollisionLayer = CollisionLayer;
		_defaultCollisionMask = CollisionMask;

		if (HomePosition == Vector3.Zero)
		{
			HomePosition = GlobalPosition;
		}

		_targetPosition = PickWanderTarget();
		EnsureBuildLoadout();
		RecalculateBuildStats();
		AddToGroup(ActorKind == "monster" ? "monsters" : "npcs");
		CreateNameplate();
		LocaleText.LanguageChanged += RefreshNameplate;
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshNameplate;
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		_attackCooldownRemaining = Mathf.Max(_attackCooldownRemaining - step, 0.0f);
		_retaliationTargetRemaining = Mathf.Max(_retaliationTargetRemaining - step, 0.0f);
		Vector3 velocity = Velocity;

		if (_isDefeated)
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		if (_isCaptured)
		{
			FollowCapturedTarget(velocity, step);
			return;
		}

		Node3D? player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		bool chasing = false;
		Vector3 destination = _targetPosition;

		if (ActorKind == "monster" && player != null)
		{
			if (TryGetRetaliationTarget(out SimpleActor retaliationTarget))
			{
				chasing = true;
				destination = retaliationTarget.GlobalPosition;
				if (TryAttackActorTarget(retaliationTarget, velocity, step))
				{
					return;
				}
			}
			else
			{
				float distanceToPlayer = GlobalPosition.DistanceTo(player.GlobalPosition);
				if (distanceToPlayer <= ChaseRadius)
				{
					chasing = true;
					destination = player.GlobalPosition;
					if (TryAttackPlayer(player, velocity, step))
					{
						return;
					}
				}
			}
		}

		if (!chasing)
		{
			_waitTime = Mathf.Max(_waitTime - step, 0.0f);
			if (_waitTime > 0.0f)
			{
				Velocity = SlowToStop(velocity, step);
				MoveAndSlideWithEffects(step);
				return;
			}
		}

		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;

		if (!chasing && toDestination.Length() < 0.8f)
		{
			_waitTime = (float)_rng.RandfRange(0.6f, 2.2f);
			_targetPosition = PickWanderTarget();
			velocity = SlowToStop(velocity, step);
		}
		else
		{
			Vector3 direction = toDestination.Normalized();
			float activeSpeed = MoveSpeed * (chasing ? 1.35f : 1.0f);
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * activeSpeed, activeSpeed * 6.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * activeSpeed, activeSpeed * 6.0f * step);
			FaceDirection(direction, step);
		}

		Velocity = velocity;
		MoveAndSlideWithEffects(step);
	}

	public void Capture(PlayerController followTarget)
	{
		_isCaptured = true;
		_isDefeated = false;
		_followTarget = followTarget;
		_isInActiveParty = false;
		_waitTime = 0.0f;
		ResetSquadActivity();
		Velocity = Vector3.Zero;
		CurrentHealth = Mathf.Max(CurrentHealth, Mathf.RoundToInt(EffectiveMaxHealth * 0.45f));
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		AddToGroup("captured_actors");
		RefreshNameplate();
	}

	public void Recruit(PlayerController followTarget)
	{
		Capture(followTarget);
	}

	public void SetWorldMapActive(bool active)
	{
		if (_isCaptured)
		{
			return;
		}

		Visible = active;
		SetPhysicsProcess(active && !_isDefeated);
		if (active && !_isDefeated)
		{
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
		}
		else
		{
			CollisionLayer = 0;
			CollisionMask = 0;
		}
	}

	public void DeployToParty(PlayerController followTarget, int followSlot)
	{
		_followTarget = followTarget;
		_followSlot = followSlot;
		_isInActiveParty = true;
		Visible = true;
		SetPhysicsProcess(!_isDefeated);
		CollisionLayer = _defaultCollisionLayer;
		CollisionMask = _defaultCollisionMask;
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		ResetSquadActivity();
		if (!_isDefeated)
		{
			GlobalPosition = GetFollowDestination();
			ApplyLivingPose();
		}
		Velocity = Vector3.Zero;
		RefreshNameplate();
	}

	public void StoreInCollection()
	{
		_isInActiveParty = false;
		Velocity = Vector3.Zero;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);
		RefreshNameplate();
	}

	public void SetFollowSlot(int followSlot)
	{
		_followSlot = followSlot;
		_squadThinkRemaining = Mathf.Min(_squadThinkRemaining, 0.2f);
	}

	public void OnFormationLayoutChanged()
	{
		ResetSquadActivity();
		_squadActivityRemaining = 0.0f;
		_squadThinkRemaining = 0.0f;
	}

	public void CycleBuildEquipment(EquipmentSlot slot)
	{
		BuildLoadout.CycleEquipment(slot);
		MarkBuildChanged();
	}

	public void EquipBuildEquipment(EquipmentSlot slot, string equipmentId)
	{
		if (BuildCatalog.GetEquipment(equipmentId).Slot != slot)
		{
			return;
		}

		BuildLoadout.SetEquipmentId(slot, equipmentId);
		MarkBuildChanged();
	}

	public void CycleAttributeGem()
	{
		BuildLoadout.CycleAttributeGem();
		MarkBuildChanged();
	}

	public void EquipAttributeGem(string gemId)
	{
		BuildLoadout.AttributeGemId = BuildCatalog.GetAttributeGem(gemId).Id;
		MarkBuildChanged();
	}

	public void CycleSkillGem(int slotIndex)
	{
		BuildLoadout.CycleSkillGem(slotIndex);
		MarkBuildChanged();
	}

	public void EquipSkillGem(int slotIndex, string gemId)
	{
		int safeSlot = Mathf.Clamp(slotIndex, 0, BuildLoadout.SkillGemIds.Length - 1);
		BuildLoadout.SkillGemIds[safeSlot] = BuildCatalog.GetSkillGem(gemId).Id;
		MarkBuildChanged();
	}

	public void CycleAiGem()
	{
		BuildLoadout.CycleAiGem();
		MarkBuildChanged();
	}

	public void EquipAiGem(string gemId)
	{
		BuildLoadout.AiGemId = BuildCatalog.GetAiGem(gemId).Id;
		MarkBuildChanged();
	}

	public void ConfigureStats(string displayName, int level, int maxHealth, int attack, int defense, int experienceReward, int goldReward)
	{
		DisplayName = displayName;
		Level = level;
		MaxHealth = Mathf.Max(maxHealth, 1);
		CurrentHealth = MaxHealth;
		Attack = Mathf.Max(attack, 0);
		Defense = Mathf.Max(defense, 0);
		ExperienceReward = Mathf.Max(experienceReward, 0);
		GoldReward = Mathf.Max(goldReward, 0);
		_buildConfigured = false;
		_buildStatsDirty = true;
		RefreshNameplate();
	}

	public void ConfigureGrowth(string specialAbility, int abilityRank)
	{
		SpecialAbility = specialAbility;
		AbilityRank = Mathf.Max(abilityRank, 1);
	}

	public void ConfigureCombatProfile(string combatRole, string personality, string passiveAbility, int affinity)
	{
		CombatRole = combatRole;
		Personality = personality;
		PassiveAbility = passiveAbility;
		Affinity = Mathf.Clamp(affinity, 0, 100);

		switch (CombatRole)
		{
			case "Tank":
				Defense += 5 + Level;
				MaxHealth += 24 + Level * 3;
				CurrentHealth = MaxHealth;
				AttackRange = 1.7f;
				DetectionRadius = 13.0f;
				AttackCooldown = 1.55f;
				break;
			case "Ranged":
				Attack += 2 + Mathf.CeilToInt(Level * 0.5f);
				AttackRange = 6.0f;
				DetectionRadius = 16.0f;
				AttackCooldown = 1.65f;
				break;
			case "Support":
				Defense += 3;
				AttackRange = 4.0f;
				DetectionRadius = 14.0f;
				AttackCooldown = 1.75f;
				break;
			case "Gatherer":
			case "Builder":
				Defense += 2;
				AttackRange = 2.2f;
				DetectionRadius = 10.0f;
				AttackCooldown = 1.45f;
				break;
			default:
				CombatRole = "DPS";
				Attack += 4 + Level;
				AttackRange = 2.0f;
				DetectionRadius = 14.0f;
				AttackCooldown = 1.20f;
				break;
		}

		_buildConfigured = false;
		EnsureBuildLoadout();
		RecalculateBuildStats();
		CurrentHealth = EffectiveMaxHealth;
	}

	public void IncreaseAffinity(int amount)
	{
		Affinity = Mathf.Clamp(Affinity + amount, 0, 100);
		RefreshNameplate();
	}

	public ActorSaveData ExportSaveData()
	{
		CompanionBuildLoadout loadout = BuildLoadout;
		return new ActorSaveData
		{
			ActorKind = ActorKind,
			DisplayName = DisplayName,
			Level = Level,
			MaxHealth = MaxHealth,
			CurrentHealth = CurrentHealth,
			Attack = Attack,
			Defense = Defense,
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
			BuildLoadout = new CompanionBuildSaveData
			{
				HelmetId = loadout.HelmetId,
				WeaponId = loadout.WeaponId,
				ArmorId = loadout.ArmorId,
				AccessoryId = loadout.AccessoryId,
				AttributeGemId = loadout.AttributeGemId,
				SkillGemIds = new[] { loadout.SkillGemIds[0], loadout.SkillGemIds[1], loadout.SkillGemIds[2] },
				AiGemId = loadout.AiGemId,
			},
		};
	}

	public void ApplySaveData(ActorSaveData data)
	{
		ActorKind = data.ActorKind;
		DisplayName = data.DisplayName;
		Level = Mathf.Max(data.Level, 1);
		MaxHealth = Mathf.Max(data.MaxHealth, 1);
		CurrentHealth = Mathf.Clamp(data.CurrentHealth, 1, MaxHealth);
		Attack = Mathf.Max(data.Attack, 0);
		Defense = Mathf.Max(data.Defense, 0);
		ExperienceReward = Mathf.Max(data.ExperienceReward, 0);
		GoldReward = Mathf.Max(data.GoldReward, 0);
		Experience = Mathf.Max(data.Experience, 0);
		EvolutionStage = Mathf.Clamp(data.EvolutionStage, 0, 3);
		SpecialAbility = data.SpecialAbility;
		AbilityRank = Mathf.Max(data.AbilityRank, 1);
		CombatRole = string.IsNullOrWhiteSpace(data.CombatRole) ? "DPS" : data.CombatRole;
		Personality = string.IsNullOrWhiteSpace(data.Personality) ? "personality.calm" : data.Personality;
		PassiveAbility = string.IsNullOrWhiteSpace(data.PassiveAbility) ? "ability.none" : data.PassiveAbility;
		Affinity = Mathf.Clamp(data.Affinity, 0, 100);
		_buildLoadout = new CompanionBuildLoadout
		{
			HelmetId = data.BuildLoadout.HelmetId,
			WeaponId = data.BuildLoadout.WeaponId,
			ArmorId = data.BuildLoadout.ArmorId,
			AccessoryId = data.BuildLoadout.AccessoryId,
			AttributeGemId = data.BuildLoadout.AttributeGemId,
			SkillGemIds = data.BuildLoadout.SkillGemIds.Length >= 3
				? new[] { data.BuildLoadout.SkillGemIds[0], data.BuildLoadout.SkillGemIds[1], data.BuildLoadout.SkillGemIds[2] }
				: new[] { "gem.skill.none", "gem.skill.none", "gem.skill.none" },
			AiGemId = data.BuildLoadout.AiGemId,
		};
		_buildConfigured = true;
		_buildStatsDirty = true;
		RecalculateBuildStats();
		CurrentHealth = Mathf.Clamp(data.CurrentHealth, 1, EffectiveMaxHealth);
		RefreshNameplate();
	}

	private void EnsureBuildLoadout()
	{
		if (_buildConfigured)
		{
			return;
		}

		_buildLoadout = BuildCatalog.CreateStarterLoadout(this);
		_buildConfigured = true;
		_buildStatsDirty = true;
	}

	private void RecalculateBuildStats()
	{
		EnsureBuildLoadout();
		_buildStats = BuildCatalog.CalculateStats(this, _buildLoadout);
		_buildStatsDirty = false;
		CurrentHealth = Mathf.Clamp(CurrentHealth, 0, _buildStats.MaxHealth);
	}

	private void MarkBuildChanged()
	{
		_buildStatsDirty = true;
		RecalculateBuildStats();
		RefreshNameplate();
	}

	private void MarkBaseStatsChanged()
	{
		_buildStatsDirty = true;
		if (_buildConfigured)
		{
			RecalculateBuildStats();
		}
	}

	public void GrantTraining(int amount)
	{
		Experience += Mathf.Max(amount, 0);
		while (Experience >= ExperienceToNextLevel)
		{
			Experience -= ExperienceToNextLevel;
			LevelUp();
		}

		RefreshNameplate();
	}

	public bool TryEvolve()
	{
		if (!CanEvolve)
		{
			return false;
		}

		EvolutionStage++;
		MaxHealth += 36 + EvolutionStage * 14;
		CurrentHealth = MaxHealth;
		Attack += 8 + EvolutionStage * 2;
		Defense += 6 + EvolutionStage * 2;
		AbilityRank++;
		MarkBaseStatsChanged();
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
		Attack += ActorKind == "monster" ? 2 : 1;
		Defense += ActorKind == "monster" ? 1 : 2;
		MarkBaseStatsChanged();
		RefreshNameplate();
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker)
	{
		if (_isDefeated)
		{
			return 0;
		}

		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(EffectiveDefense * 0.35f), 1);
		RememberAttacker(attacker);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		SpawnCombatEffect(mitigatedDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));

		if (CurrentHealth <= 0)
		{
			Defeat(attacker);
		}

		RefreshNameplate();
		return mitigatedDamage;
	}

	public int ReceiveHealing(int rawHealing)
	{
		if (_isDefeated)
		{
			return 0;
		}

		int missingHealth = Mathf.Max(EffectiveMaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnCombatEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.3f, 0.0f), 0.58f, 0.46f);
		RefreshNameplate();
		return healing;
	}

	public bool ReviveFromCaretaker(PlayerController followTarget)
	{
		if (!_isCaptured || !_isDefeated)
		{
			return false;
		}

		_isDefeated = false;
		_followTarget = followTarget;
		CurrentHealth = Mathf.Max(Mathf.RoundToInt(EffectiveMaxHealth * 0.65f), 1);
		Velocity = Vector3.Zero;
		Visible = _isInActiveParty;
		CollisionLayer = _defaultCollisionLayer;
		CollisionMask = _defaultCollisionMask;
		SetPhysicsProcess(_isInActiveParty);
		ApplyLivingPose();
		if (_isInActiveParty)
		{
			GlobalPosition = GetFollowDestination();
		}

		SpawnCombatEffect(LocaleText.T("effect.revive"), new Color(0.42f, 1.0f, 0.66f, 0.94f), GlobalPosition + new Vector3(0.0f, 1.25f, 0.0f), 0.9f, 0.76f);
		RefreshNameplate();
		return true;
	}

	private void CreateNameplate()
	{
		_nameplate = new Label3D
		{
			Name = "Nameplate",
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = false,
			FontSize = 20,
			PixelSize = 0.0075f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Width = 320.0f,
		};
		AddChild(_nameplate);
		RefreshNameplate();
	}

	private void RefreshNameplate()
	{
		if (_nameplate == null)
		{
			return;
		}

		string capturedText = _isDefeated
			? " [Dead]"
			: _isCaptured
			? _isInActiveParty ? LocaleText.T("actor.nameplate.active") : LocaleText.T("actor.nameplate.stored")
			: string.Empty;
		_nameplate.Text = $"{LocaleText.T("actor.level_prefix")}{Level} {LocalizedDisplayName}{capturedText}";
		_nameplate.Modulate = ActorKind == "monster"
			? new Color(1.0f, 0.34f, 0.26f)
			: new Color(0.64f, 0.86f, 1.0f);
		_nameplate.OutlineModulate = new Color(0.02f, 0.025f, 0.03f, 0.96f);
		UpdateNameplatePosition();
	}

	private void UpdateNameplatePosition()
	{
		if (_nameplate == null)
		{
			return;
		}

		float visualTop = GetVisualTopY(this);
		float fallbackTop = ActorKind == "monster" ? 2.2f : 2.05f;
		float labelY = Mathf.Max(visualTop + 0.38f, fallbackTop);
		_nameplate.Position = new Vector3(0.0f, labelY, 0.0f);
	}

	private float GetVisualTopY(Node node)
	{
		float topY = 0.0f;
		foreach (Node child in node.GetChildren())
		{
			if (child == _nameplate || child is CollisionShape3D)
			{
				continue;
			}

			if (child is MeshInstance3D meshInstance && meshInstance.Mesh != null)
			{
				topY = Mathf.Max(topY, GetMeshTopY(meshInstance));
			}

			topY = Mathf.Max(topY, GetVisualTopY(child));
		}

		return topY;
	}

	private float GetMeshTopY(MeshInstance3D meshInstance)
	{
		Aabb aabb = meshInstance.GetAabb();
		float topY = 0.0f;
		for (int x = 0; x <= 1; x++)
		{
			for (int y = 0; y <= 1; y++)
			{
				for (int z = 0; z <= 1; z++)
				{
					var corner = new Vector3(
						x == 0 ? aabb.Position.X : aabb.Position.X + aabb.Size.X,
						y == 0 ? aabb.Position.Y : aabb.Position.Y + aabb.Size.Y,
						z == 0 ? aabb.Position.Z : aabb.Position.Z + aabb.Size.Z
					);
					Vector3 actorLocalCorner = ToLocal(meshInstance.ToGlobal(corner));
					topY = Mathf.Max(topY, actorLocalCorner.Y);
				}
			}
		}

		return topY;
	}

	private void FollowCapturedTarget(Vector3 velocity, float step)
	{
		if (!_isInActiveParty || _followTarget == null || !IsInstanceValid(_followTarget))
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlideWithEffects(step);
			return;
		}

		if (TryUseSupportBuild(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.2f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		if (TryCompanionCombat(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.6f;
			Velocity = velocity;
			MoveAndSlideWithEffects(step);
			return;
		}

		UpdateSquadActivity(step);
		Vector3 destination = GetLivingSquadDestination();
		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;
		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);

		if (distanceToPlayer > 19.0f)
		{
			GlobalPosition = GetFollowDestination();
			Velocity = Vector3.Zero;
			ResetSquadActivity();
			return;
		}

		float followSpeed = GetLivingSquadMoveSpeed(distanceToPlayer);
		float stopDistance = _squadActivity == SquadActivity.Rest ? 0.85f : 0.55f;
		if (toDestination.Length() > stopDistance)
		{
			Vector3 direction = toDestination.Normalized();
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * followSpeed, followSpeed * 7.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * followSpeed, followSpeed * 7.0f * step);
			FaceDirection(direction, step);
		}
		else
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(GetLivingSquadLookDirection(destination), step);
		}

		Velocity = velocity;
		MoveAndSlideWithEffects(step);
	}

	private Vector3 GetFollowDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		return _followTarget.GlobalPosition + PlayerLocalToWorld(GetFormationLocalOffset());
	}

	private Vector3 GetLivingSquadDestination()
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return GlobalPosition;
		}

		Vector3 formationOffset = GetFormationLocalOffset();
		Vector3 localOffset = _squadActivity switch
		{
			SquadActivity.Guard => formationOffset * 0.72f + _squadActivityLocalOffset * 0.28f,
			SquadActivity.Scout => _squadActivityLocalOffset,
			SquadActivity.Gather => _squadActivityLocalOffset,
			SquadActivity.Roam => _squadActivityLocalOffset,
			SquadActivity.Rest => formationOffset * 0.82f + _squadActivityLocalOffset * 0.18f,
			_ => formationOffset,
		};

		return _followTarget.GlobalPosition + PlayerLocalToWorld(localOffset);
	}

	private void UpdateSquadActivity(float step)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_followTarget.GlobalPosition);
		if (distanceToPlayer > GetFormationRegroupDistance())
		{
			_squadActivity = SquadActivity.Follow;
			_squadActivityRemaining = 1.0f;
			_squadThinkRemaining = 1.2f;
			return;
		}

		_squadActivityRemaining = Mathf.Max(_squadActivityRemaining - step, 0.0f);
		_squadThinkRemaining = Mathf.Max(_squadThinkRemaining - step, 0.0f);
		if (_squadActivityRemaining > 0.0f || _squadThinkRemaining > 0.0f)
		{
			return;
		}

		ChooseSquadActivity();
	}

	private void ChooseSquadActivity()
	{
		BuildStats stats = CurrentBuildStats;
		float roll = _rng.Randf();
		_squadActivity = ChooseLivingSquadActivity(stats, roll);
		_squadActivityLocalOffset = MakeActivityLocalOffset(_squadActivity);
		_squadActivityRemaining = (float)_rng.RandfRange(2.4f, 6.8f);
		_squadThinkRemaining = (float)_rng.RandfRange(0.3f, 1.2f);
	}

	private SquadActivity ChooseLivingSquadActivity(BuildStats stats, float roll)
	{
		return stats.AiBehaviorId switch
		{
			BuildCatalog.AiFollowClosely => roll < 0.78f ? SquadActivity.Follow : SquadActivity.Rest,
			BuildCatalog.AiProtectPlayer or BuildCatalog.AiDefensive => roll < 0.58f ? SquadActivity.Guard : roll < 0.78f ? SquadActivity.Follow : SquadActivity.Rest,
			BuildCatalog.AiHealAllies => roll < 0.55f ? SquadActivity.Follow : roll < 0.82f ? SquadActivity.Guard : SquadActivity.Rest,
			BuildCatalog.AiGatherResources or BuildCatalog.AiLootNearby => roll < 0.62f ? SquadActivity.Gather : roll < 0.82f ? SquadActivity.Roam : SquadActivity.Follow,
			BuildCatalog.AiRoamFreely => roll < 0.54f ? SquadActivity.Roam : roll < 0.82f ? SquadActivity.Scout : SquadActivity.Follow,
			BuildCatalog.AiAggressive => roll < 0.48f ? SquadActivity.Scout : roll < 0.80f ? SquadActivity.Guard : SquadActivity.Roam,
			BuildCatalog.AiKeepDistance => roll < 0.46f ? SquadActivity.Scout : roll < 0.76f ? SquadActivity.Roam : SquadActivity.Follow,
			_ => roll < 0.36f ? SquadActivity.Follow : roll < 0.62f ? SquadActivity.Guard : roll < 0.82f ? SquadActivity.Roam : SquadActivity.Rest,
		};
	}

	private Vector3 MakeActivityLocalOffset(SquadActivity activity)
	{
		Vector3 formation = GetFormationLocalOffset();
		float side = _followSlot % 2 == 0 ? -1.0f : 1.0f;
		return activity switch
		{
			SquadActivity.Guard => formation + new Vector3(side * (float)_rng.RandfRange(0.45f, 1.15f), 0.0f, (float)_rng.RandfRange(-0.5f, 1.0f)),
			SquadActivity.Scout => new Vector3(side * (float)_rng.RandfRange(1.0f, 3.8f), 0.0f, (float)_rng.RandfRange(4.8f, 8.0f)),
			SquadActivity.Gather => RandomLocalRingOffset(3.4f, 6.4f),
			SquadActivity.Roam => RandomLocalRingOffset(3.0f, 7.2f),
			SquadActivity.Rest => formation + RandomLocalRingOffset(0.2f, 0.9f),
			_ => formation,
		};
	}

	private Vector3 RandomLocalRingOffset(float minRadius, float maxRadius)
	{
		float angle = (float)_rng.RandfRange(-Mathf.Pi, Mathf.Pi);
		float radius = (float)_rng.RandfRange(minRadius, maxRadius) * CurrentBuildStats.FollowDistanceMultiplier;
		return new Vector3(Mathf.Sin(angle) * radius, 0.0f, Mathf.Cos(angle) * radius);
	}

	private Vector3 GetFormationLocalOffset()
	{
		Vector3 offset = _followTarget != null && IsInstanceValid(_followTarget)
			? _followTarget.GetFormationLocalOffset(this)
			: Vector3.Zero;
		if (offset.LengthSquared() <= 0.001f)
		{
			offset = new Vector3(0.0f, 0.0f, 2.35f);
		}

		return offset * CurrentBuildStats.FollowDistanceMultiplier;
	}

	private Vector3 PlayerLocalToWorld(Vector3 localOffset)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return localOffset;
		}

		Vector3 forward = -_followTarget.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;
		forward = forward.LengthSquared() > 0.001f ? forward.Normalized() : Vector3.Forward;

		Vector3 right = _followTarget.GlobalTransform.Basis.X;
		right.Y = 0.0f;
		right = right.LengthSquared() > 0.001f ? right.Normalized() : Vector3.Right;
		return right * localOffset.X + forward * localOffset.Z;
	}

	private float GetLivingSquadMoveSpeed(float distanceToPlayer)
	{
		float multiplier = _squadActivity switch
		{
			SquadActivity.Scout => 2.55f,
			SquadActivity.Gather or SquadActivity.Roam => 2.25f,
			SquadActivity.Guard => 2.05f,
			SquadActivity.Rest => 1.35f,
			_ => 2.35f,
		};

		if (distanceToPlayer > 8.0f)
		{
			multiplier += 0.8f;
		}

		return Mathf.Max(EffectiveMoveSpeed * multiplier, 4.8f);
	}

	private Vector3 GetLivingSquadLookDirection(Vector3 destination)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			return destination - GlobalPosition;
		}

		return _squadActivity switch
		{
			SquadActivity.Guard or SquadActivity.Scout => GlobalPosition - _followTarget.GlobalPosition,
			SquadActivity.Gather or SquadActivity.Roam => destination - GlobalPosition,
			_ => _followTarget.GlobalPosition - GlobalPosition,
		};
	}

	private void ResetSquadActivity()
	{
		_squadActivity = SquadActivity.Follow;
		_squadActivityLocalOffset = GetFormationLocalOffset();
		_squadActivityRemaining = 0.0f;
		_squadThinkRemaining = (float)_rng.RandfRange(0.15f, 1.1f);
	}

	private float GetFormationRegroupDistance()
	{
		return 12.5f;
	}

	private void LevelUp()
	{
		Level++;
		MaxHealth += 14 + EvolutionStage * 4;
		CurrentHealth = MaxHealth;
		Attack += 3 + EvolutionStage;
		Defense += 2 + EvolutionStage;
		MarkBaseStatsChanged();
		CurrentHealth = EffectiveMaxHealth;
	}

	private bool TryUseSupportBuild(ref Vector3 velocity, float step)
	{
		BuildStats stats = CurrentBuildStats;
		if (!stats.HasHealSkill || stats.AiBehaviorId != BuildCatalog.AiHealAllies || _followTarget == null || !IsInstanceValid(_followTarget) || _attackCooldownRemaining > 0.0f)
		{
			return false;
		}

		int healing = Mathf.Max(Mathf.RoundToInt(stats.Attack * 0.58f + stats.Defense * 0.18f), 8);
		if (_followTarget.CurrentHealth < Mathf.RoundToInt(_followTarget.MaxHealth * 0.72f))
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(_followTarget.GlobalPosition - GlobalPosition, step);
			if (_followTarget.ReceiveHealing(healing) > 0)
			{
				PlayAttackAction(_followTarget.GlobalPosition, true);
				_attackCooldownRemaining = EffectiveAttackCooldown;
				return true;
			}
		}

		foreach (SimpleActor ally in _followTarget.ActiveParty)
		{
			if (ally == this || !IsInstanceValid(ally) || !ally.IsInActiveParty || ally.HealthRatio >= 0.68f)
			{
				continue;
			}

			velocity = SlowToStop(velocity, step);
			FaceDirection(ally.GlobalPosition - GlobalPosition, step);
			if (ally.ReceiveHealing(healing) > 0)
			{
				PlayAttackAction(ally.GlobalPosition, true);
				_attackCooldownRemaining = EffectiveAttackCooldown;
				return true;
			}
		}

		return false;
	}

	private bool TryCompanionCombat(ref Vector3 velocity, float step)
	{
		SimpleActor? target = GetCombatTarget();
		if (target == null)
		{
			_combatTarget = null;
			return false;
		}

		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		float distance = toTarget.Length();
		if (distance > EffectiveDetectionRadius * 1.25f)
		{
			_combatTarget = null;
			return false;
		}

		BuildStats stats = CurrentBuildStats;
		if (stats.AiBehaviorId == BuildCatalog.AiKeepDistance && distance < Mathf.Min(EffectiveAttackRange * 0.62f, 4.2f) && distance > 0.05f)
		{
			Vector3 retreatDirection = -toTarget.Normalized();
			float retreatSpeed = Mathf.Max(EffectiveMoveSpeed * 2.0f, 4.0f);
			velocity.X = Mathf.MoveToward(velocity.X, retreatDirection.X * retreatSpeed, retreatSpeed * 8.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, retreatDirection.Z * retreatSpeed, retreatSpeed * 8.0f * step);
			FaceDirection(toTarget, step);
			return true;
		}

		if (distance <= EffectiveAttackRange)
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(toTarget, step);
			AttackActor(target);
			return true;
		}

		Vector3 direction = toTarget.Normalized();
		float combatSpeedMultiplier = stats.AiBehaviorId == BuildCatalog.AiAggressive ? 2.35f : 2.05f;
		float combatSpeed = Mathf.Max(EffectiveMoveSpeed * combatSpeedMultiplier, 4.2f);
		velocity.X = Mathf.MoveToward(velocity.X, direction.X * combatSpeed, combatSpeed * 8.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * combatSpeed, combatSpeed * 8.0f * step);
		FaceDirection(direction, step);
		return true;
	}

	private SimpleActor? GetCombatTarget()
	{
		if (_followTarget != null && IsInstanceValid(_followTarget))
		{
			SimpleActor? focusedTarget = _followTarget.FocusedTarget;
			if (focusedTarget != null && IsValidCommandTarget(focusedTarget))
			{
				float distanceFromSelf = GlobalPosition.DistanceTo(focusedTarget.GlobalPosition);
				float distanceFromPlayer = _followTarget.GlobalPosition.DistanceTo(focusedTarget.GlobalPosition);
				float commandRadius = Mathf.Max(EffectiveDetectionRadius * 1.85f, 18.0f);
				if (distanceFromSelf <= commandRadius || distanceFromPlayer <= commandRadius)
				{
					_combatTarget = focusedTarget;
					return focusedTarget;
				}
			}
		}

		if (_combatTarget != null && IsValidCommandTarget(_combatTarget) && _combatTarget.IsHostileToPlayer && GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) <= EffectiveDetectionRadius * 1.35f)
		{
			return _combatTarget;
		}

		BuildStats stats = CurrentBuildStats;
		Vector3 anchor = GlobalPosition;
		bool playerAnchored = stats.AiBehaviorId is BuildCatalog.AiProtectPlayer or BuildCatalog.AiDefensive or BuildCatalog.AiHealAllies or BuildCatalog.AiFollowClosely;
		if (playerAnchored && _followTarget != null && IsInstanceValid(_followTarget))
		{
			anchor = _followTarget.GlobalPosition;
		}

		float searchRadius = EffectiveDetectionRadius;
		if (stats.AiBehaviorId is BuildCatalog.AiGatherResources or BuildCatalog.AiLootNearby or BuildCatalog.AiRoamFreely)
		{
			searchRadius *= 0.58f;
		}

		SimpleActor? selected = null;
		float bestScore = float.MaxValue;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is not SimpleActor actor || !actor.IsHostileToPlayer)
			{
				continue;
			}

			float distanceFromSelf = GlobalPosition.DistanceTo(actor.GlobalPosition);
			float distanceFromAnchor = anchor.DistanceTo(actor.GlobalPosition);
			if (distanceFromSelf > searchRadius && distanceFromAnchor > searchRadius)
			{
				continue;
			}

			float score = stats.AiBehaviorId switch
			{
				BuildCatalog.AiAttackBossFirst => -actor.Level * 1000.0f + distanceFromSelf,
				BuildCatalog.AiAttackLowestHp => actor.HealthRatio * 1000.0f + distanceFromSelf * 0.04f,
				BuildCatalog.AiProtectPlayer or BuildCatalog.AiDefensive or BuildCatalog.AiHealAllies or BuildCatalog.AiFollowClosely => distanceFromAnchor,
				_ => distanceFromSelf,
			};

			if (score < bestScore)
			{
				selected = actor;
				bestScore = score;
			}
		}

		_combatTarget = selected;
		return selected;
	}

	private bool IsValidCommandTarget(SimpleActor? actor)
	{
		return actor != null && IsInstanceValid(actor) && !actor.IsDefeated && !actor.IsCaptured && actor != this;
	}

	private void AttackActor(SimpleActor target)
	{
		if (_attackCooldownRemaining > 0.0f)
		{
			return;
		}

		BuildStats stats = CurrentBuildStats;
		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int damage = Mathf.Max(stats.Attack + roleBonus + affinityBonus, 1);
		if (_rng.Randf() < stats.CritChance)
		{
			damage = Mathf.RoundToInt(damage * 1.55f);
		}

		PlayAttackAction(target.GlobalPosition, false);
		int dealtDamage = target.ReceiveDamage(damage, this);
		if (stats.LifeStealPercent > 0.0f && dealtDamage > 0)
		{
			ReceiveHealing(Mathf.RoundToInt(dealtDamage * stats.LifeStealPercent));
		}

		_attackCooldownRemaining = EffectiveAttackCooldown;
	}

	private bool TryAttackPlayer(Node3D player, Vector3 velocity, float step)
	{
		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0.0f;
		if (toPlayer.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toPlayer, step);
		if (_attackCooldownRemaining <= 0.0f && player is PlayerController playerController)
		{
			SpawnPlayerAttackCue(player.GlobalPosition);
			PlayAttackAction(player.GlobalPosition, false);
			playerController.ReceiveDamage(EffectiveAttack, this);
			_attackCooldownRemaining = EffectiveAttackCooldown;
		}

		MoveAndSlideWithEffects(step);
		return true;
	}

	private bool TryAttackActorTarget(SimpleActor target, Vector3 velocity, float step)
	{
		Vector3 toTarget = target.GlobalPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		if (toTarget.Length() > EffectiveAttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toTarget, step);
		AttackActor(target);
		MoveAndSlideWithEffects(step);
		return true;
	}

	private void RememberAttacker(SimpleActor? attacker)
	{
		if (ActorKind != "monster" || attacker == null || !IsInstanceValid(attacker) || attacker == this || attacker.IsDefeated)
		{
			return;
		}

		_retaliationTarget = attacker;
		_retaliationTargetRemaining = 8.0f;
		_combatTarget = attacker;
	}

	private bool TryGetRetaliationTarget(out SimpleActor target)
	{
		target = null!;
		if (_retaliationTargetRemaining <= 0.0f || _retaliationTarget == null || !IsInstanceValid(_retaliationTarget) || _retaliationTarget.IsDefeated)
		{
			_retaliationTarget = null;
			return false;
		}

		if (GlobalPosition.DistanceTo(_retaliationTarget.GlobalPosition) > ChaseRadius * 1.65f)
		{
			_retaliationTarget = null;
			_retaliationTargetRemaining = 0.0f;
			return false;
		}

		target = _retaliationTarget;
		return true;
	}

	private void Defeat(SimpleActor? attacker)
	{
		_isDefeated = true;
		Velocity = Vector3.Zero;
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		_retaliationTarget = null;
		_retaliationTargetRemaining = 0.0f;
		_combatTarget = null;

		if (_isCaptured)
		{
			Affinity = Mathf.Max(Affinity - 12, 0);
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
			Visible = true;
			SetPhysicsProcess(false);
			ApplyDefeatedPose();
			SpawnCombatEffect(LocaleText.F("effect.affinity_loss", 12), new Color(1.0f, 0.28f, 0.22f, 0.92f), GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f), 0.95f, 0.72f);
			RefreshNameplate();
			return;
		}

		_isInActiveParty = false;
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);

		if (attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.PostSystemMessage(LocaleText.F("system.combat.defeated", attacker.LocalizedDisplayName, LocalizedDisplayName), new Color(1.0f, 0.70f, 0.42f));
			attacker._followTarget.GrantCombatExperience(ExperienceReward);
			if (ActorKind == "monster")
			{
				DropMonsterLoot(attacker._followTarget);
			}
		}
	}

	private void DropMonsterLoot(PlayerController player)
	{
		Vector3 origin = GlobalPosition;
		int goldAmount = Mathf.Max(GoldReward + _rng.RandiRange(1, Mathf.Max(Level + 2, 3)), 1);
		SpawnWorldDrop(origin + RandomDropOffset(0.45f), string.Empty, 1, goldAmount);
		SpawnWorldDrop(origin + RandomDropOffset(0.78f), "monster_trophy", 1, 0);

		if (_rng.Randf() < 0.36f)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.05f), PickEquipmentDropId(), 1, 0);
		}

		if (_rng.Randf() < 0.22f)
		{
			SpawnWorldDrop(origin + RandomDropOffset(1.18f), PickGemDropId(), 1, 0);
		}

		player.PostSystemMessage(LocaleText.F("system.drop.loot", LocalizedDisplayName), new Color(1.0f, 0.86f, 0.48f));
	}

	private void SpawnWorldDrop(Vector3 position, string itemId, int amount, int goldAmount)
	{
		var drop = new WorldDrop
		{
			ItemId = itemId,
			Amount = Mathf.Max(amount, 1),
			GoldAmount = Mathf.Max(goldAmount, 0),
		};

		Node parent = GetTree().CurrentScene ?? GetParent();
		parent.AddChild(drop);
		drop.GlobalPosition = new Vector3(position.X, 0.04f, position.Z);
	}

	private Vector3 RandomDropOffset(float radius)
	{
		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(radius * 0.35f, radius);
		return new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}

	private string PickEquipmentDropId()
	{
		EquipmentSlot[] slots =
		{
			EquipmentSlot.Helmet,
			EquipmentSlot.Weapon,
			EquipmentSlot.Armor,
			EquipmentSlot.Accessory,
		};
		EquipmentSlot slot = slots[_rng.RandiRange(0, slots.Length - 1)];
		var definitions = BuildCatalog.GetEquipmentDefinitions(slot);
		return definitions[_rng.RandiRange(0, definitions.Count - 1)].Id;
	}

	private string PickGemDropId()
	{
		int kind = _rng.RandiRange(0, 2);
		if (kind == 0)
		{
			var gems = BuildCatalog.GetAttributeGemDefinitions();
			return PickNonFreeAttributeGem(gems);
		}

		if (kind == 1)
		{
			var gems = BuildCatalog.GetSkillGemDefinitions();
			return PickNonFreeSkillGem(gems);
		}

		var aiGems = BuildCatalog.GetAiGemDefinitions();
		return aiGems[_rng.RandiRange(0, aiGems.Count - 1)].Id;
	}

	private int PickValidGemIndex(int count)
	{
		return Mathf.Clamp(_rng.RandiRange(1, Mathf.Max(count - 1, 1)), 0, Mathf.Max(count - 1, 0));
	}

	private string PickNonFreeAttributeGem(System.Collections.Generic.List<AttributeGemDefinition> gems)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			string id = gems[PickValidGemIndex(gems.Count)].Id;
			if (!BuildCatalog.IsFreeItem(id))
			{
				return id;
			}
		}

		return "gem.attribute.fire";
	}

	private string PickNonFreeSkillGem(System.Collections.Generic.List<SkillGemDefinition> gems)
	{
		for (int attempt = 0; attempt < 12; attempt++)
		{
			string id = gems[PickValidGemIndex(gems.Count)].Id;
			if (!BuildCatalog.IsFreeItem(id))
			{
				return id;
			}
		}

		return "gem.skill.fireball";
	}

	private void ApplyDefeatedPose()
	{
		SetExternalAnimationState("death");
		ResetAttackVisualScale();
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 88.0f);
		SetChildRotation("Head", ActorKind == "monster" ? new Vector3(22.0f, 0.0f, -16.0f) : new Vector3(28.0f, 0.0f, -12.0f));
		SetChildRotation("TailBase", new Vector3(82.0f, 0.0f, 0.0f));
	}

	private void ApplyLivingPose()
	{
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y, 0.0f);
		Scale = Vector3.One;
		ResetAttackVisualScale();
		SetExternalAnimationState("idle");
	}

	private Color GetAttackColor()
	{
		BuildStats stats = CurrentBuildStats;
		if (stats.DamageElementId != "physical")
		{
			return stats.AttackColor;
		}

		return CombatRole switch
		{
			"Tank" => new Color(1.0f, 0.78f, 0.28f, 0.9f),
			"Ranged" => new Color(0.34f, 0.78f, 1.0f, 0.9f),
			"Support" => new Color(0.42f, 1.0f, 0.62f, 0.9f),
			"Gatherer" => new Color(0.72f, 0.92f, 0.38f, 0.9f),
			"Builder" => new Color(0.94f, 0.64f, 0.36f, 0.9f),
			_ => new Color(1.0f, 0.42f, 0.16f, 0.92f),
		};
	}

	private void SpawnSwingEffect(Vector3 targetPosition)
	{
		Vector3 position = GlobalPosition + (targetPosition - GlobalPosition) * 0.5f;
		position.Y = Mathf.Max(GlobalPosition.Y, targetPosition.Y) + 0.95f;
		SpawnCombatEffect(string.Empty, GetAttackColor(), position, 0.34f, 0.36f);
	}

	private void PlayAttackAction(Vector3 targetPosition, bool isHealing)
	{
		SetExternalAnimationState(GetExternalAttackAnimationState(isHealing), 0.48f);
		AnimateAttackPose();
		SpawnAttackProjectile(targetPosition, isHealing);
		if (!UsesProjectileAttack(isHealing))
		{
			SpawnSwingEffect(targetPosition);
		}
	}

	private string GetExternalAttackAnimationState(bool isHealing)
	{
		if (isHealing || CombatRole == "Support")
		{
			return "cast";
		}

		return UsesArrowProjectile(false) ? "shoot" : "attack";
	}

	private void AnimateAttackPose()
	{
		if (_attackPoseTween != null && IsInstanceValid(_attackPoseTween))
		{
			_attackPoseTween.Kill();
		}

		ResetAttackVisualScale();
		Node3D? visualTarget = GetAttackVisualTarget();
		if (visualTarget == null)
		{
			return;
		}

		_attackPoseTarget = visualTarget;
		_attackPoseBaseScale = visualTarget.Scale;
		_attackPoseTween = CreateTween();
		_attackPoseTween.SetTrans(Tween.TransitionType.Sine);
		_attackPoseTween.SetEase(Tween.EaseType.Out);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale * new Vector3(1.12f, 0.90f, 1.20f), 0.075f);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale * new Vector3(0.94f, 1.08f, 0.92f), 0.085f);
		_attackPoseTween.TweenProperty(visualTarget, "scale", _attackPoseBaseScale, 0.13f);
	}

	private Node3D? GetAttackVisualTarget()
	{
		return GetNodeOrNull<Node3D>("ExternalModel")
			?? GetNodeOrNull<Node3D>("BodyCore")
			?? GetNodeOrNull<Node3D>("Torso")
			?? GetNodeOrNull<Node3D>("Head");
	}

	private void ResetAttackVisualScale()
	{
		if (_attackPoseTarget != null && IsInstanceValid(_attackPoseTarget))
		{
			_attackPoseTarget.Scale = _attackPoseBaseScale;
		}

		_attackPoseTarget = null;
		_attackPoseBaseScale = Vector3.One;
	}

	private void SpawnAttackProjectile(Vector3 targetPosition, bool isHealing)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		bool isMelee = !UsesProjectileAttack(isHealing);
		Vector3 toTarget = targetPosition - GlobalPosition;
		toTarget.Y = 0.0f;
		Vector3 forward = toTarget.LengthSquared() > 0.001f ? toTarget.Normalized() : -GlobalTransform.Basis.Z;
		Color color = isHealing ? new Color(0.36f, 1.0f, 0.54f, 0.92f) : GetAttackColor();
		float travelDistance = GlobalPosition.DistanceTo(targetPosition);

		var projectile = new AttackProjectile
		{
			StartPosition = GlobalPosition + Vector3.Up * (isMelee ? 1.04f : 1.22f) + forward * 0.44f,
			EndPosition = targetPosition + Vector3.Up * (isMelee ? 1.02f : 1.16f),
			EffectColor = color,
			IsMelee = isMelee,
			IsHealing = isHealing,
			IsArrow = UsesArrowProjectile(isHealing),
			Radius = isMelee ? 0.24f : 0.20f,
			Lifetime = isMelee
				? 0.16f
				: Mathf.Clamp(travelDistance / 18.0f, 0.24f, 0.48f),
		};
		parent.AddChild(projectile);
	}

	private bool UsesProjectileAttack(bool isHealing)
	{
		return isHealing || IsRangedCombatant;
	}

	private bool UsesArrowProjectile(bool isHealing)
	{
		return !isHealing && ActorKind == "npc" && CombatRole == "Ranged";
	}

	private void SpawnPlayerAttackCue(Vector3 playerPosition)
	{
		Color color = GetAttackColor();
		SpawnCombatEffect("!", color, GlobalPosition + new Vector3(0.0f, 1.75f, 0.0f), 0.48f, 0.68f);
		SpawnCombatEffect(string.Empty, color, playerPosition + new Vector3(0.0f, 1.12f, 0.0f), 0.42f, 0.72f);
	}

	private void SpawnCombatEffect(int damage, Color color)
	{
		SpawnCombatEffect(damage.ToString(), color, GlobalPosition + new Vector3(0.0f, 1.1f, 0.0f), 0.52f, 0.55f);
	}

	private void SpawnCombatEffect(string text, Color color, Vector3 position, float lifetime, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		var effect = new CombatEffect
		{
			Text = text,
			EffectColor = color,
			Lifetime = lifetime,
			Radius = radius,
		};
		parent.AddChild(effect);
		effect.GlobalPosition = position;
	}

	private Vector3 SlowToStop(Vector3 velocity, float step)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0.0f, MoveSpeed * 5.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, MoveSpeed * 5.0f * step);
		return velocity;
	}

	private void MoveAndSlideWithEffects(float step)
	{
		MoveAndSlide();
		UpdateMovementEffects(step);
		UpdateMovementAnimation(step);
		StabilizeExternalModelRootMotion();
	}

	private void UpdateMovementEffects(float step)
	{
		_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining - step, 0.0f);
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		if (!IsOnFloor() || speed < 0.85f || _footstepEffectRemaining > 0.0f)
		{
			return;
		}

		bool isFastStep = speed > Mathf.Max(EffectiveMoveSpeed * 1.65f, 3.6f);
		SpawnMovementDust(planarVelocity.Normalized(), speed, isFastStep);
		_footstepEffectRemaining = isFastStep ? 0.16f : 0.28f;
	}

	private void SpawnMovementDust(Vector3 moveDirection, float speed, bool isFastStep)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		Vector3 back = -moveDirection;
		Vector3 side = new(-moveDirection.Z, 0.0f, moveDirection.X);
		float footSide = Mathf.Sin((Time.GetTicksMsec() + GetInstanceId()) * 0.016f) >= 0.0f ? 1.0f : -1.0f;
		Color color = ActorKind == "monster"
			? (isFastStep ? new Color(0.82f, 0.42f, 0.32f, 0.70f) : new Color(0.62f, 0.44f, 0.36f, 0.58f))
			: (isFastStep ? new Color(0.78f, 0.86f, 0.92f, 0.66f) : new Color(0.62f, 0.70f, 0.72f, 0.54f));

		var dust = new MovementDustEffect
		{
			DustColor = color,
			Radius = isFastStep ? 0.22f : 0.15f,
			Lifetime = isFastStep ? 0.34f : 0.42f,
			IsFastStep = isFastStep,
			DirectionYaw = Mathf.RadToDeg(Mathf.Atan2(-moveDirection.X, -moveDirection.Z)),
		};
		parent.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + back * Mathf.Clamp(speed * 0.032f, 0.14f, 0.40f) + side * footSide * 0.15f + Vector3.Up * 0.04f;
	}

	private void UpdateMovementAnimation(float step)
	{
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		float speedReference = Mathf.Max(EffectiveMoveSpeed * 2.4f, 4.5f);
		float moveRatio = Mathf.Clamp(speed / speedReference, 0.0f, 1.0f);
		bool isMoving = speed > 0.18f && IsOnFloor() && !_isDefeated;
		float phaseSpeed = Mathf.Lerp(5.5f, 10.8f, moveRatio);

		if (isMoving)
		{
			_movementAnimationPhase += step * phaseSpeed;
		}
		else
		{
			_movementAnimationPhase = Mathf.Lerp(_movementAnimationPhase, 0.0f, Mathf.Min(step * 8.0f, 1.0f));
		}

		UpdateExternalMovementAnimation(step, isMoving, moveRatio);

		if (ActorKind == "monster")
		{
			UpdateMonsterMovementAnimation(isMoving, moveRatio);
		}
		else
		{
			UpdateHumanoidMovementAnimation(isMoving, moveRatio);
		}
	}

	private void UpdateHumanoidMovementAnimation(bool isMoving, float moveRatio)
	{
		float swing = Mathf.Sin(_movementAnimationPhase);
		float counterSwing = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float intensity = isMoving ? Mathf.Lerp(0.38f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(swing) * 0.035f * intensity;
		float lean = moveRatio * -3.6f;

		SetChildPosition("Body", new Vector3(0.0f, 1.02f + bob, 0.0f));
		SetChildPosition("Tunic", new Vector3(0.0f, 1.04f + bob, -0.26f));
		SetChildRotation("Body", new Vector3(lean, 0.0f, swing * 1.4f * intensity));
		SetChildRotation("Head", new Vector3(Mathf.Abs(swing) * 2.0f * intensity, 0.0f, -swing * 1.2f * intensity));

		SetChildRotation("LeftLeg", new Vector3(swing * 25.0f * intensity, 0.0f, -1.8f * intensity));
		SetChildRotation("RightLeg", new Vector3(counterSwing * 25.0f * intensity, 0.0f, 1.8f * intensity));
		SetChildPosition("LeftBoot", new Vector3(-0.14f, 0.06f + Mathf.Max(counterSwing, 0.0f) * 0.06f * intensity, -0.05f + swing * 0.045f * intensity));
		SetChildPosition("RightBoot", new Vector3(0.14f, 0.06f + Mathf.Max(swing, 0.0f) * 0.06f * intensity, -0.05f + counterSwing * 0.045f * intensity));

		SetChildRotation("LeftArm", new Vector3(counterSwing * 22.0f * intensity, 0.0f, -9.0f - swing * 4.0f * intensity));
		SetChildRotation("RightArm", new Vector3(swing * 22.0f * intensity, 0.0f, 9.0f - counterSwing * 4.0f * intensity));
		SetChildPosition("LeftGlove", new Vector3(-0.44f, 0.66f + counterSwing * 0.055f * intensity, -0.03f - counterSwing * 0.075f * intensity));
		SetChildPosition("RightGlove", new Vector3(0.44f, 0.66f + swing * 0.055f * intensity, -0.03f - swing * 0.075f * intensity));
		SetChildRotation("Cape", new Vector3(-8.0f + Mathf.Abs(swing) * 6.0f * intensity, 0.0f, -swing * 2.2f * intensity));
	}

	private void UpdateMonsterMovementAnimation(bool isMoving, float moveRatio)
	{
		float phaseA = Mathf.Sin(_movementAnimationPhase);
		float phaseB = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float liftA = Mathf.Max(phaseA, 0.0f);
		float liftB = Mathf.Max(phaseB, 0.0f);
		float intensity = isMoving ? Mathf.Lerp(0.42f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(phaseA) * 0.045f * intensity;
		float lean = moveRatio * -4.8f;

		SetChildPosition("BodyCore", new Vector3(0.0f, 0.74f + bob, 0.10f));
		SetChildPosition("Head", new Vector3(0.0f, 1.18f + bob * 0.65f, -0.92f));
		SetChildRotation("BodyCore", new Vector3(lean + phaseA * 2.0f * intensity, 0.0f, phaseA * 2.2f * intensity));
		SetChildRotation("Head", new Vector3(phaseB * 3.0f * intensity, 0.0f, -phaseA * 1.5f * intensity));

		SetChildRotation("LeftForeLeg", new Vector3(7.0f + phaseA * 26.0f * intensity, 0.0f, -7.0f));
		SetChildRotation("RightForeLeg", new Vector3(7.0f + phaseB * 26.0f * intensity, 0.0f, 7.0f));
		SetChildRotation("LeftBackLeg", new Vector3(-8.0f + phaseB * 24.0f * intensity, 0.0f, -8.0f));
		SetChildRotation("RightBackLeg", new Vector3(-8.0f + phaseA * 24.0f * intensity, 0.0f, 8.0f));

		SetChildPosition("LeftFrontPaw", new Vector3(-0.42f, 0.13f + liftA * 0.08f * intensity, -0.70f + phaseA * 0.055f * intensity));
		SetChildPosition("RightFrontPaw", new Vector3(0.42f, 0.13f + liftB * 0.08f * intensity, -0.70f + phaseB * 0.055f * intensity));
		SetChildPosition("LeftBackPaw", new Vector3(-0.46f, 0.13f + liftB * 0.07f * intensity, 0.68f + phaseB * 0.05f * intensity));
		SetChildPosition("RightBackPaw", new Vector3(0.46f, 0.13f + liftA * 0.07f * intensity, 0.68f + phaseA * 0.05f * intensity));

		SetChildRotation("TailBase", new Vector3(64.0f + Mathf.Abs(phaseA) * 5.0f * intensity, phaseA * 9.0f * intensity, 0.0f));
		SetChildPosition("TailTip", new Vector3(phaseA * 0.05f * intensity, 0.38f + bob * 0.45f, 1.42f));
	}

	private void UpdateExternalMovementAnimation(float step, bool isMoving, float moveRatio)
	{
		if (_isDefeated)
		{
			SetExternalAnimationState("death");
			return;
		}

		if (_externalAnimationLockRemaining > 0.0f)
		{
			_externalAnimationLockRemaining = Mathf.Max(_externalAnimationLockRemaining - step, 0.0f);
			return;
		}

		string state = isMoving
			? moveRatio > 0.58f ? "run" : "walk"
			: "idle";
		SetExternalAnimationState(state);
	}

	private void SetExternalAnimationState(string state, float lockDuration = 0.0f)
	{
		bool played = ExternalModelLibrary.TryPlayActorAnimation(this, state);
		if (played)
		{
			_externalAnimationState = state;
			StabilizeExternalModelRootMotion();
		}

		if (played && lockDuration > 0.0f)
		{
			_externalAnimationLockRemaining = lockDuration;
		}
	}

	private void SetChildPosition(string nodeName, Vector3 position)
	{
		if (GetNodeOrNull<Node3D>(nodeName) is Node3D node)
		{
			node.Position = position;
		}
	}

	private void SetChildRotation(string nodeName, Vector3 rotationDegrees)
	{
		if (GetNodeOrNull<Node3D>(nodeName) is Node3D node)
		{
			node.RotationDegrees = rotationDegrees;
		}
	}

	private void StabilizeExternalModelRootMotion()
	{
		Node3D? model = GetNodeOrNull<Node3D>("ExternalModel");
		if (model == null)
		{
			_externalModelRootMotionNode = null;
			return;
		}

		model.Position = Vector3.Zero;
		model.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);

		Node3D? rootMotionNode = _externalModelRootMotionNode;
		if (rootMotionNode == null || !IsInstanceValid(rootMotionNode))
		{
			rootMotionNode = FindDescendantNode3D(model, "root") ?? FindDescendantNode3D(model, "Root");
			_externalModelRootMotionNode = rootMotionNode;
		}

		if (rootMotionNode == null)
		{
			return;
		}

		rootMotionNode.Position = Vector3.Zero;
		rootMotionNode.Rotation = Vector3.Zero;
	}

	private static Node3D? FindDescendantNode3D(Node node, string nodeName)
	{
		foreach (Node child in node.GetChildren())
		{
			if (child is Node3D childNode3D && childNode3D.Name == nodeName)
			{
				return childNode3D;
			}

			Node3D? found = FindDescendantNode3D(child, nodeName);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private void FaceDirection(Vector3 direction, float step)
	{
		if (direction.LengthSquared() == 0.0f)
		{
			return;
		}

		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, Mathf.Min(step * 8.0f, 1.0f));
		Rotation = rotation;
	}

	private Vector3 PickWanderTarget()
	{
		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(2.0f, WanderRadius);
		return HomePosition + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}
}
