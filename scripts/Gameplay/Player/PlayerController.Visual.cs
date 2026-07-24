using Godot;
using System.Collections.Generic;

public partial class PlayerController
{

	private void UpdateMovementEffects(float step, float targetSpeed)
	{
		_footstepEffectRemaining = Mathf.Max(_footstepEffectRemaining - step, 0.0f);
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		if (!IsOnFloor() || speed < 1.2f || _footstepEffectRemaining > 0.0f)
		{
			return;
		}

		bool isFastStep = speed > WalkSpeed * 1.12f || targetSpeed > WalkSpeed + 0.1f;
		SpawnMovementDust(planarVelocity.Normalized(), speed, isFastStep);
		_footstepEffectRemaining = isFastStep ? 0.13f : 0.22f;
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
		float footSide = Mathf.Sin(Time.GetTicksMsec() * 0.018f) >= 0.0f ? 1.0f : -1.0f;
		var dust = new MovementDustEffect
		{
			DustColor = isFastStep ? new Color(0.86f, 0.78f, 0.52f, 0.74f) : new Color(0.68f, 0.62f, 0.48f, 0.62f),
			Radius = isFastStep ? 0.24f : 0.17f,
			Lifetime = isFastStep ? 0.34f : 0.44f,
			IsFastStep = isFastStep,
			DirectionYaw = Mathf.RadToDeg(Mathf.Atan2(-moveDirection.X, -moveDirection.Z)),
		};
		parent.AddChild(dust);
		dust.GlobalPosition = GlobalPosition + back * Mathf.Clamp(speed * 0.035f, 0.18f, 0.45f) + side * footSide * 0.18f + Vector3.Up * 0.04f;
	}

	private void UpdateMovementAnimation(float step)
	{
		Vector3 planarVelocity = Velocity;
		planarVelocity.Y = 0.0f;
		float speed = planarVelocity.Length();
		float moveRatio = Mathf.Clamp(speed / Mathf.Max(SprintSpeed, 0.01f), 0.0f, 1.0f);
		bool isMoving = speed > 0.25f && IsOnFloor();
		if (MountedCompanion != null)
		{
			if (_playerExternalModel != null)
			{
				SetPlayerExternalAnimationState("idle");
				StabilizePlayerExternalModel();
				return;
			}

			ApplyMountedPose();
			return;
		}

		if (_playerExternalModel != null)
		{
			string state = !isMoving ? "idle" : moveRatio > 0.72f ? "run" : "walk";
			SetPlayerExternalAnimationState(state);
			StabilizePlayerExternalModel();
			return;
		}

		float phaseSpeed = Mathf.Lerp(6.2f, 11.2f, moveRatio);

		if (isMoving)
		{
			_movementAnimationPhase += step * phaseSpeed;
		}
		else
		{
			_movementAnimationPhase = Mathf.Lerp(_movementAnimationPhase, 0.0f, Mathf.Min(step * 8.0f, 1.0f));
		}

		float swing = Mathf.Sin(_movementAnimationPhase);
		float counterSwing = Mathf.Sin(_movementAnimationPhase + Mathf.Pi);
		float lift = Mathf.Abs(Mathf.Cos(_movementAnimationPhase));
		float intensity = isMoving ? Mathf.Lerp(0.45f, 1.0f, moveRatio) : 0.0f;
		float bob = Mathf.Abs(swing) * 0.045f * intensity;
		float lean = Mathf.Clamp(speed / Mathf.Max(SprintSpeed, 0.01f), 0.0f, 1.0f) * -4.5f;

		SetVisualPosition("PlayerCoatBody", new Vector3(0.0f, 1.02f + bob, 0.0f));
		SetVisualPosition("PlayerChestArmor", new Vector3(0.0f, 1.28f + bob, -0.255f));
		SetVisualPosition("PlayerFrontPanel", new Vector3(0.0f, 0.98f + bob, -0.275f));
		SetVisualPosition("PlayerChestTrim", new Vector3(0.0f, 1.42f + bob, -0.30f));
		SetVisualRotation("PlayerCoatBody", new Vector3(lean, 0.0f, swing * 1.6f * intensity));

		SetVisualRotation("PlayerLeftLeg", new Vector3(swing * 28.0f * intensity, 0.0f, -2.0f * intensity));
		SetVisualRotation("PlayerRightLeg", new Vector3(counterSwing * 28.0f * intensity, 0.0f, 2.0f * intensity));
		SetVisualPosition("PlayerLeftBoot", new Vector3(-0.16f, 0.07f + Mathf.Max(counterSwing, 0.0f) * 0.07f * intensity, -0.055f + swing * 0.05f * intensity));
		SetVisualPosition("PlayerRightBoot", new Vector3(0.16f, 0.07f + Mathf.Max(swing, 0.0f) * 0.07f * intensity, -0.055f + counterSwing * 0.05f * intensity));

		SetVisualRotation("PlayerLeftSleeve", new Vector3(counterSwing * 24.0f * intensity, 0.0f, -11.0f - swing * 5.0f * intensity));
		SetVisualRotation("PlayerRightSleeve", new Vector3(swing * 24.0f * intensity, 0.0f, 11.0f - counterSwing * 5.0f * intensity));
		SetVisualPosition("PlayerLeftGlove", new Vector3(-0.48f, 0.70f + counterSwing * 0.06f * intensity, -0.03f - counterSwing * 0.08f * intensity));
		SetVisualPosition("PlayerRightGlove", new Vector3(0.48f, 0.70f + swing * 0.06f * intensity, -0.03f - swing * 0.08f * intensity));

		SetVisualRotation("PlayerCape", new Vector3(-8.0f + Mathf.Abs(swing) * 7.0f * intensity, 0.0f, -swing * 2.5f * intensity));
		SetVisualRotation("PlayerScarfTail", new Vector3(-12.0f - moveRatio * 12.0f, 0.0f, -12.0f + swing * 5.0f * intensity));
	}

	private void ApplyMountedPose()
	{
		SetVisualRotation("PlayerLeftLeg", new Vector3(74.0f, 0.0f, -20.0f));
		SetVisualRotation("PlayerRightLeg", new Vector3(74.0f, 0.0f, 20.0f));
		SetVisualPosition("PlayerLeftLeg", new Vector3(-0.27f, 0.43f, -0.20f));
		SetVisualPosition("PlayerRightLeg", new Vector3(0.27f, 0.43f, -0.20f));
		SetVisualPosition("PlayerLeftBoot", new Vector3(-0.36f, 0.24f, -0.48f));
		SetVisualPosition("PlayerRightBoot", new Vector3(0.36f, 0.24f, -0.48f));
		SetVisualRotation("PlayerLeftBoot", new Vector3(8.0f, 0.0f, 0.0f));
		SetVisualRotation("PlayerRightBoot", new Vector3(8.0f, 0.0f, 0.0f));
	}

	private void SetPlayerExternalAnimationState(string state)
	{
		if (_playerExternalModel == null || _playerExternalAnimationState == state)
		{
			return;
		}

		_playerExternalAnimationState = state;
		ExternalModelLibrary.TryPlayActorAnimation(_playerExternalModel, state);
		StabilizePlayerExternalModel();
	}

	private void StabilizePlayerExternalModel()
	{
		if (_playerExternalModel != null)
		{
			SimpleActor? mount = MountedCompanion;
			Vector3 ridingOffset = mount != null ? Vector3.Up * mount.MountSeatHeight : Vector3.Zero;
			ExternalModelLibrary.StabilizeRootMotion(_playerExternalModel, ridingOffset, new Vector3(0.0f, 180.0f, 0.0f));
		}
	}

	private void SetVisualPosition(string nodeName, Vector3 position)
	{
		if (GetCachedPlayerVisualNode(nodeName) is Node3D node)
		{
			node.Position = position;
		}
	}

	private void SetVisualRotation(string nodeName, Vector3 rotationDegrees)
	{
		if (GetCachedPlayerVisualNode(nodeName) is Node3D node)
		{
			node.RotationDegrees = rotationDegrees;
		}
	}

	private Node3D? GetCachedPlayerVisualNode(string nodeName)
	{
		if (_playerVisualNodeCache.TryGetValue(nodeName, out Node3D? cachedNode))
		{
			if (cachedNode == null || IsInstanceValid(cachedNode))
			{
				return cachedNode;
			}

			_playerVisualNodeCache.Remove(nodeName);
		}

		Node3D? node = _playerVisualRoot?.FindChild(nodeName, true, false) as Node3D;
		_playerVisualNodeCache[nodeName] = node;
		return node;
	}

	private Vector3 SlowPlayerToStop(Vector3 velocity, float step)
	{
		velocity.X = Mathf.MoveToward(velocity.X, 0.0f, WalkSpeed * 7.0f * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, 0.0f, WalkSpeed * 7.0f * step);
		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		return velocity;
	}

	private void UpdateSafeGroundPosition()
	{
		if (!IsOnFloor())
		{
			return;
		}

		if (GlobalPosition.Y < -0.2f || GetFloorNormal().Dot(Vector3.Up) < 0.65f)
		{
			return;
		}

		_lastSafePosition = GlobalPosition + Vector3.Up * 0.18f;
	}

	private void RecoverIfOutOfWorld()
	{
		if (GlobalPosition.Y > FallRespawnHeight)
		{
			return;
		}

		TeleportToSafePosition();
	}

	private void TeleportToSafePosition()
	{
		Vector3 safePosition = _lastSafePosition;
		if (safePosition.Y < 0.05f)
		{
			safePosition.Y = 0.35f;
		}

		GlobalPosition = safePosition;
		Velocity = Vector3.Zero;
	}

	private void FaceMovementDirection(Vector3 direction, float step)
	{
		float targetAngle = Mathf.Atan2(-direction.X, -direction.Z);
		Vector3 rotation = Rotation;
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, Mathf.Min(step * 22.0f, 1.0f));
		Rotation = rotation;
	}

	// Floating nickname above the local player's head (the name chosen at
	// character creation / loaded from the save).
	private void CreatePlayerNameplate()
	{
		if (_playerNameLabel != null && IsInstanceValid(_playerNameLabel))
		{
			RefreshPlayerNameplate();
			return;
		}

		_playerNameLabel = new Label3D
		{
			Name = "PlayerNameplate",
			Text = LocalizedPlayerName,
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = Mathf.RoundToInt(PlayerNameplateBaseFont * SimpleActor.NameplateScale),
			OutlineSize = 10,
			PixelSize = 0.0075f,
			Modulate = new Color(0.6f, 1.0f, 0.78f),
			NoDepthTest = true,
			HorizontalAlignment = HorizontalAlignment.Center,
		};
		_playerNameLabel.OutlineModulate = new Color(0.02f, 0.03f, 0.03f, 0.95f);
		AddChild(_playerNameLabel);
	}

	private const int PlayerNameplateBaseFont = 20;

	// Updates the player's overhead nickname text + size (nickname changes and
	// the shared nameplate scale setting both route here).
	public void RefreshPlayerNameplate()
	{
		if (_playerNameLabel != null && IsInstanceValid(_playerNameLabel))
		{
			_playerNameLabel.Text = LocalizedPlayerName;
			_playerNameLabel.FontSize = Mathf.RoundToInt(PlayerNameplateBaseFont * SimpleActor.NameplateScale);
		}
	}

	// New game: pull the model + name chosen on the character-select screen.
	private void ApplyNewGameCharacterChoice()
	{
		if (GameLaunchOptions.LoadSaveOnWorldReady)
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(GameLaunchOptions.NewGamePlayerName))
		{
			PlayerName = GameLaunchOptions.NewGamePlayerName;
		}

		if (!string.IsNullOrWhiteSpace(GameLaunchOptions.NewGamePlayerModelPath))
		{
			PlayerModelPath = GameLaunchOptions.NewGamePlayerModelPath;
		}
	}

	// Swap the player's external model at runtime (used after loading a save
	// whose character differs from the one built during _Ready).
	public void RebuildPlayerExternalModel()
	{
		Node3D? existing = GetNodeOrNull<Node3D>("PlayerExternalModel");
		if (existing != null)
		{
			// Detach immediately (rename + remove) BEFORE rebuilding: QueueFree is
			// deferred, so if the old node stayed in the tree TryAddPlayerModel would
			// find the still-named "PlayerExternalModel" and hand back the node we're
			// about to free — leaving the player with no visible model after load.
			if (existing == _playerExternalModel)
			{
				_playerExternalModel = null;
			}

			existing.Name = "PlayerExternalModelDiscarded";
			RemoveChild(existing);
			existing.QueueFree();
		}

		Node3D? rebuilt = ExternalModelLibrary.TryAddPlayerModel(this, PlayerModelPath);
		if (rebuilt != null)
		{
			_playerExternalModel = rebuilt;
			_playerVisualRoot = rebuilt;
			AddPlayerExternalEquipment();
		}
	}

	private void CreatePlayerVisual()
	{
		_playerExternalModel = ExternalModelLibrary.TryAddPlayerModel(this, PlayerModelPath);
		if (_playerExternalModel != null)
		{
			_playerVisualRoot = _playerExternalModel;
			AddPlayerExternalEquipment();
			return;
		}

		_playerVisualRoot = new Node3D { Name = "PlayerVisualRoot" };
		AddChild(_playerVisualRoot);

		var matCoat = MakeMaterial(new Color(0.18f, 0.36f, 0.62f));
		var matCoatDark = MakeMaterial(new Color(0.08f, 0.19f, 0.32f));
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		var matScarf = MakeMaterial(new Color(0.68f, 0.10f, 0.12f));
		var matSkin = MakeMaterial(new Color(0.86f, 0.62f, 0.44f));
		var matLeather = MakeMaterial(new Color(0.22f, 0.14f, 0.09f));
		var matDark = MakeMaterial(new Color(0.06f, 0.07f, 0.08f));
		var matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.36f);
		var matEye = MakeMaterial(new Color(0.98f, 0.96f, 0.88f), 0.35f);

		AddVisualMesh("PlayerCoatBody", new CapsuleMesh { Radius = 0.31f, Height = 1.06f }, new Vector3(0.0f, 1.02f, 0.0f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.76f), matCoat);
		AddVisualMesh("PlayerChestArmor", new BoxMesh { Size = new Vector3(0.56f, 0.24f, 0.065f) }, new Vector3(0.0f, 1.28f, -0.255f), Vector3.Zero, Vector3.One, matMetal);
		AddVisualMesh("PlayerFrontPanel", new BoxMesh { Size = new Vector3(0.34f, 0.60f, 0.058f) }, new Vector3(0.0f, 0.98f, -0.275f), Vector3.Zero, Vector3.One, matCoatDark);
		AddVisualMesh("PlayerChestTrim", new BoxMesh { Size = new Vector3(0.60f, 0.065f, 0.066f) }, new Vector3(0.0f, 1.42f, -0.30f), Vector3.Zero, Vector3.One, matTrim);
		AddVisualMesh("PlayerSash", new BoxMesh { Size = new Vector3(0.12f, 0.80f, 0.065f) }, new Vector3(-0.12f, 1.13f, -0.315f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matScarf);
		AddVisualMesh("PlayerBelt", new BoxMesh { Size = new Vector3(0.70f, 0.11f, 0.13f) }, new Vector3(0.0f, 0.72f, -0.02f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerBeltBuckle", new BoxMesh { Size = new Vector3(0.15f, 0.13f, 0.055f) }, new Vector3(0.0f, 0.72f, -0.29f), Vector3.Zero, Vector3.One, matTrim);

		AddVisualMesh("PlayerHead", new SphereMesh { Radius = 0.27f, Height = 0.54f }, new Vector3(0.0f, 1.67f, 0.0f), Vector3.Zero, new Vector3(0.94f, 1.05f, 0.92f), matSkin);
		AddVisualMesh("PlayerHairCap", new SphereMesh { Radius = 0.30f, Height = 0.36f }, new Vector3(0.0f, 1.83f, 0.03f), Vector3.Zero, new Vector3(1.04f, 0.50f, 0.94f), matDark);
		AddVisualMesh("PlayerHairBangLeft", new SphereMesh { Radius = 0.10f, Height = 0.12f }, new Vector3(-0.10f, 1.75f, -0.22f), Vector3.Zero, new Vector3(1.1f, 0.55f, 0.8f), matDark);
		AddVisualMesh("PlayerHairBangRight", new SphereMesh { Radius = 0.09f, Height = 0.11f }, new Vector3(0.09f, 1.74f, -0.23f), Vector3.Zero, new Vector3(1.0f, 0.5f, 0.8f), matDark);
		AddPlayerEye("Left", new Vector3(-0.095f, 1.68f, -0.245f), 0.032f, matEye, matDark);
		AddPlayerEye("Right", new Vector3(0.095f, 1.68f, -0.245f), 0.032f, matEye, matDark);
		AddVisualMesh("PlayerNose", new CapsuleMesh { Radius = 0.022f, Height = 0.08f }, new Vector3(0.0f, 1.63f, -0.275f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, matSkin);
		AddVisualMesh("PlayerScarfCollar", new CylinderMesh { TopRadius = 0.31f, BottomRadius = 0.33f, Height = 0.08f, RadialSegments = 24 }, new Vector3(0.0f, 1.43f, 0.0f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.82f), matScarf);
		AddVisualMesh("PlayerScarfTail", new BoxMesh { Size = new Vector3(0.14f, 0.44f, 0.055f) }, new Vector3(-0.24f, 1.18f, 0.32f), new Vector3(-12.0f, 0.0f, -12.0f), Vector3.One, matScarf);

		AddVisualMesh("PlayerLeftShoulder", new SphereMesh { Radius = 0.13f, Height = 0.16f }, new Vector3(-0.35f, 1.34f, -0.02f), Vector3.Zero, new Vector3(1.35f, 0.55f, 0.95f), matMetal);
		AddVisualMesh("PlayerRightShoulder", new SphereMesh { Radius = 0.13f, Height = 0.16f }, new Vector3(0.35f, 1.34f, -0.02f), Vector3.Zero, new Vector3(1.35f, 0.55f, 0.95f), matMetal);
		AddVisualMesh("PlayerLeftSleeve", new CapsuleMesh { Radius = 0.082f, Height = 0.54f }, new Vector3(-0.43f, 1.03f, 0.0f), new Vector3(0.0f, 0.0f, -11.0f), Vector3.One, matCoat);
		AddVisualMesh("PlayerRightSleeve", new CapsuleMesh { Radius = 0.082f, Height = 0.54f }, new Vector3(0.43f, 1.03f, 0.0f), new Vector3(0.0f, 0.0f, 11.0f), Vector3.One, matCoat);
		AddVisualMesh("PlayerLeftGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(-0.48f, 0.70f, -0.03f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerRightGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(0.48f, 0.70f, -0.03f), Vector3.Zero, Vector3.One, matLeather);

		AddVisualMesh("PlayerLeftLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.72f }, new Vector3(-0.16f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerRightLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.72f }, new Vector3(0.16f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerLeftBoot", new BoxMesh { Size = new Vector3(0.23f, 0.14f, 0.36f) }, new Vector3(-0.16f, 0.07f, -0.055f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerRightBoot", new BoxMesh { Size = new Vector3(0.23f, 0.14f, 0.36f) }, new Vector3(0.16f, 0.07f, -0.055f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerCape", new BoxMesh { Size = new Vector3(0.62f, 0.90f, 0.055f) }, new Vector3(0.0f, 1.02f, 0.38f), new Vector3(-8.0f, 0.0f, 0.0f), Vector3.One, matTrim);
		AddVisualMesh("PlayerBackBlade", new BoxMesh { Size = new Vector3(0.075f, 0.90f, 0.045f) }, new Vector3(0.44f, 1.10f, 0.36f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matMetal);
		AddVisualMesh("PlayerBackBladeGuard", new BoxMesh { Size = new Vector3(0.30f, 0.055f, 0.055f) }, new Vector3(0.30f, 0.73f, 0.36f), new Vector3(0.0f, 0.0f, -24.0f), Vector3.One, matTrim);
	}

	private void AddPlayerExternalEquipment()
	{
		var equipmentRoot = new Node3D
		{
			Name = "PlayerExternalEquipment",
			Position = Vector3.Zero,
			Scale = new Vector3(0.88f, 0.88f, 0.88f),
		};
		AddChild(equipmentRoot);

		bool swordAdded = ExternalModelLibrary.TryAddModel(
			equipmentRoot,
			"res://assets/models/player/sword_2handed_color.gltf",
			"BackSword",
			new Vector3(0.34f, 1.10f, 0.34f),
			new Vector3(12.0f, 0.0f, -28.0f),
			new Vector3(0.82f, 0.82f, 0.82f)
		);
		if (!swordAdded)
		{
			AddFallbackBackSword(equipmentRoot);
		}

		bool shieldAdded = ExternalModelLibrary.TryAddModel(
			equipmentRoot,
			"res://assets/models/player/shield_badge_color.gltf",
			"BackShield",
			new Vector3(-0.34f, 1.08f, 0.34f),
			new Vector3(8.0f, 180.0f, 18.0f),
			new Vector3(0.82f, 0.82f, 0.82f)
		);
		if (!shieldAdded)
		{
			AddFallbackBackShield(equipmentRoot);
		}
	}

	private void AddFallbackBackSword(Node3D parent)
	{
		var matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.36f);
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		AddEquipmentMesh(parent, "BackSwordBlade", new BoxMesh { Size = new Vector3(0.07f, 0.92f, 0.045f) }, new Vector3(0.34f, 1.10f, 0.34f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matMetal);
		AddEquipmentMesh(parent, "BackSwordGuard", new BoxMesh { Size = new Vector3(0.30f, 0.055f, 0.055f) }, new Vector3(0.23f, 0.72f, 0.28f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matTrim);
		AddEquipmentMesh(parent, "BackSwordGrip", new BoxMesh { Size = new Vector3(0.055f, 0.25f, 0.055f) }, new Vector3(0.16f, 0.58f, 0.25f), new Vector3(12.0f, 0.0f, -28.0f), Vector3.One, matTrim);
	}

	private void AddFallbackBackShield(Node3D parent)
	{
		var matMetal = MakeMaterial(new Color(0.58f, 0.64f, 0.70f), 0.28f);
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		AddEquipmentMesh(parent, "BackShieldPlate", new CylinderMesh { TopRadius = 0.31f, BottomRadius = 0.31f, Height = 0.075f, RadialSegments = 28 }, new Vector3(-0.34f, 1.08f, 0.34f), new Vector3(90.0f, 0.0f, 18.0f), new Vector3(0.92f, 1.18f, 0.92f), matMetal);
		AddEquipmentMesh(parent, "BackShieldBoss", new SphereMesh { Radius = 0.095f, Height = 0.12f }, new Vector3(-0.34f, 1.08f, 0.29f), new Vector3(0.0f, 0.0f, 18.0f), new Vector3(1.0f, 0.55f, 1.0f), matTrim);
		AddEquipmentMesh(parent, "BackShieldBand", new BoxMesh { Size = new Vector3(0.11f, 0.55f, 0.045f) }, new Vector3(-0.34f, 1.08f, 0.27f), new Vector3(0.0f, 0.0f, 18.0f), Vector3.One, matTrim);
	}

	private static void AddEquipmentMesh(Node3D parent, string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(meshInstance);
	}

	private void AddPlayerEye(string side, Vector3 position, float radius, Material eyeMaterial, Material pupilMaterial)
	{
		AddVisualMesh($"Player{side}EyeWhite", new SphereMesh { Radius = radius, Height = radius * 2.0f }, position, Vector3.Zero, new Vector3(1.0f, 1.0f, 0.45f), eyeMaterial);
		AddVisualMesh($"Player{side}EyePupil", new SphereMesh { Radius = radius * 0.45f, Height = radius * 0.9f }, position + new Vector3(0.0f, 0.0f, -radius * 0.72f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.35f), pupilMaterial);
	}

	private void AddVisualMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		(_playerVisualRoot ?? this).AddChild(meshInstance);
	}

	private void UpdateMountedVisualOffset()
	{
		if (_playerVisualRoot != null && IsInstanceValid(_playerVisualRoot))
		{
			SimpleActor? mount = MountedCompanion;
			_playerVisualRoot.Position = mount != null ? Vector3.Up * mount.MountSeatHeight : Vector3.Zero;
		}
	}

	private static StandardMaterial3D MakeMaterial(Color color, float roughness = 0.82f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};
	}

}
