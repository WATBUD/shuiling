using Godot;

public partial class CaptureNet : Area3D
{
	[Export] public float Speed { get; set; } = 28.0f;
	[Export] public float Lifetime { get; set; } = 1.4f;
	[Export] public float CaptureRadius { get; set; } = 1.05f;

	public PlayerController? OwnerPlayer { get; set; }
	public Vector3 Direction { get; set; } = Vector3.Forward;

	private bool _hasHit;
	private float _age;
	private MeshInstance3D _netVisual = null!;

	public override void _Ready()
	{
		Monitoring = true;
		Monitorable = false;
		BodyEntered += OnBodyEntered;
		Direction = Direction.LengthSquared() > 0.001f ? Direction.Normalized() : Vector3.Forward;
		CreateCollision();
		CreateVisual();
		AlignToDirection();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hasHit)
		{
			return;
		}

		float step = (float)delta;
		_age += step;
		if (_age >= Lifetime)
		{
			QueueFree();
			return;
		}

		GlobalPosition += Direction * Speed * step;
		_netVisual.RotateY(16.0f * step);
		TryCaptureOverlaps();
	}

	public void AlignToDirection()
	{
		if (Direction.LengthSquared() <= 0.001f)
		{
			return;
		}

		LookAt(GlobalPosition + Direction, Vector3.Up);
	}

	private void CreateCollision()
	{
		var collisionShape = new CollisionShape3D
		{
			Name = "CaptureArea",
			Shape = new SphereShape3D { Radius = CaptureRadius },
		};
		AddChild(collisionShape);
	}

	private void CreateVisual()
	{
		var netMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.72f, 0.92f, 1.0f, 0.48f),
			Roughness = 0.35f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		};

		_netVisual = new MeshInstance3D
		{
			Name = "NetVisual",
			Mesh = new CylinderMesh
			{
				TopRadius = CaptureRadius,
				BottomRadius = CaptureRadius,
				Height = 0.04f,
				RadialSegments = 32,
			},
			RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f),
		};
		_netVisual.SetSurfaceOverrideMaterial(0, netMaterial);
		AddChild(_netVisual);

		AddNetStrap(new Vector3(CaptureRadius, 0.0f, 0.0f));
		AddNetStrap(new Vector3(0.0f, 0.0f, CaptureRadius));
	}

	private void AddNetStrap(Vector3 size)
	{
		var strap = new MeshInstance3D
		{
			Name = "NetStrap",
			Mesh = new BoxMesh
			{
				Size = new Vector3(
					size.X > 0.0f ? size.X * 2.0f : 0.06f,
					0.06f,
					size.Z > 0.0f ? size.Z * 2.0f : 0.06f
				),
			},
			Position = new Vector3(0.0f, 0.03f, 0.0f),
		};
		strap.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = new Color(0.96f, 0.98f, 1.0f, 0.82f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		});
		_netVisual.AddChild(strap);
	}

	private void TryCaptureOverlaps()
	{
		foreach (Node3D body in GetOverlappingBodies())
		{
			OnBodyEntered(body);
			if (_hasHit)
			{
				return;
			}
		}
	}

	private void OnBodyEntered(Node3D body)
	{
		if (_hasHit || body == OwnerPlayer)
		{
			return;
		}

		if (body is SimpleActor actor)
		{
			if (OwnerPlayer?.BeginCaptureChallenge(actor) == true)
			{
				_hasHit = true;
				QueueFree();
			}

			return;
		}

		if (body is StaticBody3D)
		{
			_hasHit = true;
			QueueFree();
		}
	}
}
