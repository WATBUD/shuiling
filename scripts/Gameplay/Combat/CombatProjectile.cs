using Godot;
using System.Collections.Generic;

// A live, gameplay-affecting projectile. Unlike the cosmetic AttackProjectile, this
// one travels, homes lightly toward its target, collides with hostile actors, and
// applies damage on impact. Equipped skill gems attach PoE-style behaviors that this
// projectile resolves on hit: split into extra shots, chain to fresh targets, pierce
// through, or explode for area damage.
public partial class CombatProjectile : Node3D
{
	// Damage falloff so recursive/secondary hits do not snowball out of control.
	private const float ChainDamageScale = 0.78f;
	private const float SplitDamageScale = 0.62f;
	private const float ExplosionDamageScale = 0.5f;
	private const float ChainSearchRadius = 8.5f;
	private const float SafetyLifetime = 3.2f;

	public SimpleActor? Attacker { get; set; }
	public int Damage { get; set; } = 1;
	public Color EffectColor { get; set; } = new(1.0f, 0.5f, 0.18f, 0.92f);
	public bool IsMelee { get; set; }
	public bool IsArrow { get; set; }
	public string VisualSkillId { get; set; } = string.Empty;
	public string ElementId { get; set; } = "physical";
	public bool HasLifeSteal { get; set; }
	public float Speed { get; set; } = 17.0f;
	public float MaxRange { get; set; } = 10.0f;
	public float HitRadius { get; set; } = 1.15f;
	public SimpleActor? InitialTarget { get; set; }
	public Vector3 LaunchDirection { get; set; } = Vector3.Forward;
	public Vector3 SpawnOrigin { get; set; }
	public ProjectileBehaviorProfile Behavior { get; set; } = new();

	private readonly List<StandardMaterial3D> _materials = new();
	private readonly HashSet<SimpleActor> _alreadyHit = new();
	private Vector3 _direction = Vector3.Forward;
	private SimpleActor? _homingTarget;
	private float _traveled;
	private float _age;
	private bool _hasSplit;
	private bool _finished;

	public override void _Ready()
	{
		GlobalPosition = SpawnOrigin;
		_direction = FlattenOrDefault(LaunchDirection, -GlobalTransform.Basis.Z);
		_homingTarget = InitialTarget;
		FaceTravelDirection();
		BuildVisuals();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_finished)
		{
			return;
		}

		float step = (float)delta;
		_age += step;
		if (_age >= SafetyLifetime || Attacker == null || !IsInstanceValid(Attacker))
		{
			Finish(false);
			return;
		}

		SteerTowardHoming(step);

		float distance = Speed * step;
		Vector3 previousPosition = GlobalPosition;
		GlobalPosition += _direction * distance;
		_traveled += distance;
		RotationDegrees += IsMelee
			? new Vector3(0.0f, 900.0f * step, 0.0f)
			: new Vector3(220.0f * step, 0.0f, 420.0f * step);

		SimpleActor? victim = FindImpact(previousPosition, GlobalPosition);
		if (victim != null)
		{
			ResolveHit(victim);
			return;
		}

		if (_traveled >= MaxRange)
		{
			Finish(true);
		}
	}

	private void SteerTowardHoming(float step)
	{
		if (_homingTarget == null || !IsInstanceValid(_homingTarget) || !_homingTarget.IsActiveWorldTarget)
		{
			_homingTarget = null;
			return;
		}

		Vector3 desired = FlattenOrDefault(_homingTarget.GlobalPosition - GlobalPosition, _direction);
		_direction = FlattenOrDefault(_direction.Lerp(desired, Mathf.Clamp(step * 7.0f, 0.0f, 1.0f)), _direction);
		FaceTravelDirection();
	}

	private SimpleActor? FindImpact(Vector3 from, Vector3 to)
	{
		if (Attacker == null)
		{
			return null;
		}

		SimpleActor? closest = null;
		float bestPathProgress = float.MaxValue;
		Vector3 flatFrom = new(from.X, 0.0f, from.Z);
		Vector3 flatTo = new(to.X, 0.0f, to.Z);
		Vector3 segment = flatTo - flatFrom;
		float segmentLengthSquared = segment.LengthSquared();
		Vector3 midpoint = (flatFrom + flatTo) * 0.5f;
		float searchRadius = HitRadius + Mathf.Sqrt(segmentLengthSquared) * 0.5f;
		foreach (SimpleActor candidate in Attacker.FindProjectileTargets(midpoint, searchRadius, _alreadyHit))
		{
			Vector3 targetPosition = candidate.GlobalPosition;
			targetPosition.Y = 0.0f;
			float progress = segmentLengthSquared > 0.0001f
				? Mathf.Clamp((targetPosition - flatFrom).Dot(segment) / segmentLengthSquared, 0.0f, 1.0f)
				: 0.0f;
			Vector3 nearestPoint = flatFrom + segment * progress;
			if (nearestPoint.DistanceSquaredTo(targetPosition) <= HitRadius * HitRadius && progress < bestPathProgress)
			{
				bestPathProgress = progress;
				closest = candidate;
			}
		}

		return closest;
	}

	private void ResolveHit(SimpleActor target)
	{
		if (Attacker == null || !IsInstanceValid(Attacker))
		{
			Finish(false);
			return;
		}

		_alreadyHit.Add(target);
		Attacker.ResolveProjectileHit(target, Damage);
		SpawnImpactPulse(target.GlobalPosition, IsMelee ? HitRadius * 1.3f : HitRadius * 1.55f);

		if (Behavior.ExplosionRadius > 0.0f)
		{
			DetonateExplosion(target.GlobalPosition);
		}

		if (Behavior.SplitCount > 0 && !_hasSplit)
		{
			SpawnSplitChildren(target.GlobalPosition);
			_hasSplit = true;
		}

		if (Behavior.ChainBounces > 0 && TryChain(target.GlobalPosition))
		{
			return;
		}

		if (Behavior.PierceCount > 0)
		{
			SpawnSpecialEffect(SkillAttackVfx.PierceEvent, target.GlobalPosition, _direction, HitRadius * 1.35f);
			Behavior.PierceCount--;
			return;
		}

		// ResolveHit already created the skill-specific impact; do not create a
		// second generic pulse at the same position.
		Finish(false);
	}

	private void DetonateExplosion(Vector3 center)
	{
		if (Attacker == null)
		{
			return;
		}

		int splashDamage = Mathf.Max(Mathf.RoundToInt(Damage * ExplosionDamageScale), 1);
		foreach (SimpleActor victim in Attacker.FindProjectileTargets(center, Behavior.ExplosionRadius, _alreadyHit))
		{
			_alreadyHit.Add(victim);
			Attacker.ResolveProjectileHit(victim, splashDamage);
		}

		SpawnSpecialEffect(SkillAttackVfx.ExplosionEvent, center, _direction, Behavior.ExplosionRadius);
	}

	private void SpawnSplitChildren(Vector3 origin)
	{
		if (Attacker == null)
		{
			return;
		}

		int childDamage = Mathf.Max(Mathf.RoundToInt(Damage * SplitDamageScale), 1);
		SpawnSpecialEffect(SkillAttackVfx.SplitEvent, origin, _direction, HitRadius * 1.8f);
		List<SimpleActor> nearby = Attacker.FindProjectileTargets(origin, ChainSearchRadius, _alreadyHit);
		for (int index = 0; index < Behavior.SplitCount; index++)
		{
			Vector3 direction;
			SimpleActor? childTarget = index < nearby.Count ? nearby[index] : null;
			if (childTarget != null)
			{
				direction = FlattenOrDefault(childTarget.GlobalPosition - origin, _direction);
			}
			else
			{
				float spread = Mathf.DegToRad(52.0f);
				float offset = (index - (Behavior.SplitCount - 1) / 2.0f) * spread;
				direction = _direction.Rotated(Vector3.Up, offset);
			}

			var child = new CombatProjectile
			{
				Attacker = Attacker,
				Damage = childDamage,
				EffectColor = EffectColor,
				IsMelee = false,
				IsArrow = IsArrow,
				VisualSkillId = VisualSkillId,
				ElementId = ElementId,
				HasLifeSteal = HasLifeSteal,
				Speed = Speed * 0.95f,
				MaxRange = ChainSearchRadius,
				HitRadius = HitRadius,
				InitialTarget = childTarget,
				LaunchDirection = direction,
				SpawnOrigin = origin + Vector3.Up * 0.1f,
				// Children carry no further behaviors so splits cannot cascade forever.
				Behavior = new ProjectileBehaviorProfile(),
			};
			foreach (SimpleActor seen in _alreadyHit)
			{
				child._alreadyHit.Add(seen);
			}

			AddSibling(child);
		}
	}

	private bool TryChain(Vector3 fromPosition)
	{
		if (Attacker == null)
		{
			return false;
		}

		SimpleActor? next = null;
		float bestDistanceSq = ChainSearchRadius * ChainSearchRadius;
		foreach (SimpleActor candidate in Attacker.FindProjectileTargets(fromPosition, ChainSearchRadius, _alreadyHit))
		{
			float distanceSq = fromPosition.DistanceSquaredTo(candidate.GlobalPosition);
			if (distanceSq <= bestDistanceSq)
			{
				bestDistanceSq = distanceSq;
				next = candidate;
			}
		}

		if (next == null)
		{
			return false;
		}

		Behavior.ChainBounces--;
		Damage = Mathf.Max(Mathf.RoundToInt(Damage * ChainDamageScale), 1);
		SpawnSpecialEffect(SkillAttackVfx.ChainEvent, fromPosition, next.GlobalPosition - fromPosition, HitRadius);
		_homingTarget = next;
		_direction = FlattenOrDefault(next.GlobalPosition - GlobalPosition, _direction);
		_traveled = 0.0f;
		MaxRange = ChainSearchRadius + 2.0f;
		FaceTravelDirection();
		return true;
	}

	private void Finish(bool spawnDissipate)
	{
		if (_finished)
		{
			return;
		}

		_finished = true;
		if (spawnDissipate)
		{
			SpawnSpecialEffect(SkillAttackVfx.DissipateEvent, GlobalPosition, _direction, IsMelee ? HitRadius : HitRadius * 1.15f);
		}

		QueueFree();
	}

	private void SpawnImpactPulse(Vector3 position, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnImpact(parent, position + Vector3.Up * 0.10f, _direction, VisualSkillId, ElementId, EffectColor, radius, Behavior, HasLifeSteal);
	}

	private void SpawnSpecialEffect(string eventId, Vector3 position, Vector3 travelVector, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		SkillAttackVfx.SpawnSpecial(parent, eventId, position + Vector3.Up * 0.10f, travelVector, VisualSkillId, ElementId, EffectColor, radius, Behavior, HasLifeSteal);
	}

	private void FaceTravelDirection()
	{
		if (_direction.LengthSquared() > 0.0001f)
		{
			LookAt(GlobalPosition + _direction, Vector3.Up);
		}
	}

	private static Vector3 FlattenOrDefault(Vector3 value, Vector3 fallback)
	{
		value.Y = 0.0f;
		return value.LengthSquared() > 0.0001f ? value.Normalized() : fallback;
	}

	private void BuildVisuals()
	{
		if (IsMelee)
		{
			int bladeCount = VisualSkillId == "gem.skill.whirlwind" ? 3 : 1;
			for (int index = 0; index < bladeCount; index++)
			{
				AddFxMesh(
					$"SlashBlade{index}",
					new BoxMesh { Size = new Vector3(HitRadius * (VisualSkillId == "gem.skill.whirlwind" ? 2.35f : 1.9f), 0.055f, HitRadius * 0.34f) },
					new Vector3(0.0f, index * 0.08f, 0.0f),
					new Vector3(0.0f, index * (360.0f / bladeCount), index % 2 == 0 ? 18.0f : -18.0f),
					new Color(1.0f, 0.92f, 0.58f, 0.90f)
				);
			}
			AddProjectileTrail(new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.78f), VisualSkillId == "gem.skill.whirlwind" ? 26 : 12, 0.24f, HitRadius * 0.045f, Vector3.Zero);
			AddSupportAccents();
			return;
		}

		if (IsArrow)
		{
			AddFxMesh(
				"ArrowShaft",
				new CapsuleMesh { Radius = 0.04f, Height = 0.9f },
				Vector3.Zero,
				new Vector3(90.0f, 0.0f, 0.0f),
				new Color(0.62f, 0.38f, 0.16f, 0.96f)
			);
			AddFxMesh(
				"ArrowHead",
				new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.11f, Height = 0.26f, RadialSegments = 12 },
				new Vector3(0.0f, 0.0f, -0.5f),
				new Vector3(90.0f, 0.0f, 0.0f),
				EffectColor
			);
			AddProjectileTrail(new Color(0.96f, 0.76f, 0.26f, 0.68f), 10, 0.22f, 0.025f, new Vector3(0.0f, -1.2f, 0.0f));
			AddSupportAccents();
			return;
		}

		if (VisualSkillId == "gem.skill.laser")
		{
			AddFxMesh("LaserCore", new CapsuleMesh { Radius = 0.075f, Height = 1.70f }, Vector3.Zero, new Vector3(90.0f, 0.0f, 0.0f), new Color(0.82f, 0.98f, 1.0f, 0.98f));
			AddFxMesh("LaserGlow", new CapsuleMesh { Radius = 0.19f, Height = 1.42f }, Vector3.Zero, new Vector3(90.0f, 0.0f, 0.0f), new Color(0.18f, 0.76f, 1.0f, 0.42f));
			AddProjectileTrail(new Color(0.26f, 0.84f, 1.0f, 0.76f), 18, 0.30f, 0.035f, Vector3.Zero);
			AddSupportAccents();
			return;
		}

		if (VisualSkillId == "gem.skill.meteor")
		{
			AddFxMesh("MeteorCore", new SphereMesh { Radius = 0.36f, Height = 0.72f }, Vector3.Zero, Vector3.Zero, new Color(0.30f, 0.20f, 0.16f, 1.0f));
			AddFxMesh("MeteorFlame", new SphereMesh { Radius = 0.48f, Height = 0.96f }, new Vector3(0.0f, 0.0f, 0.24f), Vector3.Zero, new Color(1.0f, 0.28f, 0.05f, 0.62f));
			AddProjectileTrail(new Color(1.0f, 0.18f, 0.025f, 0.86f), 34, 0.48f, 0.075f, new Vector3(0.0f, 1.4f, 0.0f));
			AddProjectileTrail(new Color(0.22f, 0.18f, 0.16f, 0.52f), 18, 0.68f, 0.13f, new Vector3(0.0f, 0.7f, 0.0f));
			AddSupportAccents();
			return;
		}

		if (VisualSkillId == "gem.skill.fireball")
		{
			AddFxMesh("FireballCore", new SphereMesh { Radius = 0.30f, Height = 0.60f }, Vector3.Zero, Vector3.Zero, new Color(1.0f, 0.78f, 0.18f, 1.0f));
			AddFxMesh("FireballFlame", new SphereMesh { Radius = 0.42f, Height = 0.84f }, new Vector3(0.0f, 0.0f, 0.18f), Vector3.Zero, new Color(1.0f, 0.18f, 0.03f, 0.58f));
			AddProjectileTrail(new Color(1.0f, 0.24f, 0.035f, 0.88f), 28, 0.42f, 0.06f, new Vector3(0.0f, 0.9f, 0.0f));
			AddSupportAccents();
			return;
		}

		AddFxMesh(
			"AttackOrb",
			new SphereMesh { Radius = 0.22f, Height = 0.44f },
			Vector3.Zero,
			Vector3.Zero,
			EffectColor
		);
		AddFxMesh(
			"Trail",
			new CapsuleMesh { Radius = 0.06f, Height = 0.72f },
			new Vector3(0.0f, 0.0f, 0.4f),
			new Vector3(90.0f, 0.0f, 0.0f),
			new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * 0.5f)
		);
		AddProjectileTrail(new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.66f), 18, 0.34f, 0.04f, Vector3.Zero);
		AddSupportAccents();
	}

	private void AddSupportAccents()
	{
		if (Behavior.SplitCount > 0)
		{
			AddFxMesh("SplitShardLeft", new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.55f) }, new Vector3(-0.22f, 0.0f, 0.05f), new Vector3(0.0f, -24.0f, 18.0f), new Color(0.82f, 0.92f, 1.0f, 0.78f));
			AddFxMesh("SplitShardRight", new BoxMesh { Size = new Vector3(0.07f, 0.07f, 0.55f) }, new Vector3(0.22f, 0.0f, 0.05f), new Vector3(0.0f, 24.0f, -18.0f), new Color(0.82f, 0.92f, 1.0f, 0.78f));
		}
		if (Behavior.ChainBounces > 0)
		{
			AddFxMesh("ChainSparkA", new BoxMesh { Size = new Vector3(0.55f, 0.035f, 0.035f) }, Vector3.Zero, new Vector3(24.0f, 35.0f, 18.0f), new Color(0.72f, 0.94f, 1.0f, 0.88f));
			AddFxMesh("ChainSparkB", new BoxMesh { Size = new Vector3(0.035f, 0.55f, 0.035f) }, Vector3.Zero, new Vector3(-18.0f, -20.0f, 32.0f), new Color(0.72f, 0.94f, 1.0f, 0.72f));
		}
		if (Behavior.PierceCount > 0)
		{
			AddFxMesh("PiercingTip", new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.15f, Height = 0.48f, RadialSegments = 12 }, new Vector3(0.0f, 0.0f, -0.52f), new Vector3(90.0f, 0.0f, 0.0f), new Color(0.92f, 0.98f, 1.0f, 0.92f));
		}
		if (Behavior.ExplosionRadius > 0.0f)
		{
			AddFxMesh("ExplosionCharge", new SphereMesh { Radius = 0.50f, Height = 1.0f }, Vector3.Zero, Vector3.Zero, new Color(1.0f, 0.30f, 0.045f, 0.20f));
		}
		if (HasLifeSteal)
		{
			AddFxMesh("LifeStealCore", new SphereMesh { Radius = 0.12f, Height = 0.24f }, new Vector3(0.0f, 0.18f, 0.0f), Vector3.Zero, new Color(0.72f, 0.16f, 0.92f, 0.78f));
		}
	}

	private void AddProjectileTrail(Color color, int amount, float lifetime, float size, Vector3 gravity)
	{
		var particleMaterial = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 2.7f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		var trailMesh = new QuadMesh
		{
			Size = new Vector2(Mathf.Max(size, 0.022f), Mathf.Max(size * 3.2f, 0.075f)),
			Material = particleMaterial,
		};
		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = Mathf.Max(size * 1.8f, 0.04f),
			Direction = Vector3.Back,
			Spread = 38.0f,
			InitialVelocityMin = 0.15f,
			InitialVelocityMax = 1.2f,
			Gravity = gravity,
			ScaleMin = 0.55f,
			ScaleMax = 1.25f,
			Color = color,
		};
		AddChild(new GpuParticles3D
		{
			Name = "ProjectileTrail",
			Amount = amount,
			Lifetime = lifetime,
			LocalCoords = false,
			Randomness = 0.55f,
			ProcessMaterial = processMaterial,
			DrawPass1 = trailMesh,
			Emitting = true,
		});
	}

	private void AddFxMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.2f,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 2.2f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
		_materials.Add(material);

		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		AddChild(meshInstance);
	}
}
