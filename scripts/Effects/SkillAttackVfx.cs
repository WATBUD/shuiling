using Godot;
using System.Collections.Generic;

// Short-lived procedural combat visuals shared by every core skill.  The effect is
// intentionally data-driven (skill id + support behavior) so new cores can reuse the
// same cast/impact vocabulary without adding one scene per gem.
public partial class SkillAttackVfx : Node3D
{
	private static int _activeEffectCount;
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
	private bool _registeredAsActive;

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
		if (_activeEffectCount >= PerformanceConfig.MaximumVisibleSkillEffects)
		{
			effect.Dispose();
			return;
		}

		parent.AddChild(effect);
		effect.GlobalPosition = position;
	}

	public override void _Ready()
	{
		_activeEffectCount++;
		_registeredAsActive = true;
		_visualRoot = new Node3D { Name = "SkillVfxVisuals" };
		AddChild(_visualRoot);
		BuildVisuals();
	}

	public override void _ExitTree()
	{
		if (_registeredAsActive)
		{
			_activeEffectCount = Mathf.Max(_activeEffectCount - 1, 0);
			_registeredAsActive = false;
		}
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

		AddTextureSprite("CastCore", "magic_03.png", Vector3.Up * Radius * 0.08f, Radius * 0.58f, Lift(EffectColor, 0.18f));
		AddGroundTexture("CastSigil", "symbol_02.png", Radius * 1.25f, new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.62f));
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
				AddTextureSprite("FireCore", "fire_02.png", Vector3.Up * Radius * 0.12f, Radius * 1.10f, new Color(1.0f, 0.58f, 0.10f, 0.94f));
				AddParticles("FireEmbers", new Color(1.0f, 0.24f, 0.035f, 0.94f), 30, 0.46f, 2.8f, 7.8f, 180.0f, new Vector3(0.0f, -2.2f, 0.0f), Radius * 0.065f);
				break;
			case "gem.skill.meteor":
				AddTextureSprite("MeteorFlash", "fire_01.png", Vector3.Up * Radius * 0.12f, Radius * 1.65f, new Color(1.0f, 0.28f, 0.04f, 0.92f));
				AddGroundTexture("MeteorScorch", "scorch_02.png", Radius * 1.75f, new Color(1.0f, 0.24f, 0.04f, 0.58f), false);
				AddParticles("MeteorFlame", new Color(1.0f, 0.18f, 0.025f, 0.94f), 44, 0.52f, 3.4f, 9.4f, 180.0f, new Vector3(0.0f, -5.4f, 0.0f), Radius * 0.085f);
				AddParticles("MeteorSmoke", new Color(0.24f, 0.19f, 0.18f, 0.62f), 16, 0.72f, 0.8f, 2.6f, 110.0f, new Vector3(0.0f, 1.4f, 0.0f), Radius * 0.15f);
				break;
			case "gem.skill.laser":
				AddTextureSprite("LaserFlash", "flare_01.png", Vector3.Up * Radius * 0.10f, Radius * 1.15f, new Color(0.50f, 0.92f, 1.0f, 0.94f));
				AddSlashStar(6, Radius * 1.18f, Radius * 0.045f, new Color(0.64f, 0.94f, 1.0f, 0.96f));
				AddParticles("LaserIons", new Color(0.28f, 0.84f, 1.0f, 0.94f), 22, 0.30f, 4.0f, 9.0f, 180.0f, Vector3.Zero, Radius * 0.045f);
				break;
			case "gem.skill.rocket":
				// 火箭：熾熱核心 + 火花噴發 + 上升煙塵。
				AddTextureSprite("RocketCore", "muzzle_05.png", Vector3.Up * Radius * 0.12f, Radius * 1.35f, new Color(1.0f, 0.48f, 0.06f, 0.95f));
				AddParticles("RocketBlast", new Color(1.0f, 0.34f, 0.05f, 0.95f), 40, 0.48f, 3.4f, 9.0f, 180.0f, new Vector3(0.0f, -3.0f, 0.0f), Radius * 0.08f);
				AddParticles("RocketSmoke", new Color(0.26f, 0.22f, 0.20f, 0.6f), 14, 0.7f, 0.8f, 2.4f, 120.0f, new Vector3(0.0f, 1.6f, 0.0f), Radius * 0.14f);
				break;
			case "gem.skill.ice_shard":
				// 冰箭：淡藍冰晶碎片 + 霜霧。
				AddSlashStar(7, Radius * 1.0f, Radius * 0.06f, new Color(0.66f, 0.9f, 1.0f, 0.96f));
				AddTextureSprite("IceCore", "star_08.png", Vector3.Up * Radius * 0.10f, Radius * 0.88f, new Color(0.72f, 0.92f, 1.0f, 0.96f));
				AddParticles("FrostMotes", new Color(0.72f, 0.92f, 1.0f, 0.92f), 26, 0.5f, 1.6f, 5.0f, 150.0f, new Vector3(0.0f, -1.0f, 0.0f), Radius * 0.05f);
				break;
			case "gem.skill.lightning":
				// Targeted RPG lightning: a tall bolt descends from above the enemy
				// and terminates in a bright ground-level flash.
				AddLightningBolt(new Color(1.0f, 0.96f, 0.48f, 0.98f));
				AddTextureSprite("LightningImpact", "flare_01.png", Vector3.Up * Radius * 0.12f, Radius * 0.82f, new Color(1.0f, 0.94f, 0.44f, 0.98f));
				AddParticles("Sparks", new Color(1.0f, 0.92f, 0.4f, 0.95f), 24, 0.26f, 4.5f, 10.0f, 180.0f, Vector3.Zero, Radius * 0.04f);
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
		AddTextureSprite("BlastCore", "fire_01.png", Vector3.Up * Radius * 0.12f, Radius * 1.55f, new Color(1.0f, 0.34f, 0.05f, 0.88f));
		AddGroundTexture("BlastScorch", "scorch_01.png", Radius * 1.35f, new Color(0.95f, 0.26f, 0.04f, 0.48f), false);
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
		if (EventId == ImpactEvent)
		{
			length *= KenneyParticleVfx.ImpactFlashScale;
		}
		for (int index = 0; index < count; index++)
		{
			float angle = 360.0f * index / Mathf.Max(count, 1);
			MeshInstance3D slash = AddTextureSprite(
				$"EnergySlash{index}",
				index % 2 == 0 ? "slash_03.png" : "slash_04.png",
				Vector3.Up * Radius * (0.08f + index * 0.008f),
				Mathf.Max(length, width * 5.0f),
				color);
			slash.RotationDegrees = new Vector3(0.0f, angle, angle * 0.5f);
		}
	}

	private void AddDirectionalStreak(string name, Vector3 direction, Vector3 offset, float length, float width, Color color)
	{
		Vector3 safeDirection = SafeDirection(direction);
		var mesh = new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = new Vector3(width, width, length) },
			Position = offset + safeDirection * length * 0.18f + Vector3.Up * Radius * 0.10f,
			Basis = Basis.LookingAt(safeDirection, SafeUp(safeDirection)),
		};
		mesh.SetSurfaceOverrideMaterial(0, CreateMaterial(color));
		_visualRoot.AddChild(mesh);
	}

	private void AddBeam(string name, Vector3 delta, float width, Color color)
	{
		float length = Mathf.Max(delta.Length(), 0.1f);
		Vector3 direction = SafeDirection(delta);
		var beam = new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = new Vector3(width, width, length) },
			Position = delta * 0.5f,
			Basis = Basis.LookingAt(direction, SafeUp(direction)),
		};
		beam.SetSurfaceOverrideMaterial(0, CreateMaterial(color));
		_visualRoot.AddChild(beam);
	}

	private static Vector3 SafeDirection(Vector3 direction)
	{
		return direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Forward;
	}

	private static Vector3 SafeUp(Vector3 direction)
	{
		return Mathf.Abs(direction.Dot(Vector3.Up)) > 0.98f ? Vector3.Forward : Vector3.Up;
	}

	private void AddParticles(string name, Color color, int amount, float lifetime, float minimumSpeed, float maximumSpeed, float spread, Vector3 gravity, float size)
	{
		string texture = KenneyParticleVfx.TextureFor(name, SkillId, ElementId);
		AddChild(KenneyParticleVfx.CreateBurst(
			name,
			texture,
			color,
			amount,
			lifetime,
			minimumSpeed,
			maximumSpeed,
			spread,
			gravity,
			Mathf.Max(size * 1.7f, 0.04f),
			Mathf.Max(size * 3.8f, 0.10f),
			Mathf.Max(Radius * 0.10f, 0.08f)));
	}

	private MeshInstance3D AddTextureSprite(string name, string texture, Vector3 position, float size, Color color)
	{
		MeshInstance3D sprite = KenneyParticleVfx.CreateSprite(name, texture, color, Vector2.One * Mathf.Max(size, 0.08f));
		sprite.Position = position;
		if (sprite.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D material)
		{
			_materials.Add((material, color));
		}
		_visualRoot.AddChild(sprite);
		return sprite;
	}

	private void AddLightningBolt(Color color)
	{
		MeshInstance3D bolt = KenneyParticleVfx.CreateSprite(
			"LightningBolt",
			"spark_06.png",
			color,
			new Vector2(Mathf.Max(Radius * 0.52f, 0.18f), Mathf.Max(Radius * 3.2f, 1.2f)));
		if (bolt.Mesh is QuadMesh quad)
		{
			// Anchor the bottom of the scaled billboard at the enemy instead of
			// centering half of the lightning underneath the ground.
			bolt.Position = Vector3.Up * quad.Size.Y * 0.48f;
		}
		if (bolt.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D material)
		{
			_materials.Add((material, color));
		}
		_visualRoot.AddChild(bolt);
	}

	private void AddGroundTexture(string name, string texture, float size, Color color, bool additive = true)
	{
		MeshInstance3D sprite = KenneyParticleVfx.CreateSprite(name, texture, color, Vector2.One * Mathf.Max(size, 0.10f), false, additive);
		sprite.Position = new Vector3(0.0f, 0.025f, 0.0f);
		sprite.RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f);
		if (sprite.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D material)
		{
			_materials.Add((material, color));
		}
		_visualRoot.AddChild(sprite);
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
			EmissionEnergyMultiplier = 5.0f,
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
