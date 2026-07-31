using Godot;

// Movement effects, procedural body animation, external-model animation glue,
// and the cached-child / wander helpers for SimpleActor. Split out of the core
// file (Stage-0 separation — see docs/ARCHITECTURE_REVIEW.md). Pure code move:
// shared state stays in SimpleActor.cs and is reached as the same class.
public partial class SimpleActor : CharacterBody3D
{
	private void UpdateMovementEffects(float step)
	{
		_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining - step, 0.0f);
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		if (_isCaptured && _followSlot >= RearCompanionDustStartSlot)
		{
			_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining, speed > EffectiveMoveSpeed * 1.65f ? 0.22f : 0.36f);
			return;
		}

		if (!IsOnFloor() || speed < 0.85f || _footstepEffectRemaining > 0.0f)
		{
			return;
		}

		bool isFastStep = speed > Mathf.Max(EffectiveMoveSpeed * 1.65f, 3.6f);
		SpawnMovementDust(planarVelocity.Normalized(), speed, isFastStep);
		_footstepEffectRemaining = isFastStep ? 0.16f : 0.28f;
	}

	private void SpawnMovementDust(Vector3 moveDirection, float speed, bool isFastStep)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		Vector3 back = -moveDirection;
		Vector3 side = new(-moveDirection.Z, 0.0f, moveDirection.X);
		float footSide = Mathf.Sin((Time.GetTicksMsec() + GetInstanceId()) * 0.016f) >= 0.0f ? 1.0f : -1.0f;
		Color color = ActorKind == "monster"
			? (isFastStep ? new Color(0.82f, 0.42f, 0.32f, 0.70f) : new Color(0.62f, 0.44f, 0.36f, 0.58f))
			: (isFastStep ? new Color(0.78f, 0.86f, 0.92f, 0.66f) : new Color(0.62f, 0.70f, 0.72f, 0.54f));

		var dust = new MovementDustEffect
		{
			DustColor = color,
			Radius = isFastStep ? 0.22f : 0.15f,
			Lifetime = isFastStep ? 0.34f : 0.42f,
			IsFastStep = isFastStep,
			DirectionYaw = Mathf.RadToDeg(Mathf.Atan2(-moveDirection.X, -moveDirection.Z)),
		};
		parent.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + back * Mathf.Clamp(speed * 0.032f, 0.14f, 0.40f) + side * footSide * 0.15f + Vector3.Up * 0.04f;
	}

	private void UpdateMovementAnimation(float step)
	{
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		float speedReference = Mathf.Max(EffectiveMoveSpeed * 1.55f, 7.0f);
		float moveRatio = Mathf.Clamp(speed / speedReference, 0.0f, 1.0f);
		bool isMoving = speed > 0.18f && IsOnFloor() && !_isDefeated;
		float phaseSpeed = Mathf.Lerp(5.5f, 10.8f, moveRatio);

		if (isMoving)
		{
			_movementAnimationPhase += step * phaseSpeed;
		}
		else
		{
			_movementAnimationPhase = Mathf.Lerp(_movementAnimationPhase, 0.0f, Mathf.Min(step * 8.0f, 1.0f));
		}

		UpdateExternalMovementAnimation(step, isMoving, speed);

		if (ActorKind == "monster")
		{
			UpdateMonsterMovementAnimation(isMoving, moveRatio);
		}
		else
		{
			UpdateHumanoidMovementAnimation(isMoving, moveRatio);
		}
	}

	private void UpdateHumanoidMovementAnimation(bool isMoving, float moveRatio)
	{
		float swing = Mathf.Sin(_movementAnimationPhase);
		float counterSwing = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float intensity = isMoving ? Mathf.Lerp(0.38f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(swing) * 0.035f * intensity;
		float lean = moveRatio * -3.6f;

		SetChildPosition("Body", new Vector3(0.0f, 1.02f + bob, 0.0f));
		SetChildPosition("Tunic", new Vector3(0.0f, 1.04f + bob, -0.26f));
		SetChildRotation("Body", new Vector3(lean, 0.0f, swing * 1.4f * intensity));
		SetChildRotation("Head", new Vector3(Mathf.Abs(swing) * 2.0f * intensity, 0.0f, -swing * 1.2f * intensity));

		SetChildRotation("LeftLeg", new Vector3(swing * 25.0f * intensity, 0.0f, -1.8f * intensity));
		SetChildRotation("RightLeg", new Vector3(counterSwing * 25.0f * intensity, 0.0f, 1.8f * intensity));
		SetChildPosition("LeftBoot", new Vector3(-0.14f, 0.06f + Mathf.Max(counterSwing, 0.0f) * 0.06f * intensity, -0.05f + swing * 0.045f * intensity));
		SetChildPosition("RightBoot", new Vector3(0.14f, 0.06f + Mathf.Max(swing, 0.0f) * 0.06f * intensity, -0.05f + counterSwing * 0.045f * intensity));

		SetChildRotation("LeftArm", new Vector3(counterSwing * 22.0f * intensity, 0.0f, -9.0f - swing * 4.0f * intensity));
		SetChildRotation("RightArm", new Vector3(swing * 22.0f * intensity, 0.0f, 9.0f - counterSwing * 4.0f * intensity));
		SetChildPosition("LeftGlove", new Vector3(-0.44f, 0.66f + counterSwing * 0.055f * intensity, -0.03f - counterSwing * 0.075f * intensity));
		SetChildPosition("RightGlove", new Vector3(0.44f, 0.66f + swing * 0.055f * intensity, -0.03f - swing * 0.075f * intensity));
		SetChildRotation("Cape", new Vector3(-8.0f + Mathf.Abs(swing) * 6.0f * intensity, 0.0f, -swing * 2.2f * intensity));
	}

	private void UpdateMonsterMovementAnimation(bool isMoving, float moveRatio)
	{
		float phaseA = Mathf.Sin(_movementAnimationPhase);
		float phaseB = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float liftA = Mathf.Max(phaseA, 0.0f);
		float liftB = Mathf.Max(phaseB, 0.0f);
		float intensity = isMoving ? Mathf.Lerp(0.42f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(phaseA) * 0.045f * intensity;
		float lean = moveRatio * -4.8f;

		SetChildPosition("BodyCore", new Vector3(0.0f, 0.74f + bob, 0.10f));
		SetChildPosition("Head", new Vector3(0.0f, 1.18f + bob * 0.65f, -0.92f));
		SetChildRotation("BodyCore", new Vector3(lean + phaseA * 2.0f * intensity, 0.0f, phaseA * 2.2f * intensity));
		SetChildRotation("Head", new Vector3(phaseB * 3.0f * intensity, 0.0f, -phaseA * 1.5f * intensity));

		SetChildRotation("LeftForeLeg", new Vector3(7.0f + phaseA * 26.0f * intensity, 0.0f, -7.0f));
		SetChildRotation("RightForeLeg", new Vector3(7.0f + phaseB * 26.0f * intensity, 0.0f, 7.0f));
		SetChildRotation("LeftBackLeg", new Vector3(-8.0f + phaseB * 24.0f * intensity, 0.0f, -8.0f));
		SetChildRotation("RightBackLeg", new Vector3(-8.0f + phaseA * 24.0f * intensity, 0.0f, 8.0f));

		SetChildPosition("LeftFrontPaw", new Vector3(-0.42f, 0.13f + liftA * 0.08f * intensity, -0.70f + phaseA * 0.055f * intensity));
		SetChildPosition("RightFrontPaw", new Vector3(0.42f, 0.13f + liftB * 0.08f * intensity, -0.70f + phaseB * 0.055f * intensity));
		SetChildPosition("LeftBackPaw", new Vector3(-0.46f, 0.13f + liftB * 0.07f * intensity, 0.68f + phaseB * 0.05f * intensity));
		SetChildPosition("RightBackPaw", new Vector3(0.46f, 0.13f + liftA * 0.07f * intensity, 0.68f + phaseA * 0.05f * intensity));

		SetChildRotation("TailBase", new Vector3(64.0f + Mathf.Abs(phaseA) * 5.0f * intensity, phaseA * 9.0f * intensity, 0.0f));
		SetChildPosition("TailTip", new Vector3(phaseA * 0.05f * intensity, 0.38f + bob * 0.45f, 1.42f));
	}

	private void UpdateExternalMovementAnimation(float step, bool isMoving, float speed)
	{
		if (_isDefeated)
		{
			SetExternalAnimationState("death");
			return;
		}

		if (_externalAnimationLockRemaining > 0.0f)
		{
			_externalAnimationLockRemaining = Mathf.Max(_externalAnimationLockRemaining - step, 0.0f);
			return;
		}

		float runThreshold = Mathf.Max(EffectiveMoveSpeed * 1.12f, 6.4f);
		string state = isMoving
			? speed >= runThreshold ? "run" : "walk"
			: "idle";
		SetExternalAnimationState(state);
	}

	private void SetExternalAnimationState(string state, float lockDuration = 0.0f)
	{
		if (_externalAnimationState == state && lockDuration <= 0.0f)
		{
			return;
		}

		bool played = ExternalModelLibrary.TryPlayActorAnimation(this, state);
		if (played)
		{
			_externalAnimationState = state;
			StabilizeExternalModelRootMotion();
		}

		if (played && lockDuration > 0.0f)
		{
			_externalAnimationLockRemaining = lockDuration;
		}
	}

	private void SetChildPosition(string nodeName, Vector3 position)
	{
		if (GetCachedChildNode(nodeName) is Node3D node)
		{
			node.Position = position;
		}
	}

	private void SetChildRotation(string nodeName, Vector3 rotationDegrees)
	{
		if (GetCachedChildNode(nodeName) is Node3D node)
		{
			node.RotationDegrees = rotationDegrees;
		}
	}

	private Node3D? GetCachedChildNode(string nodeName)
	{
		if (_childNodeCache.TryGetValue(nodeName, out Node3D? cachedNode))
		{
			if (cachedNode == null || IsInstanceValid(cachedNode))
			{
				return cachedNode;
			}

			_childNodeCache.Remove(nodeName);
		}

		Node3D? node = GetNodeOrNull<Node3D>(nodeName);
		_childNodeCache[nodeName] = node;
		return node;
	}

	private void StabilizeExternalModelRootMotion()
	{
		Node3D? model = GetCachedExternalModelNode();
		if (model == null)
		{
			return;
		}

		ExternalModelLibrary.StabilizeRootMotion(model, Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f));
	}

	private Node3D? GetCachedExternalModelNode()
	{
		if (_externalModelNode != null)
		{
			if (IsInstanceValid(_externalModelNode))
			{
				return _externalModelNode;
			}

			_externalModelNode = null;
			_externalModelLookupAttempted = false;
		}

		if (_externalModelLookupAttempted)
		{
			return null;
		}

		_externalModelLookupAttempted = true;
		_externalModelNode = GetNodeOrNull<Node3D>("ExternalModel");
		return _externalModelNode;
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
		if (WanderRadius <= 0.05f)
		{
			return HomePosition;
		}

		float angle = (float)_rng.RandfRange(0.0f, Mathf.Tau);
		float distance = (float)_rng.RandfRange(Mathf.Min(2.0f, WanderRadius), WanderRadius);
		return HomePosition + new Vector3(Mathf.Cos(angle) * distance, 0.0f, Mathf.Sin(angle) * distance);
	}
}
