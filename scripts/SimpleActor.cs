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

	private readonly RandomNumberGenerator _rng = new();
	private bool _isCaptured;
	private PlayerController? _followTarget;
	private int _followSlot;
	private float _gravity;
	private Vector3 _targetPosition = Vector3.Zero;
	private float _waitTime;

	public bool CanBeCaptured => !_isCaptured;
	public bool IsCaptured => _isCaptured;
	public string TypeName => ActorKind == "monster" ? "怪物" : "NPC";
	public string StateName => _isCaptured ? "已捕捉" : ActorKind == "monster" ? "敵對" : "中立";
	public float HealthRatio => MaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)MaxHealth, 0.0f, 1.0f);

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_rng.Seed = Time.GetTicksUsec() + GetInstanceId();

		if (HomePosition == Vector3.Zero)
		{
			HomePosition = GlobalPosition;
		}

		_targetPosition = PickWanderTarget();
		AddToGroup(ActorKind == "monster" ? "monsters" : "npcs");
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = (float)delta;
		Vector3 velocity = Velocity;

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

	public void Capture(PlayerController followTarget, int followSlot)
	{
		_isCaptured = true;
		_followTarget = followTarget;
		_followSlot = followSlot;
		_waitTime = 0.0f;
		Velocity = Vector3.Zero;
		AddCollisionExceptionWith(followTarget);
		followTarget.AddCollisionExceptionWith(this);
		RemoveFromGroup(ActorKind == "monster" ? "monsters" : "npcs");
		AddToGroup("captured_actors");
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
	}

	private void FollowCapturedTarget(Vector3 velocity, float step)
	{
		if (_followTarget == null || !IsInstanceValid(_followTarget))
		{
			Velocity = SlowToStop(velocity, step);
			MoveAndSlide();
			return;
		}

		Vector3 behind = _followTarget.GlobalTransform.Basis.Z;
		behind.Y = 0.0f;
		behind = behind.LengthSquared() > 0.001f ? behind.Normalized() : new Vector3(0.0f, 0.0f, 1.0f);

		Vector3 right = _followTarget.GlobalTransform.Basis.X;
		right.Y = 0.0f;
		right = right.LengthSquared() > 0.001f ? right.Normalized() : new Vector3(1.0f, 0.0f, 0.0f);

		float rowDistance = 2.0f + (_followSlot / 2) * 1.35f;
		float sideOffset = _followSlot == 0 ? 0.0f : (_followSlot % 2 == 0 ? -0.7f : 0.7f);
		Vector3 destination = _followTarget.GlobalPosition + behind * rowDistance + right * sideOffset;
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
