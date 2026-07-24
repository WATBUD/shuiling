using Godot;

// A thrown capture orb that flies in a parabolic arc (gravity) and opens the
// capture attempt on the monster it lands on. Rendered as a glowing energy
// sphere with a pulsing halo instead of a flat net.
public partial class CaptureNet : Area3D
{
	[Export] public float CaptureRadius { get; set; } = 1.05f;
	[Export] public float Lifetime { get; set; } = 3.0f;
	[Export] public float Gravity { get; set; } = 18.0f;

	public PlayerController? OwnerPlayer { get; set; }
	public Vector3 LaunchVelocity { get; set; } = Vector3.Forward * 16.0f;

	private static readonly Color OrbColor = new(0.5f, 0.85f, 1.0f);

	private Vector3 _velocity;
	private bool _hasHit;
	private float _age;
	private float _spawnY;
	private float _trailRemaining;
	private Node _effectParent = null!;
	private MeshInstance3D _halo = null!;

	public override void _Ready()
	{
		Monitoring = true;
		Monitorable = false;
		BodyEntered += OnBodyEntered;
		_velocity = LaunchVelocity;
		_spawnY = GlobalPosition.Y;
		_effectParent = GetTree().CurrentScene ?? GetParent();
		CreateCollision();
		CreateVisual();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hasHit)
		{
			return;
		}

		float step = (float)delta;
		_age += step;

		// Parabolic integration.
		_velocity.Y -= Gravity * step;
		GlobalPosition += _velocity * step;

		// Pulse the halo for a lively energy look.
		float pulse = 1.0f + 0.18f * Mathf.Sin(_age * 14.0f);
		_halo.Scale = new Vector3(pulse, pulse, pulse);

		// Leave a fading trail behind the orb.
		_trailRemaining -= step;
		if (_trailRemaining <= 0.0f)
		{
			_trailRemaining = 0.035f;
			CaptureVfx.SpawnTrailDot(_effectParent, GlobalPosition, OrbColor);
		}

		if (_age >= Lifetime || GlobalPosition.Y < _spawnY - 6.0f)
		{
			CaptureVfx.SpawnShockwave(_effectParent, GlobalPosition, OrbColor);
			QueueFree();
			return;
		}

		TryCaptureOverlaps();
	}

	private void CreateCollision()
	{
		AddChild(new CollisionShape3D
		{
			Name = "CaptureArea",
			Shape = new SphereShape3D { Radius = CaptureRadius },
		});
	}

	private void CreateVisual()
	{
		// Bright emissive core.
		var core = new MeshInstance3D
		{
			Name = "OrbCore",
			Mesh = new SphereMesh { Radius = 0.30f, Height = 0.60f, RadialSegments = 24, Rings = 12 },
		};
		core.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = new Color(0.70f, 0.95f, 1.0f, 0.9f),
			EmissionEnabled = true,
			Emission = new Color(0.45f, 0.85f, 1.0f),
			EmissionEnergyMultiplier = 3.2f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		});
		AddChild(core);

		// Soft translucent halo that pulses.
		_halo = new MeshInstance3D
		{
			Name = "OrbHalo",
			Mesh = new SphereMesh { Radius = 0.55f, Height = 1.10f, RadialSegments = 24, Rings = 12 },
		};
		_halo.SetSurfaceOverrideMaterial(0, new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.9f, 1.0f, 0.22f),
			EmissionEnabled = true,
			Emission = new Color(0.4f, 0.8f, 1.0f),
			EmissionEnergyMultiplier = 1.4f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		});
		AddChild(_halo);

		// A gentle light so it glows on the ground it passes over.
		AddChild(new OmniLight3D
		{
			Name = "OrbGlow",
			LightColor = new Color(0.5f, 0.85f, 1.0f),
			LightEnergy = 1.6f,
			OmniRange = 3.2f,
		});
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
			if (OwnerPlayer != null && OwnerPlayer.HandleCaptureNetHit(actor))
			{
				_hasHit = true;
				CaptureVfx.SpawnConvergence(_effectParent, actor.GlobalPosition + new Vector3(0.0f, 0.8f, 0.0f), OrbColor);
				QueueFree();
			}

			return;
		}

		if (body is StaticBody3D)
		{
			_hasHit = true;
			CaptureVfx.SpawnShockwave(_effectParent, GlobalPosition, OrbColor);
			QueueFree();
		}
	}
}
