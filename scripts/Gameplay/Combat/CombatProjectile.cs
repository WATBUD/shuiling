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
		GlobalPosition += _direction * distance;
		_traveled += distance;
		RotationDegrees += IsMelee
			? new Vector3(0.0f, 900.0f * step, 0.0f)
			: new Vector3(220.0f * step, 0.0f, 420.0f * step);

		SimpleActor? victim = FindImpact();
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

	private SimpleActor? FindImpact()
	{
		if (Attacker == null)
		{
			return null;
		}

		SimpleActor? closest = null;
		float bestDistanceSq = HitRadius * HitRadius;
		foreach (SimpleActor candidate in Attacker.FindProjectileTargets(GlobalPosition, HitRadius, _alreadyHit))
		{
			float distanceSq = GlobalPosition.DistanceSquaredTo(candidate.GlobalPosition);
			if (distanceSq <= bestDistanceSq)
			{
				bestDistanceSq = distanceSq;
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
			Behavior.PierceCount--;
			return;
		}

		Finish(true);
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

		var blast = new CombatEffect
		{
			Text = string.Empty,
			EffectColor = new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.85f),
			Lifetime = 0.36f,
			Radius = Behavior.ExplosionRadius,
		};
		AddSibling(blast);
		blast.GlobalPosition = center;
	}

	private void SpawnSplitChildren(Vector3 origin)
	{
		if (Attacker == null)
		{
			return;
		}

		int childDamage = Mathf.Max(Mathf.RoundToInt(Damage * SplitDamageScale), 1);
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
		_homingTarget = next;
		_direction = FlattenOrDefault(next.GlobalPosition - GlobalPosition, _direction);
		_traveled = 0.0f;
		MaxRange = ChainSearchRadius + 2.0f;
		FaceTravelDirection();
		return true;
	}

	private void Finish(bool spawnPulse)
	{
		if (_finished)
		{
			return;
		}

		_finished = true;
		if (spawnPulse)
		{
			SpawnImpactPulse(GlobalPosition, IsMelee ? HitRadius * 1.1f : HitRadius * 1.35f);
		}

		QueueFree();
	}

	private void SpawnImpactPulse(Vector3 position, float radius)
	{
		var effect = new CombatEffect
		{
			Text = string.Empty,
			EffectColor = EffectColor,
			Lifetime = IsMelee ? 0.2f : 0.28f,
			Radius = radius,
		};
		AddSibling(effect);
		effect.GlobalPosition = position;
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
			AddFxMesh(
				"SlashBlade",
				new BoxMesh { Size = new Vector3(HitRadius * 1.9f, 0.06f, HitRadius * 0.4f) },
				Vector3.Zero,
				new Vector3(0.0f, 0.0f, 24.0f),
				new Color(1.0f, 0.92f, 0.58f, 0.92f)
			);
			AddFxMesh(
				"SlashGlow",
				new CylinderMesh { TopRadius = HitRadius * 0.9f, BottomRadius = HitRadius * 0.9f, Height = 0.03f, RadialSegments = 32 },
				Vector3.Zero,
				new Vector3(90.0f, 0.0f, 0.0f),
				new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * 0.55f)
			);
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
			EmissionEnergyMultiplier = 0.8f,
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
