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
	private SimpleActor? _combatTarget;
	private Label3D? _nameplate;
	private SquadActivity _squadActivity = SquadActivity.Follow;
	private Vector3 _squadActivityLocalOffset = Vector3.Zero;
	private float _squadActivityRemaining;
	private float _squadThinkRemaining;
	private CompanionBuildLoadout _buildLoadout = new();
	private BuildStats _buildStats = new();
	private bool _buildConfigured;
	private bool _buildStatsDirty = true;

	public bool CanBeCaptured => !_isCaptured && !_isDefeated;
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
	public string CombatSummary => LocaleText.F("combat.summary", CombatRoleName, LocalizedPersonality, Affinity);
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
		Vector3 velocity = Velocity;

		if (_isDefeated)
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlide();
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

		if (!chasing)
		{
			_waitTime = Mathf.Max(_waitTime - step, 0.0f);
			if (_waitTime > 0.0f)
			{
				Velocity = SlowToStop(velocity, step);
				MoveAndSlide();
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
		MoveAndSlide();
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

	public void DeployToParty(PlayerController followTarget, int followSlot)
	{
		_followTarget = followTarget;
		_followSlot = followSlot;
		_isInActiveParty = true;
		Visible = true;
		SetPhysicsProcess(true);
		CollisionLayer = _defaultCollisionLayer;
		CollisionMask = _defaultCollisionMask;
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		ResetSquadActivity();
		GlobalPosition = GetFollowDestination();
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

		string capturedText = _isCaptured
			? _isInActiveParty ? LocaleText.T("actor.nameplate.active") : LocaleText.T("actor.nameplate.stored")
			: string.Empty;
		_nameplate.Text = $"{LocaleText.T("actor.level_prefix")}{Level} {LocalizedDisplayName}{capturedText}";
		_nameplate.Modulate = ActorKind == "monster"
			? new Color(1.0f, 0.34f, 0.26f)
			: new Color(0.64f, 0.86f, 1.0f);
		_nameplate.OutlineModulate = new Color(0.02f, 0.025f, 0.03f, 0.96f);
	}

	private void FollowCapturedTarget(Vector3 velocity, float step)
	{
		if (!_isInActiveParty || _followTarget == null || !IsInstanceValid(_followTarget))
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlide();
			return;
		}

		if (TryUseSupportBuild(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.2f;
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		if (TryCompanionCombat(ref velocity, step))
		{
			_squadActivity = SquadActivity.Follow;
			_squadThinkRemaining = 1.6f;
			Velocity = velocity;
			MoveAndSlide();
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
		MoveAndSlide();
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
		if (_combatTarget != null && IsInstanceValid(_combatTarget) && _combatTarget.IsHostileToPlayer && GlobalPosition.DistanceTo(_combatTarget.GlobalPosition) <= EffectiveDetectionRadius * 1.35f)
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

		SpawnSwingEffect(target.GlobalPosition);
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
			SpawnSwingEffect(player.GlobalPosition);
			playerController.ReceiveDamage(EffectiveAttack, this);
			_attackCooldownRemaining = EffectiveAttackCooldown;
		}

		MoveAndSlide();
		return true;
	}

	private void Defeat(SimpleActor? attacker)
	{
		_isDefeated = true;
		_isInActiveParty = false;
		Velocity = Vector3.Zero;
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		CollisionLayer = 0;
		CollisionMask = 0;
		Visible = false;
		SetPhysicsProcess(false);

		if (attacker?._followTarget != null && IsInstanceValid(attacker._followTarget))
		{
			attacker._followTarget.GrantCombatExperience(ExperienceReward);
		}
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
