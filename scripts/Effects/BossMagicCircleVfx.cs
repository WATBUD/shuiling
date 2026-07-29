using Godot;

/// <summary>
/// Low-profile, persistent boss aura: a rotating six-pointed magic circle with
/// soft light and compact star motes. It deliberately avoids vertical beams.
/// </summary>
public partial class BossMagicCircleVfx : Node3D
{
	public Color AuraColor { get; set; } = new(1.0f, 0.58f, 0.16f, 0.92f);
	public float EffectRadius { get; set; } = 3.2f;

	private Node3D _clockwiseLayer = null!;
	private Node3D _counterClockwiseLayer = null!;
	private OmniLight3D _light = null!;
	private float _phase;

	public override void _Ready()
	{
		Name = "BossMagicCircle";
		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_phase += step;
		_clockwiseLayer.RotateY(step * 0.34f);
		_counterClockwiseLayer.RotateY(-step * 0.22f);
		float pulse = 1.0f + Mathf.Sin(_phase * 2.35f) * 0.045f;
		_clockwiseLayer.Scale = new Vector3(pulse, 1.0f, pulse);
		_light.LightEnergy = 1.05f + Mathf.Sin(_phase * 2.35f) * 0.22f;
	}

	private void BuildVisuals()
	{
		_clockwiseLayer = new Node3D { Name = "ClockwiseRunes" };
		_counterClockwiseLayer = new Node3D { Name = "CounterClockwiseRunes" };
		AddChild(_clockwiseLayer);
		AddChild(_counterClockwiseLayer);

		StandardMaterial3D bright = MakeMaterial(AuraColor, 5.2f);
		StandardMaterial3D soft = MakeMaterial(new Color(AuraColor.R, AuraColor.G, AuraColor.B, 0.28f), 2.2f);

		AddRing(_clockwiseLayer, "OuterRing", EffectRadius, 0.09f, bright, 0.025f);
		AddRing(_counterClockwiseLayer, "InnerRing", EffectRadius * 0.62f, 0.055f, bright, 0.032f);
		AddSixPointedStar(_clockwiseLayer, EffectRadius * 0.80f, 0.075f, bright);

		var groundGlow = new MeshInstance3D
		{
			Name = "GroundGlow",
			Position = new Vector3(0.0f, 0.012f, 0.0f),
			Mesh = new CylinderMesh
			{
				TopRadius = EffectRadius * 0.92f,
				BottomRadius = EffectRadius * 0.92f,
				Height = 0.012f,
				RadialSegments = 64,
				Material = soft,
			},
		};
		AddChild(groundGlow);

		var moteProcess = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = EffectRadius * 0.72f,
			Direction = Vector3.Up,
			Spread = 18.0f,
			InitialVelocityMin = 0.18f,
			InitialVelocityMax = 0.58f,
			Gravity = new Vector3(0.0f, 0.08f, 0.0f),
			ScaleMin = 0.45f,
			ScaleMax = 1.05f,
			AngleMin = -180.0f,
			AngleMax = 180.0f,
			AngularVelocityMin = -90.0f,
			AngularVelocityMax = 90.0f,
			Color = Colors.White,
		};
		AddChild(new GpuParticles3D
		{
			Name = "MagicCircleMotes",
			Position = new Vector3(0.0f, 0.10f, 0.0f),
			Amount = 34,
			Lifetime = 1.55f,
			Preprocess = 1.55f,
			Randomness = 0.72f,
			LocalCoords = true,
			ProcessMaterial = moteProcess,
			DrawPass1 = new QuadMesh
			{
				Size = Vector2.One * Mathf.Clamp(EffectRadius * 0.075f, 0.16f, 0.34f),
				Material = KenneyParticleVfx.CreateMaterial("star_06.png", AuraColor, true, true, true),
			},
			VisibilityAabb = new Aabb(
				new Vector3(-EffectRadius, -0.5f, -EffectRadius),
				new Vector3(EffectRadius * 2.0f, 3.0f, EffectRadius * 2.0f)),
			Emitting = true,
		});

		_light = new OmniLight3D
		{
			Name = "MagicCircleLight",
			Position = new Vector3(0.0f, 0.34f, 0.0f),
			LightColor = new Color(AuraColor.R, AuraColor.G, AuraColor.B),
			LightEnergy = 1.05f,
			OmniRange = EffectRadius * 1.45f,
			ShadowEnabled = false,
		};
		AddChild(_light);
	}

	private static StandardMaterial3D MakeMaterial(Color color, float emissionEnergy)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = emissionEnergy,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
	}

	private static void AddRing(Node3D parent, string name, float radius, float width, Material material, float height)
	{
		parent.AddChild(new MeshInstance3D
		{
			Name = name,
			Position = new Vector3(0.0f, height, 0.0f),
			Mesh = new TorusMesh
			{
				InnerRadius = Mathf.Max(radius - width, 0.04f),
				OuterRadius = radius,
				Rings = 64,
				RingSegments = 8,
				Material = material,
			},
		});
	}

	private static void AddSixPointedStar(Node3D parent, float radius, float width, Material material)
	{
		Vector3[] points = new Vector3[6];
		for (int index = 0; index < points.Length; index++)
		{
			float angle = Mathf.DegToRad(90.0f + index * 60.0f);
			points[index] = new Vector3(Mathf.Cos(angle) * radius, 0.045f, Mathf.Sin(angle) * radius);
		}

		AddTriangleEdges(parent, points, new[] { 0, 2, 4 }, width, material, "UpTriangle");
		AddTriangleEdges(parent, points, new[] { 1, 3, 5 }, width, material, "DownTriangle");
	}

	private static void AddTriangleEdges(Node3D parent, Vector3[] points, int[] indices, float width, Material material, string prefix)
	{
		for (int edge = 0; edge < 3; edge++)
		{
			Vector3 from = points[indices[edge]];
			Vector3 to = points[indices[(edge + 1) % 3]];
			Vector3 delta = to - from;
			var segment = new MeshInstance3D
			{
				Name = $"{prefix}{edge + 1}",
				Position = (from + to) * 0.5f,
				Rotation = new Vector3(0.0f, Mathf.Atan2(delta.X, delta.Z), 0.0f),
				Mesh = new BoxMesh
				{
					Size = new Vector3(width, 0.025f, delta.Length()),
					Material = material,
				},
			};
			parent.AddChild(segment);
		}
	}
}
