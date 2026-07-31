using Godot;

// Mole (name.monster.rat) burrow behaviour, kept in its own partial so the
// species-specific state machine does not grow the SimpleActor core further.
//
// Wiring in SimpleActor.cs is intentionally tiny (a step toward separating this
// God-class's concerns without a risky full refactor):
//   - IsInvulnerable       includes `_isBurrowed`  (damage immunity)
//   - IsActiveWorldTarget  excludes `_isBurrowed`  (drops AI target search)
//   - TickActorTimers      calls UpdateBurrow(step)
//   - RunWildActorFrame    early-returns while burrowed
// Zeroing the collision layer/mask (as EnterMourning does) also removes the
// actor from the player's raycast targeting while underground.
public partial class SimpleActor : CharacterBody3D
{
	private const string MoleSpeciesKey = "name.monster.rat";
	private const float BurrowIntervalMin = 6.0f;
	private const float BurrowIntervalMax = 10.0f;
	private const float BurrowDurationMin = 1.4f;
	private const float BurrowDurationMax = 2.4f;

	private bool _isBurrowed;
	private bool _burrowScheduled;
	private float _burrowCooldownRemaining;
	private float _burrowedRemaining;

	private bool IsMoleSpecies => ActorKind == "monster" && DisplayName == MoleSpeciesKey;

	// Ticked from TickActorTimers for mole actors (and any actor mid-burrow, so a
	// dive always gets to resurface).
	private void UpdateBurrow(float step)
	{
		if (_isBurrowed)
		{
			_burrowedRemaining -= step;
			if (_burrowedRemaining <= 0.0f || _isCaptured || _isDefeated)
			{
				Resurface();
			}

			return;
		}

		if (!IsMoleSpecies || _isCaptured || _isDefeated)
		{
			return;
		}

		if (!_burrowScheduled)
		{
			_burrowScheduled = true;
			_burrowCooldownRemaining = (float)_rng.RandfRange(BurrowIntervalMin, BurrowIntervalMax);
		}

		_burrowCooldownRemaining -= step;
		if (_burrowCooldownRemaining <= 0.0f)
		{
			Dive();
		}
	}

	private void Dive()
	{
		_isBurrowed = true;
		_burrowedRemaining = (float)_rng.RandfRange(BurrowDurationMin, BurrowDurationMax);

		_combatTarget = null;
		_retaliationTarget = null;
		Velocity = Vector3.Zero;

		EmitBurrowDust(GlobalPosition);

		// Untargetable (raycast + AI) and damage-immune while underground.
		CollisionLayer = 0;
		CollisionMask = 0;

		if (GetCachedExternalModelNode() is Node3D model)
		{
			model.Visible = false;
		}
	}

	private void Resurface()
	{
		_isBurrowed = false;
		_burrowScheduled = false; // roll a fresh interval before the next dive

		// Pop up at a new spot near home. PickWanderTarget keeps Y at the spawn
		// height, so this also undoes any gravity drift while hidden.
		Vector3 target = PickWanderTarget();
		GlobalPosition = target;
		_targetPosition = PickWanderTarget();

		EmitBurrowDust(GlobalPosition);

		if (!_isDefeated && !_isCaptured)
		{
			CollisionLayer = _defaultCollisionLayer;
			CollisionMask = _defaultCollisionMask;
		}

		if (GetCachedExternalModelNode() is Node3D model)
		{
			model.Visible = true;
		}
	}

	// A short brown dust puff at ground level. CpuParticles3D so it also renders
	// under the gl_compatibility renderer. Parented to the actor's parent so it
	// outlives the actor hiding/moving, and frees itself when the burst ends.
	private void EmitBurrowDust(Vector3 globalPosition)
	{
		Node? host = GetParent();
		if (host == null)
		{
			return;
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.55f, 0.42f, 0.30f, 0.9f),
			Roughness = 1.0f,
		};
		var mesh = new SphereMesh
		{
			Radius = 0.08f,
			Height = 0.16f,
			RadialSegments = 6,
			Rings = 3,
			Material = material,
		};

		var dust = new CpuParticles3D
		{
			Name = "BurrowDust",
			Mesh = mesh,
			Amount = 18,
			Lifetime = 0.7f,
			OneShot = true,
			Explosiveness = 0.85f,
			EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.28f,
			Direction = Vector3.Up,
			Spread = 55.0f,
			Gravity = new Vector3(0.0f, -2.4f, 0.0f),
			InitialVelocityMin = 0.9f,
			InitialVelocityMax = 2.1f,
			ScaleAmountMin = 0.6f,
			ScaleAmountMax = 1.5f,
			Color = new Color(0.55f, 0.42f, 0.30f, 0.9f),
			Emitting = true,
		};
		host.AddChild(dust);
		dust.GlobalPosition = new Vector3(globalPosition.X, 0.06f, globalPosition.Z);
		dust.Finished += dust.QueueFree;
	}
}
