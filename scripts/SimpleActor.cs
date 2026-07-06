using Godot;

public partial class SimpleActor : CharacterBody3D
{
	[Export] public string ActorKind { get; set; } = "npc";
	[Export] public float MoveSpeed { get; set; } = 1.6f;
	[Export] public float WanderRadius { get; set; } = 10.0f;
	[Export] public float ChaseRadius { get; set; } = 20.0f;
	[Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;
	[Export] public string DisplayName { get; set; } = "旅人";
	[Export] public int Level { get; set; } = 1;
	[Export] public int MaxHealth { get; set; } = 100;
	[Export] public int CurrentHealth { get; set; } = 100;
	[Export] public int Attack { get; set; } = 10;
	[Export] public int Defense { get; set; } = 6;
	[Export] public int ExperienceReward { get; set; } = 6;
	[Export] public int GoldReward { get; set; } = 2;
	[Export] public int Experience { get; set; }
	[Export] public int EvolutionStage { get; set; }
	[Export] public string SpecialAbility { get; set; } = "無";
	[Export] public int AbilityRank { get; set; } = 1;
	[Export] public string CombatRole { get; set; } = "DPS";
	[Export] public string Personality { get; set; } = "沉著";
	[Export] public string PassiveAbility { get; set; } = "無";
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

	public bool CanBeCaptured => !_isCaptured && !_isDefeated;
	public bool IsCaptured => _isCaptured;
	public bool IsInActiveParty => _isInActiveParty;
	public bool IsDefeated => _isDefeated;
	public bool IsHostileToPlayer => !_isCaptured && !_isDefeated && ActorKind == "monster";
	public string TypeName => ActorKind == "monster" ? "怪物" : "NPC";
	public string StateName => _isDefeated ? "擊倒" : _isCaptured ? _isInActiveParty ? "出戰中" : "收藏中" : ActorKind == "monster" ? "敵對" : "中立";
	public string GrowthName => EvolutionStage <= 0 ? "初階" : EvolutionStage == 1 ? "進化 I" : EvolutionStage == 2 ? "進化 II" : "完全體";
	public string CombatRoleName => CombatRole switch
	{
		"Tank" => "坦克",
		"Ranged" => "遠程",
		"Support" => "支援",
		"Gatherer" => "採集",
		"Builder" => "建造",
		_ => "輸出",
	};
	public string CombatSummary => $"{CombatRoleName} / {Personality} / 親密 {Affinity}";
	public int ExperienceToNextLevel => 35 + Level * 18 + EvolutionStage * 20;
	public bool CanEvolve => EvolutionStage < 3 && Level >= (EvolutionStage + 1) * 5;
	public float HealthRatio => MaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)MaxHealth, 0.0f, 1.0f);

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
		AddToGroup(ActorKind == "monster" ? "monsters" : "npcs");
		CreateNameplate();
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
		Velocity = Vector3.Zero;
		CurrentHealth = Mathf.Max(CurrentHealth, Mathf.RoundToInt(MaxHealth * 0.45f));
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
		RefreshNameplate();
		return true;
	}

	public void EnhanceAbility()
	{
		if (SpecialAbility == "無")
		{
			SpecialAbility = ActorKind == "monster" ? "野性爆發" : "戰術支援";
		}

		AbilityRank++;
		Attack += ActorKind == "monster" ? 2 : 1;
		Defense += ActorKind == "monster" ? 1 : 2;
		RefreshNameplate();
	}

	public int ReceiveDamage(int rawDamage, SimpleActor? attacker)
	{
		if (_isDefeated)
		{
			return 0;
		}

		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(Defense * 0.35f), 1);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		SpawnCombatEffect(mitigatedDamage, attacker?.GetAttackColor() ?? new Color(1.0f, 0.5f, 0.22f, 0.92f));

		if (CurrentHealth <= 0)
		{
			Defeat(attacker);
		}

		RefreshNameplate();
		return mitigatedDamage;
	}

	private void CreateNameplate()
	{
		_nameplate = new Label3D
		{
			Name = "Nameplate",
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = true,
			NoDepthTest = false,
			FontSize = 22,
			PixelSize = 0.0085f,
			OutlineSize = 7,
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

		string capturedText = _isCaptured ? _isInActiveParty ? " 出戰" : " 收藏" : string.Empty;
		_nameplate.Text = $"Lv.{Level} {DisplayName}{capturedText}";
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

		if (TryCompanionCombat(ref velocity, step))
		{
			Velocity = velocity;
			MoveAndSlide();
			return;
		}

		Vector3 destination = GetFollowDestination();
		Vector3 toDestination = destination - GlobalPosition;
		toDestination.Y = 0.0f;

		if (toDestination.Length() > 16.0f)
		{
			GlobalPosition = destination;
			Velocity = Vector3.Zero;
			return;
		}

		float followSpeed = Mathf.Max(MoveSpeed * 2.4f, 5.0f);
		if (toDestination.Length() > 0.45f)
		{
			Vector3 direction = toDestination.Normalized();
			velocity.X = Mathf.MoveToward(velocity.X, direction.X * followSpeed, followSpeed * 7.0f * step);
			velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * followSpeed, followSpeed * 7.0f * step);
			FaceDirection(direction, step);
		}
		else
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(_followTarget.GlobalPosition - GlobalPosition, step);
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

		Vector3 behind = _followTarget.GlobalTransform.Basis.Z;
		behind.Y = 0.0f;
		behind = behind.LengthSquared() > 0.001f ? behind.Normalized() : new Vector3(0.0f, 0.0f, 1.0f);

		Vector3 right = _followTarget.GlobalTransform.Basis.X;
		right.Y = 0.0f;
		right = right.LengthSquared() > 0.001f ? right.Normalized() : new Vector3(1.0f, 0.0f, 0.0f);

		float rowDistance = 2.0f + (_followSlot / 2) * 1.35f;
		float sideOffset = _followSlot == 0 ? 0.0f : (_followSlot % 2 == 0 ? -0.7f : 0.7f);
		return _followTarget.GlobalPosition + behind * rowDistance + right * sideOffset;
	}

	private void LevelUp()
	{
		Level++;
		MaxHealth += 14 + EvolutionStage * 4;
		CurrentHealth = MaxHealth;
		Attack += 3 + EvolutionStage;
		Defense += 2 + EvolutionStage;
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
		if (distance > DetectionRadius * 1.25f)
		{
			_combatTarget = null;
			return false;
		}

		if (distance <= AttackRange)
		{
			velocity = SlowToStop(velocity, step);
			FaceDirection(toTarget, step);
			AttackActor(target);
			return true;
		}

		Vector3 direction = toTarget.Normalized();
		float combatSpeed = Mathf.Max(MoveSpeed * 2.05f, 4.2f);
		velocity.X = Mathf.MoveToward(velocity.X, direction.X * combatSpeed, combatSpeed * 8.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * combatSpeed, combatSpeed * 8.0f * step);
		FaceDirection(direction, step);
		return true;
	}

	private SimpleActor? GetCombatTarget()
	{
		if (_combatTarget != null && IsInstanceValid(_combatTarget) && _combatTarget.IsHostileToPlayer)
		{
			return _combatTarget;
		}

		SimpleActor? nearest = null;
		float nearestDistance = DetectionRadius;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is not SimpleActor actor || !actor.IsHostileToPlayer)
			{
				continue;
			}

			float distance = GlobalPosition.DistanceTo(actor.GlobalPosition);
			if (distance < nearestDistance)
			{
				nearest = actor;
				nearestDistance = distance;
			}
		}

		_combatTarget = nearest;
		return nearest;
	}

	private void AttackActor(SimpleActor target)
	{
		if (_attackCooldownRemaining > 0.0f)
		{
			return;
		}

		int roleBonus = CombatRole == "DPS" ? 4 : CombatRole == "Tank" ? 1 : CombatRole == "Ranged" ? 2 : 0;
		int affinityBonus = Affinity >= 80 ? 2 : Affinity >= 55 ? 1 : 0;
		int damage = Mathf.Max(Attack + roleBonus + affinityBonus, 1);
		SpawnSwingEffect(target.GlobalPosition);
		target.ReceiveDamage(damage, this);
		_attackCooldownRemaining = AttackCooldown;
	}

	private bool TryAttackPlayer(Node3D player, Vector3 velocity, float step)
	{
		Vector3 toPlayer = player.GlobalPosition - GlobalPosition;
		toPlayer.Y = 0.0f;
		if (toPlayer.Length() > AttackRange)
		{
			return false;
		}

		Velocity = SlowToStop(velocity, step);
		FaceDirection(toPlayer, step);
		if (_attackCooldownRemaining <= 0.0f && player is PlayerController playerController)
		{
			SpawnSwingEffect(player.GlobalPosition);
			playerController.ReceiveDamage(Attack);
			_attackCooldownRemaining = AttackCooldown;
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
