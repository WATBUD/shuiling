using Godot;

public partial class SimpleActor : CharacterBody3D
{
	[Export] public string ActorKind { get; set; } = "npc";
	[Export] public float MoveSpeed { get; set; } = 1.6f;
	[Export] public float WanderRadius { get; set; } = 10.0f;
	[Export] public float ChaseRadius { get; set; } = 20.0f;
	[Export] public Vector3 HomePosition { get; set; } = Vector3.Zero;

	private readonly RandomNumberGenerator _rng = new();
	private float _gravity;
	private Vector3 _targetPosition = Vector3.Zero;
	private float _waitTime;

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
