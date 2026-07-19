using Godot;
using System.Collections.Generic;

// Short-lived procedural combat visuals shared by every core skill.  The effect is
// intentionally data-driven (skill id + support behavior) so new cores can reuse the
// same cast/impact vocabulary without adding one scene per gem.
public partial class SkillAttackVfx : Node3D
{
	public const string CastEvent = "cast";
	public const string ImpactEvent = "impact";
	public const string ExplosionEvent = "explosion";
	public const string SplitEvent = "split";
	public const string ChainEvent = "chain";
	public const string PierceEvent = "pierce";
	public const string DissipateEvent = "dissipate";

	public string EventId { get; set; } = ImpactEvent;
	public string SkillId { get; set; } = string.Empty;
	public string ElementId { get; set; } = "physical";
	public Color EffectColor { get; set; } = new(1.0f, 0.48f, 0.16f, 0.92f);
	public float Radius { get; set; } = 1.0f;
	public float Lifetime { get; set; } = 0.42f;
	public Vector3 TravelVector { get; set; } = Vector3.Forward;
	public bool HasLifeSteal { get; set; }
	public ProjectileBehaviorProfile Behavior { get; set; } = new();

	private readonly List<(StandardMaterial3D Material, Color BaseColor)> _materials = new();
	private Node3D _visualRoot = null!;
	private float _age;

	public static void SpawnCast(Node parent, Vector3 position, Vector3 direction, string skillId, string elementId, Color color, ProjectileBehaviorProfile behavior, bool hasLifeSteal)
	{
		Spawn(parent, position, new SkillAttackVfx
		{
			EventId = CastEvent,
			SkillId = skillId,
			ElementId = elementId,
			EffectColor = color,
			Radius = skillId == "gem.skill.whirlwind" ? 1.25f : 0.82f,
			Lifetime = 0.38f,
			TravelVector = direction,
			Behavior = behavior.Clone(),
			HasLifeSteal = hasLifeSteal,
		});
	}

	public static void SpawnImpact(Node parent, Vector3 position, Vector3 direction, string skillId, string elementId, Color color, float radius, ProjectileBehaviorProfile behavior, bool hasLifeSteal)
	{
		Spawn(parent, position, new SkillAttackVfx
		{
			EventId = ImpactEvent,
			SkillId = skillId,
			ElementId = elementId,
			EffectColor = color,
			Radius = Mathf.Max(radius, 0.35f),
			Lifetime = skillId == "gem.skill.meteor" ? 0.58f : 0.42f,
			TravelVector = direction,
			Behavior = behavior.Clone(),
			HasLifeSteal = hasLifeSteal,
		});
	}

	public static void SpawnSpecial(Node parent, string eventId, Vector3 position, Vector3 travelVector, string skillId, string elementId, Color color, float radius, ProjectileBehaviorProfile behavior, bool hasLifeSteal = false)
	{
		Spawn(parent, position, new SkillAttackVfx
		{
			EventId = eventId,
			SkillId = skillId,
			ElementId = elementId,
			EffectColor = color,
			Radius = Mathf.Max(radius, 0.28f),
			Lifetime = eventId == ExplosionEvent ? 0.62f : eventId == ChainEvent ? 0.30f : 0.40f,
			TravelVector = travelVector,
			Behavior = behavior.Clone(),
			HasLifeSteal = hasLifeSteal,
		});
	}

	private static void Spawn(Node parent, Vector3 position, SkillAttackVfx effect)
	{
		parent.AddChild(effect);
		effect.GlobalPosition = position;
	}

	public override void _Ready()
	{
		_visualRoot = new Node3D { Name = "SkillVfxVisuals" };
		AddChild(_visualRoot);
		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		float t = Mathf.Clamp(_age / Mathf.Max(Lifetime, 0.01f), 0.0f, 1.0f);
		float fade = t < 0.62f ? 1.0f : 1.0f - (t - 0.62f) / 0.38f;
		float pop = t < 0.16f ? Mathf.Lerp(0.35f, 1.08f, t / 0.16f) : Mathf.Lerp(1.08f, 1.32f, (t - 0.16f) / 0.84f);

		if (EventId != ChainEvent)
		{
			_visualRoot.Scale = Vector3.One * pop;
		}
		_visualRoot.RotateY(step * (SkillId == "gem.skill.whirlwind" ? 13.0f : 4.2f));
		foreach ((StandardMaterial3D material, Color baseColor) in _materials)
		{
			material.AlbedoColor = new Color(baseColor.R, baseColor.G, baseColor.B, baseColor.A * fade);
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}

	private void BuildVisuals()
	{
		switch (EventId)
		{
			case CastEvent:
				BuildCast();
				break;
			case ExplosionEvent:
				BuildExplosion();
				break;
			case SplitEvent:
				BuildSplit();
				break;
			case ChainEvent:
				BuildChain();
				break;
			case PierceEvent:
				BuildPierce();
				break;
			case DissipateEvent:
				AddParticles("DissipatingMotes", EffectColor, 8, 0.25f, 0.3f, 1.2f, 180.0f, Vector3.Zero, Radius * 0.07f);
				break;
			default:
				BuildImpact();
				break;
		}
	}

	private void BuildCast()
	{
		Color bright = Lift(EffectColor, 0.25f);
		if (SkillId == "gem.skill.whirlwind")
		{
			AddSlashStar(5, Radius * 1.25f, Radius * 0.10f, bright);
			AddParticles("WhirlwindDust", new Color(0.86f, 0.92f, 1.0f, 0.72f), 24, 0.34f, 1.5f, 4.2f, 32.0f, new Vector3(0.0f, 0.25f, 0.0f), Radius * 0.055f);
			return;
		}

		AddFxMesh("CastCore", new SphereMesh { Radius = Radius * 0.15f, Height = Radius * 0.30f }, Vector3.Zero, Vector3.Zero, EffectColor);
		int count = 14 + Mathf.Min(Behavior.ExtraProjectiles, 5) * 3;
		AddParticles("CastMotes", bright, count, 0.34f, 1.0f, 3.8f, SkillId == "gem.skill.laser" ? 24.0f : 150.0f, new Vector3(0.0f, 0.45f, 0.0f), Radius * 0.055f);
		if (Behavior.ExtraProjectiles > 0)
		{
			AddSlashStar(Mathf.Min(Behavior.ExtraProjectiles + 2, 7), Radius * 0.72f, Radius * 0.045f, bright);
		}
	}

	private void BuildImpact()
	{
		Color bright = Lift(EffectColor, 0.30f);
		switch (SkillId)
		{
			case "gem.skill.fireball":
				AddFxMesh("FireCore", new SphereMesh { Radius = Radius * 0.24f, Height = Radius * 0.48f }, Vector3.Up * Radius * 0.10f, Vector3.Zero, new Color(1.0f, 0.72f, 0.12f, 0.92f));
				AddParticles("FireEmbers", new Color(1.0f, 0.24f, 0.035f, 0.94f), 30, 0.46f, 2.8f, 7.8f, 180.0f, new Vector3(0.0f, -2.2f, 0.0f), Radius * 0.065f);
				break;
			case "gem.skill.meteor":
				AddFxMesh("MeteorFlash", new SphereMesh { Radius = Radius * 0.34f, Height = Radius * 0.68f }, Vector3.Up * Radius * 0.08f, Vector3.Zero, new Color(1.0f, 0.32f, 0.04f, 0.88f));
				AddParticles("MeteorFlame", new Color(1.0f, 0.18f, 0.025f, 0.94f), 44, 0.52f, 3.4f, 9.4f, 180.0f, new Vector3(0.0f, -5.4f, 0.0f), Radius * 0.085f);
				AddParticles("MeteorSmoke", new Color(0.24f, 0.19f, 0.18f, 0.62f), 16, 0.72f, 0.8f, 2.6f, 110.0f, new Vector3(0.0f, 1.4f, 0.0f), Radius * 0.15f);
				break;
			case "gem.skill.laser":
				AddSlashStar(6, Radius * 1.18f, Radius * 0.045f, new Color(0.64f, 0.94f, 1.0f, 0.96f));
				AddParticles("LaserIons", new Color(0.28f, 0.84f, 1.0f, 0.94f), 22, 0.30f, 4.0f, 9.0f, 180.0f, Vector3.Zero, Radius * 0.045f);
				break;
			case "gem.skill.whirlwind":
				AddSlashStar(7, Radius * 1.12f, Radius * 0.075f, bright);
				AddParticles("SlashFragments", bright, 24, 0.32f, 2.8f, 7.0f, 48.0f, new Vector3(0.0f, -2.0f, 0.0f), Radius * 0.05f);
				break;
			default:
				AddSlashStar(4, Radius * 0.82f, Radius * 0.05f, bright);
				AddParticles("ImpactSparks", bright, 16, 0.32f, 2.4f, 6.4f, 180.0f, new Vector3(0.0f, -3.2f, 0.0f), Radius * 0.05f);
				break;
		}

		if (HasLifeSteal)
		{
			AddParticles("LifeStealMotes", new Color(0.72f, 0.18f, 0.92f, 0.90f), 12, 0.48f, 0.7f, 2.4f, 65.0f, new Vector3(0.0f, 1.8f, 0.0f), Radius * 0.055f);
		}
	}

	private void BuildExplosion()
	{
		AddFxMesh("BlastCore", new SphereMesh { Radius = Radius * 0.30f, Height = Radius * 0.60f }, Vector3.Up * Radius * 0.10f, Vector3.Zero, new Color(1.0f, 0.38f, 0.06f, 0.72f));
		AddSlashStar(10, Radius * 0.88f, Mathf.Max(Radius * 0.025f, 0.035f), Lift(EffectColor, 0.28f));
		AddParticles("ExplosionFragments", EffectColor, 52, 0.56f, Radius * 1.4f, Radius * 3.8f, 180.0f, new Vector3(0.0f, -4.8f, 0.0f), Mathf.Max(Radius * 0.025f, 0.04f));
	}

	private void BuildSplit()
	{
		AddSlashStar(Mathf.Clamp(Behavior.SplitCount + 3, 5, 10), Radius, Radius * 0.04f, Lift(EffectColor, 0.24f));
		AddParticles("SplitShards", EffectColor, 18 + Mathf.Min(Behavior.SplitCount, 6) * 2, 0.36f, 3.0f, 7.8f, 42.0f, new Vector3(0.0f, -2.4f, 0.0f), Radius * 0.045f);
	}

	private void BuildChain()
	{
		Vector3 delta = TravelVector;
		float length = Mathf.Max(delta.Length(), 0.1f);
		Vector3 direction = delta / length;
		AddBeam("ChainOuter", delta, 0.10f, new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.34f));
		AddBeam("ChainCore", delta, 0.035f, new Color(0.88f, 0.97f, 1.0f, 0.96f));
		AddParticles("ChainOrigin", Lift(EffectColor, 0.25f), 10, 0.24f, 2.5f, 6.0f, 180.0f, Vector3.Zero, 0.045f);
		_ = direction;
	}

	private void BuildPierce()
	{
		Vector3 direction = TravelVector.LengthSquared() > 0.001f ? TravelVector.Normalized() : Vector3.Forward;
		for (int index = -1; index <= 1; index++)
		{
			Vector3 side = direction.Cross(Vector3.Up).Normalized() * index * Radius * 0.13f;
			AddDirectionalStreak($"PierceStreak{index + 1}", direction, side, Radius * (1.1f - Mathf.Abs(index) * 0.18f), Radius * 0.035f, Lift(EffectColor, 0.30f));
		}
		AddParticles("PierceSparks", EffectColor, 13, 0.26f, 3.5f, 7.0f, 28.0f, Vector3.Zero, Radius * 0.04f);
	}

	private void AddSlashStar(int count, float length, float width, Color color)
	{
		for (int index = 0; index < count; index++)
		{
			float angle = 360.0f * index / Mathf.Max(count, 1);
			AddFxMesh(
				$"EnergySlash{index}",
				new BoxMesh { Size = new Vector3(width, width * 0.55f, length) },
				Vector3.Up * Radius * 0.10f,
				new Vector3((index % 2 == 0 ? 1.0f : -1.0f) * 12.0f, angle, 0.0f),
				color);
		}
	}

	private void AddDirectionalStreak(string name, Vector3 direction, Vector3 offset, float length, float width, Color color)
	{
		var mesh = new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = new Vector3(width, width, length) },
			Position = offset + direction * length * 0.18f + Vector3.Up * Radius * 0.10f,
			Basis = Basis.LookingAt(direction, Vector3.Up),
		};
		mesh.SetSurfaceOverrideMaterial(0, CreateMaterial(color));
		_visualRoot.AddChild(mesh);
	}

	private void AddBeam(string name, Vector3 delta, float width, Color color)
	{
		float length = Mathf.Max(delta.Length(), 0.1f);
		Vector3 direction = delta / length;
		var beam = new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = new Vector3(width, width, length) },
			Position = delta * 0.5f,
			Basis = Basis.LookingAt(direction, Vector3.Up),
		};
		beam.SetSurfaceOverrideMaterial(0, CreateMaterial(color));
		_visualRoot.AddChild(beam);
	}

	private void AddParticles(string name, Color color, int amount, float lifetime, float minimumSpeed, float maximumSpeed, float spread, Vector3 gravity, float size)
	{
		var particleMaterial = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 3.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		var sparkMesh = new QuadMesh
		{
			Size = new Vector2(Mathf.Max(size, 0.022f), Mathf.Max(size * 3.8f, 0.09f)),
			Material = particleMaterial,
		};
		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = Mathf.Max(Radius * 0.10f, 0.08f),
			Direction = Vector3.Up,
			Spread = spread,
			InitialVelocityMin = minimumSpeed,
			InitialVelocityMax = maximumSpeed,
			Gravity = gravity,
			ScaleMin = 0.55f,
			ScaleMax = 1.25f,
			Color = color,
		};
		var particles = new GpuParticles3D
		{
			Name = name,
			Amount = Mathf.Max(amount, 1),
			Lifetime = Mathf.Max(lifetime, 0.12f),
			OneShot = true,
			Explosiveness = 0.92f,
			Randomness = 0.52f,
			ProcessMaterial = processMaterial,
			DrawPass1 = sparkMesh,
			Emitting = true,
		};
		AddChild(particles);
	}

	private void AddFxMesh(string name, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Color color)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = name,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, CreateMaterial(color));
		_visualRoot.AddChild(meshInstance);
	}

	private StandardMaterial3D CreateMaterial(Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 2.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		_materials.Add((material, color));
		return material;
	}

	private static Color Lift(Color color, float amount)
	{
		return new Color(
			Mathf.Clamp(color.R + amount, 0.0f, 1.0f),
			Mathf.Clamp(color.G + amount, 0.0f, 1.0f),
			Mathf.Clamp(color.B + amount, 0.0f, 1.0f),
			color.A);
	}
}
