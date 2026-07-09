using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float WalkSpeed { get; set; } = 6.5f;
	[Export] public float SprintSpeed { get; set; } = 10.0f;
	[Export] public float JumpVelocity { get; set; } = 5.2f;
	[Export] public float ThirdPersonDistance { get; set; } = 6.2f;
	[Export] public float ThirdPersonCameraHeight { get; set; } = 3.35f;
	[Export] public float ThirdPersonLookHeight { get; set; } = 2.2f;
	[Export] public float CameraWorldHalfExtent { get; set; } = 68.5f;
	[Export] public float FallRespawnHeight { get; set; } = -8.0f;
	[Export] public float Acceleration { get; set; } = 18.0f;
	[Export] public float CaptureCooldown { get; set; } = 0.55f;
	[Export] public int CaptureNetCapacity { get; set; } = 6;
	[Export] public float CaptureNetRechargeSeconds { get; set; } = 5.0f;
	[Export] public float TargetInfoRange { get; set; } = 30.0f;
	[Export] public string PlayerName { get; set; } = "player.default_name";
	[Export] public int Level { get; set; } = 1;
	[Export] public int MaxHealth { get; set; } = 150;
	[Export] public int CurrentHealth { get; set; } = 150;
	[Export] public int Attack { get; set; } = 16;
	[Export] public int Defense { get; set; } = 10;
	[Export] public int ActivePartyLimit { get; set; } = 20;

	private readonly List<SimpleActor> _capturedCollection = new();
	private readonly List<SimpleActor> _activeParty = new();
	private readonly Dictionary<string, int> _inventoryItems = new();
	private float _cameraYaw;
	private Vector3 _lastSafePosition = new(0.0f, 0.2f, 8.0f);
	private float _gravity;
	private float _captureCooldownRemaining;
	private int _captureNetCharges;
	private float _captureNetRechargeRemaining;
	private Node3D _cameraPivot = null!;
	private Camera3D _camera = null!;
	private TargetInfoPanel _targetInfoPanel = null!;
	private PartyPanel _partyPanel = null!;
	private InventoryPanel _inventoryPanel = null!;
	private SettingsPanel _settingsPanel = null!;
	private MinimapPanel _minimapPanel = null!;
	private PanelContainer _captureAmmoPanel = null!;
	private Label _captureAmmoCaptionLabel = null!;
	private Label _captureAmmoCountLabel = null!;
	private ProgressBar _captureAmmoRechargeBar = null!;

	public IReadOnlyList<SimpleActor> CapturedCollection => _capturedCollection;
	public IReadOnlyList<SimpleActor> ActiveParty => _activeParty;
	public IReadOnlyDictionary<string, int> InventoryItems => _inventoryItems;
	public string LocalizedPlayerName => LocaleText.T(PlayerName);
	public Vector3 MinimapForward => GetCameraPlanarForward();
	public float HealthRatio => MaxHealth <= 0 ? 0.0f : Mathf.Clamp(CurrentHealth / (float)MaxHealth, 0.0f, 1.0f);

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
		_lastSafePosition = GlobalPosition + Vector3.Up * 0.2f;
		ConfigureThirdPersonCamera();
		CreatePlayerVisual();
		CreateTargetInfoPanel();
		CreateMinimapPanel();
		CreatePartyPanel();
		CreateInventoryPanel();
		CreateSettingsPanel();
		InitializeStarterInventory();
		InitializeCaptureNetAmmo();
		CreateCaptureAmmoHud();

		AddToGroup("player");
		EnsureInputActions();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		UpdateCaptureNetRecharge((float)delta);
		UpdateThirdPersonCamera();
		UpdateTargetInfoPanel();
		UpdateCaptureAmmoHud();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_settingsPanel.Visible)
			{
				SetSettingsPanelVisible(false);
			}
			else if (_inventoryPanel.Visible)
			{
				SetInventoryPanelVisible(false);
			}
			else if (_partyPanel.Visible)
			{
				SetPartyPanelVisible(false);
			}
			else
			{
				SetSettingsPanelVisible(true);
			}

			return;
		}

		if (_settingsPanel.Visible)
		{
			return;
		}

		if (@event.IsActionPressed("inventory_panel"))
		{
			SetInventoryPanelVisible(!_inventoryPanel.Visible);
			return;
		}

		if (_inventoryPanel.Visible)
		{
			return;
		}

		if (@event.IsActionPressed("party_panel"))
		{
			SetPartyPanelVisible(!_partyPanel.Visible);
			return;
		}

		if (_partyPanel.Visible)
		{
			return;
		}

		if (@event is InputEventMouseButton { Pressed: true })
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		if (@event.IsActionPressed("capture_net"))
		{
			ThrowCaptureNet();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_settingsPanel.Visible || _partyPanel.Visible || _inventoryPanel.Visible)
		{
			Velocity = SlowPlayerToStop(Velocity, (float)delta);
			MoveAndSlide();
			UpdateSafeGroundPosition();
			RecoverIfOutOfWorld();
			return;
		}

		float step = (float)delta;
		_captureCooldownRemaining = Mathf.Max(_captureCooldownRemaining - step, 0.0f);
		Vector3 velocity = Velocity;

		if (!IsOnFloor())
		{
			velocity.Y -= _gravity * step;
		}

		if (Input.IsActionJustPressed("jump") && IsOnFloor())
		{
			velocity.Y = JumpVelocity;
		}

		Vector2 inputDirection = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		Vector3 forward = GetCameraPlanarForward();
		Vector3 right = new(-forward.Z, 0.0f, forward.X);
		Vector3 direction = (right * inputDirection.X + forward * -inputDirection.Y).Normalized();
		float targetSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;

		velocity.X = Mathf.MoveToward(velocity.X, direction.X * targetSpeed, Acceleration * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * targetSpeed, Acceleration * step);
		if (direction.LengthSquared() > 0.01f)
		{
			FaceMovementDirection(direction, step);
		}

		Velocity = velocity;
		MoveAndSlide();
		UpdateSafeGroundPosition();
		RecoverIfOutOfWorld();
	}

	private static void EnsureInputActions()
	{
		AddKeyAction("move_forward", Key.W);
		AddKeyAction("move_back", Key.S);
		AddKeyAction("move_left", Key.A);
		AddKeyAction("move_right", Key.D);
		AddKeyAction("jump", Key.Space);
		AddKeyAction("sprint", Key.Shift);
		AddKeyAction("capture_net", Key.R);
		AddKeyAction("party_panel", Key.P);
		AddKeyAction("inventory_panel", Key.I);
		AddKeyAction("ui_cancel", Key.Escape);
	}

	private void ThrowCaptureNet()
	{
		if (_captureCooldownRemaining > 0.0f || _captureNetCharges <= 0)
		{
			return;
		}

		_captureCooldownRemaining = CaptureCooldown;
		_captureNetCharges = Mathf.Max(_captureNetCharges - 1, 0);
		Vector3 direction = GetCaptureThrowDirection();
		Vector3 spawnPosition = GlobalPosition + new Vector3(0.0f, 1.18f, 0.0f) + GetCameraPlanarForward() * 1.05f;
		var net = new CaptureNet
		{
			OwnerPlayer = this,
			Direction = direction,
		};

		Node projectileParent = GetTree().CurrentScene ?? GetParent();
		projectileParent.AddChild(net);
		net.GlobalPosition = spawnPosition;
		net.AlignToDirection();
		UpdateCaptureAmmoHud();
	}

	public bool CaptureActor(SimpleActor actor)
	{
		if (!actor.CanBeCaptured || _capturedCollection.Contains(actor))
		{
			return false;
		}

		_capturedCollection.Add(actor);
		actor.Capture(this);

		if (_activeParty.Count < ActivePartyLimit)
		{
			DeployCompanion(actor, false);
		}
		else
		{
			actor.StoreInCollection();
		}

		_partyPanel.RefreshParty();
		return true;
	}

	public bool IsInActiveParty(SimpleActor actor)
	{
		return _activeParty.Contains(actor);
	}

	public bool DeployCompanion(SimpleActor actor, bool replaceLastIfFull)
	{
		if (!_capturedCollection.Contains(actor))
		{
			return false;
		}

		if (_activeParty.Contains(actor))
		{
			return true;
		}

		if (_activeParty.Count >= ActivePartyLimit)
		{
			if (!replaceLastIfFull || _activeParty.Count == 0)
			{
				return false;
			}

			StoreCompanion(_activeParty[_activeParty.Count - 1]);
		}

		_activeParty.Add(actor);
		actor.DeployToParty(this, _activeParty.Count - 1);
		_partyPanel.RefreshParty();
		return true;
	}

	public bool StoreCompanion(SimpleActor actor)
	{
		if (!_capturedCollection.Contains(actor))
		{
			return false;
		}

		bool removed = _activeParty.Remove(actor);
		actor.StoreInCollection();
		if (removed)
		{
			ReassignFollowSlots();
		}

		_partyPanel.RefreshParty();
		return true;
	}

	public int GetInventoryCount(string itemId)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return 1;
		}

		return _inventoryItems.TryGetValue(itemId, out int count) ? count : 0;
	}

	public bool HasInventoryItem(string itemId)
	{
		return BuildCatalog.IsFreeItem(itemId) || GetInventoryCount(itemId) > 0;
	}

	public void AddInventoryItem(string itemId, int amount = 1)
	{
		if (BuildCatalog.IsFreeItem(itemId))
		{
			return;
		}

		_inventoryItems.TryGetValue(itemId, out int currentCount);
		_inventoryItems[itemId] = Mathf.Max(currentCount + amount, 0);
		if (_inventoryPanel != null)
		{
			_inventoryPanel.RefreshAll();
		}
	}

	public void OpenInventoryForActor(SimpleActor actor)
	{
		SetInventoryPanelVisible(true);
		_inventoryPanel.SelectActor(actor);
	}

	public int ReceiveDamage(int rawDamage)
	{
		int mitigatedDamage = Mathf.Max(rawDamage - Mathf.RoundToInt(Defense * 0.35f), 1);
		CurrentHealth = Mathf.Max(CurrentHealth - mitigatedDamage, 0);
		SpawnFloatingEffect(mitigatedDamage.ToString(), new Color(1.0f, 0.18f, 0.14f, 0.92f), 0.48f, 0.62f);

		if (CurrentHealth <= 0)
		{
			RecoverFromKnockdown();
		}

		return mitigatedDamage;
	}

	public int ReceiveHealing(int rawHealing)
	{
		int missingHealth = Mathf.Max(MaxHealth - CurrentHealth, 0);
		int healing = Mathf.Min(Mathf.Max(rawHealing, 0), missingHealth);
		if (healing <= 0)
		{
			return 0;
		}

		CurrentHealth += healing;
		SpawnFloatingEffect($"+{healing}", new Color(0.36f, 1.0f, 0.54f, 0.92f), 0.55f, 0.48f);
		return healing;
	}

	public void GrantCombatExperience(int amount)
	{
		int experience = Mathf.Max(amount, 0);
		if (experience <= 0)
		{
			return;
		}

		foreach (SimpleActor actor in _activeParty)
		{
			if (IsInstanceValid(actor) && actor.IsInActiveParty)
			{
				actor.GrantTraining(experience);
			}
		}

		_partyPanel.RefreshParty();
	}

	private void ReassignFollowSlots()
	{
		for (int index = 0; index < _activeParty.Count; index++)
		{
			_activeParty[index].SetFollowSlot(index);
		}
	}

	private void RecoverFromKnockdown()
	{
		CurrentHealth = Mathf.Max(MaxHealth / 2, 1);
		TeleportToSafePosition();
	}

	private void SpawnDamageEffect(int damage)
	{
		SpawnFloatingEffect(damage.ToString(), new Color(1.0f, 0.18f, 0.14f, 0.92f), 0.48f, 0.62f);
	}

	private void SpawnFloatingEffect(string text, Color color, float lifetime, float radius)
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		var effect = new CombatEffect
		{
			Text = text,
			EffectColor = color,
			Lifetime = lifetime,
			Radius = radius,
		};
		parent.AddChild(effect);
		effect.GlobalPosition = GlobalPosition + new Vector3(0.0f, 1.15f, 0.0f);
	}

	private void InitializeCaptureNetAmmo()
	{
		CaptureNetCapacity = Mathf.Max(CaptureNetCapacity, 1);
		_captureNetCharges = CaptureNetCapacity;
		_captureNetRechargeRemaining = CaptureNetRechargeSeconds;
	}

	private void InitializeStarterInventory()
	{
		foreach (string itemId in BuildCatalog.GetStarterInventoryItemIds())
		{
			AddInventoryItem(itemId);
		}
	}

	private void UpdateCaptureNetRecharge(float step)
	{
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
			return;
		}

		_captureNetRechargeRemaining -= step;
		while (_captureNetRechargeRemaining <= 0.0f && _captureNetCharges < CaptureNetCapacity)
		{
			_captureNetCharges++;
			_captureNetRechargeRemaining += rechargeSeconds;
		}

		if (_captureNetCharges >= CaptureNetCapacity)
		{
			_captureNetRechargeRemaining = rechargeSeconds;
		}
	}

	private void CreateCaptureAmmoHud()
	{
		var layer = new CanvasLayer
		{
			Name = "CaptureAmmoLayer",
			Layer = 24,
		};
		AddChild(layer);

		_captureAmmoPanel = new PanelContainer
		{
			Name = "CaptureAmmoHud",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 1.0f,
			AnchorRight = 1.0f,
			AnchorTop = 1.0f,
			AnchorBottom = 1.0f,
			OffsetLeft = -224.0f,
			OffsetRight = -28.0f,
			OffsetTop = -112.0f,
			OffsetBottom = -28.0f,
		};
		var panelStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.035f, 0.041f, 0.050f, 0.78f),
			BorderColor = new Color(0.62f, 0.72f, 0.82f, 0.58f),
		};
		panelStyle.SetBorderWidthAll(1);
		panelStyle.SetCornerRadiusAll(4);
		_captureAmmoPanel.AddThemeStyleboxOverride("panel", panelStyle);
		layer.AddChild(_captureAmmoPanel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 12);
		margin.AddThemeConstantOverride("margin_right", 12);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		_captureAmmoPanel.AddChild(margin);

		var rows = new VBoxContainer();
		rows.AddThemeConstantOverride("separation", 4);
		margin.AddChild(rows);

		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 8);
		rows.AddChild(titleRow);

		var netLabel = MakeHudLabel(LocaleText.T("hud.net"), 13, new Color(0.68f, 0.80f, 0.90f));
		titleRow.AddChild(netLabel);

		_captureAmmoCaptionLabel = MakeHudLabel(LocaleText.T("hud.capture_net_key"), 13, new Color(0.86f, 0.92f, 0.96f));
		_captureAmmoCaptionLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_captureAmmoCaptionLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleRow.AddChild(_captureAmmoCaptionLabel);

		_captureAmmoCountLabel = MakeHudLabel($"6 / {CaptureNetCapacity}", 31, new Color(1.0f, 1.0f, 1.0f));
		_captureAmmoCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
		rows.AddChild(_captureAmmoCountLabel);

		_captureAmmoRechargeBar = new ProgressBar
		{
			MinValue = 0.0,
			MaxValue = 100.0,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0.0f, 7.0f),
		};
		rows.AddChild(_captureAmmoRechargeBar);
		UpdateCaptureAmmoHud();
	}

	private void UpdateCaptureAmmoHud()
	{
		if (_captureAmmoCountLabel == null || _captureAmmoRechargeBar == null)
		{
			return;
		}

		int capacity = Mathf.Max(CaptureNetCapacity, 1);
		float rechargeSeconds = Mathf.Max(CaptureNetRechargeSeconds, 0.05f);
		_captureAmmoCountLabel.Text = $"{_captureNetCharges} / {capacity}";

		if (_captureNetCharges <= 0)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.34f, 0.28f));
		}
		else if (_captureNetCharges <= 2)
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.82f, 0.38f));
		}
		else
		{
			_captureAmmoCountLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f));
		}

		bool full = _captureNetCharges >= capacity;
		float rechargeProgress = full
			? 100.0f
			: Mathf.Clamp((1.0f - _captureNetRechargeRemaining / rechargeSeconds) * 100.0f, 0.0f, 100.0f);
		_captureAmmoRechargeBar.Value = rechargeProgress;
		_captureAmmoCaptionLabel.Text = full
			? LocaleText.T("hud.capture_net_key")
			: LocaleText.F("hud.recharge_seconds", Mathf.CeilToInt(_captureNetRechargeRemaining));
	}

	private static Label MakeHudLabel(string text, int fontSize, Color color)
	{
		var label = new Label
		{
			Text = text,
			VerticalAlignment = VerticalAlignment.Center,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		return label;
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

	private void ConfigureThirdPersonCamera()
	{
		_cameraYaw = 0.0f;
		_camera.TopLevel = true;
		_camera.Fov = 58.0f;
		_camera.Near = 0.05f;
		_camera.HOffset = 0.0f;
		_camera.VOffset = 0.0f;
		_cameraPivot.Position = Vector3.Zero;
		UpdateThirdPersonCamera();
	}

	private void UpdateThirdPersonCamera()
	{
		Vector3 backward = GetCameraPlanarBackward();
		float distance = Mathf.Max(ThirdPersonDistance, 1.0f);
		float cameraHeight = Mathf.Max(ThirdPersonCameraHeight, 2.8f);
		float lookHeight = Mathf.Clamp(ThirdPersonLookHeight, 1.75f, cameraHeight - 0.35f);
		Vector3 cameraPosition = GlobalPosition + backward * distance + Vector3.Up * cameraHeight;
		cameraPosition = ClampCameraInsideMap(cameraPosition);
		Vector3 lookTarget = GlobalPosition + Vector3.Up * lookHeight;

		_camera.LookAtFromPosition(cameraPosition, lookTarget, Vector3.Up);
	}

	private Vector3 ClampCameraInsideMap(Vector3 position)
	{
		float halfExtent = Mathf.Max(CameraWorldHalfExtent, 8.0f);
		position.X = Mathf.Clamp(position.X, -halfExtent, halfExtent);
		position.Z = Mathf.Clamp(position.Z, -halfExtent, halfExtent);
		position.Y = Mathf.Max(position.Y, GlobalPosition.Y + 2.8f);
		return position;
	}

	private Vector3 GetCameraPlanarBackward()
	{
		return new Vector3(Mathf.Sin(_cameraYaw), 0.0f, Mathf.Cos(_cameraYaw)).Normalized();
	}

	private Vector3 GetCameraPlanarForward()
	{
		return -GetCameraPlanarBackward();
	}

	private Vector3 GetCameraPlanarRight()
	{
		Vector3 forward = GetCameraPlanarForward();
		return new Vector3(-forward.Z, 0.0f, forward.X).Normalized();
	}

	private Vector3 GetCameraAimDirection()
	{
		if (_camera == null || !IsInstanceValid(_camera))
		{
			return GetCameraPlanarForward();
		}

		return -_camera.GlobalTransform.Basis.Z.Normalized();
	}

	private Vector3 GetCaptureThrowDirection()
	{
		return GetCameraPlanarForward();
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
		rotation.Y = Mathf.LerpAngle(rotation.Y, targetAngle, Mathf.Min(step * 12.0f, 1.0f));
		Rotation = rotation;
	}

	private void CreatePlayerVisual()
	{
		var matCoat = MakeMaterial(new Color(0.18f, 0.36f, 0.62f));
		var matTrim = MakeMaterial(new Color(0.95f, 0.72f, 0.26f));
		var matSkin = MakeMaterial(new Color(0.86f, 0.62f, 0.44f));
		var matLeather = MakeMaterial(new Color(0.22f, 0.14f, 0.09f));
		var matDark = MakeMaterial(new Color(0.06f, 0.07f, 0.08f));
		var matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.36f);

		AddVisualMesh("PlayerBody", new CapsuleMesh { Radius = 0.30f, Height = 0.95f }, new Vector3(0.0f, 0.98f, 0.0f), Vector3.Zero, new Vector3(0.95f, 1.0f, 0.78f), matCoat);
		AddVisualMesh("PlayerChestTrim", new BoxMesh { Size = new Vector3(0.58f, 0.08f, 0.055f) }, new Vector3(0.0f, 1.22f, -0.24f), Vector3.Zero, Vector3.One, matTrim);
		AddVisualMesh("PlayerBelt", new BoxMesh { Size = new Vector3(0.66f, 0.10f, 0.12f) }, new Vector3(0.0f, 0.74f, -0.02f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerHead", new SphereMesh { Radius = 0.27f, Height = 0.54f }, new Vector3(0.0f, 1.66f, 0.0f), Vector3.Zero, new Vector3(0.94f, 1.05f, 0.92f), matSkin);
		AddVisualMesh("PlayerHair", new SphereMesh { Radius = 0.29f, Height = 0.34f }, new Vector3(0.0f, 1.82f, 0.03f), Vector3.Zero, new Vector3(1.02f, 0.48f, 0.92f), matDark);
		AddVisualMesh("PlayerLeftArm", new CapsuleMesh { Radius = 0.075f, Height = 0.78f }, new Vector3(-0.38f, 1.04f, 0.0f), new Vector3(0.0f, 0.0f, -9.0f), Vector3.One, matSkin);
		AddVisualMesh("PlayerRightArm", new CapsuleMesh { Radius = 0.075f, Height = 0.78f }, new Vector3(0.38f, 1.04f, 0.0f), new Vector3(0.0f, 0.0f, 9.0f), Vector3.One, matSkin);
		AddVisualMesh("PlayerLeftLeg", new CapsuleMesh { Radius = 0.095f, Height = 0.72f }, new Vector3(-0.14f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerRightLeg", new CapsuleMesh { Radius = 0.095f, Height = 0.72f }, new Vector3(0.14f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, matLeather);
		AddVisualMesh("PlayerLeftBoot", new BoxMesh { Size = new Vector3(0.20f, 0.12f, 0.32f) }, new Vector3(-0.14f, 0.06f, -0.05f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerRightBoot", new BoxMesh { Size = new Vector3(0.20f, 0.12f, 0.32f) }, new Vector3(0.14f, 0.06f, -0.05f), Vector3.Zero, Vector3.One, matDark);
		AddVisualMesh("PlayerCape", new BoxMesh { Size = new Vector3(0.50f, 0.78f, 0.055f) }, new Vector3(0.0f, 1.04f, 0.38f), new Vector3(-8.0f, 0.0f, 0.0f), Vector3.One, matTrim);
		AddVisualMesh("PlayerTool", new BoxMesh { Size = new Vector3(0.075f, 0.74f, 0.045f) }, new Vector3(0.55f, 0.98f, -0.12f), new Vector3(0.0f, 0.0f, -22.0f), Vector3.One, matMetal);
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
		AddChild(meshInstance);
	}

	private static StandardMaterial3D MakeMaterial(Color color, float roughness = 0.82f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};
	}

	private void CreateTargetInfoPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "TargetInfoLayer",
			Layer = 20,
		};

		AddChild(layer);
		_targetInfoPanel = new TargetInfoPanel();
		layer.AddChild(_targetInfoPanel);
	}

	private void CreateMinimapPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "MinimapLayer",
			Layer = 22,
		};

		AddChild(layer);
		_minimapPanel = new MinimapPanel();
		layer.AddChild(_minimapPanel);
		_minimapPanel.Bind(this);
	}

	private void CreatePartyPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "PartyLayer",
			Layer = 30,
		};

		AddChild(layer);
		_partyPanel = new PartyPanel();
		layer.AddChild(_partyPanel);
		_partyPanel.Bind(this);
	}

	private void CreateInventoryPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "InventoryLayer",
			Layer = 34,
		};

		AddChild(layer);
		_inventoryPanel = new InventoryPanel();
		layer.AddChild(_inventoryPanel);
		_inventoryPanel.Bind(this);
		_inventoryPanel.CloseRequested = () => SetInventoryPanelVisible(false);
	}

	private void CreateSettingsPanel()
	{
		var layer = new CanvasLayer
		{
			Name = "SettingsLayer",
			Layer = 45,
		};

		AddChild(layer);
		_settingsPanel = new SettingsPanel();
		layer.AddChild(_settingsPanel);
		_settingsPanel.Bind(this);
		_settingsPanel.CloseRequested = () => SetSettingsPanelVisible(false);
	}

	private void SetPartyPanelVisible(bool visible)
	{
		_partyPanel.SetPanelVisible(visible);
		if (visible)
		{
			_settingsPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
		}

		UpdateMouseModeForPanels();
	}

	private void SetInventoryPanelVisible(bool visible)
	{
		_inventoryPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_settingsPanel.SetPanelVisible(false);
		}

		UpdateMouseModeForPanels();
	}

	private void SetSettingsPanelVisible(bool visible)
	{
		_settingsPanel.SetPanelVisible(visible);
		if (visible)
		{
			_partyPanel.SetPanelVisible(false);
			_inventoryPanel.SetPanelVisible(false);
		}

		UpdateMouseModeForPanels();
	}

	private void UpdateMouseModeForPanels()
	{
		Input.MouseMode = _partyPanel.Visible || _inventoryPanel.Visible || _settingsPanel.Visible
			? Input.MouseModeEnum.Visible
			: Input.MouseModeEnum.Captured;
	}

	private void UpdateTargetInfoPanel()
	{
		PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;
		Vector3 origin = _camera.GlobalPosition;
		Vector3 end = origin + GetCameraAimDirection() * TargetInfoRange;
		var query = PhysicsRayQueryParameters3D.Create(origin, end);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

		Godot.Collections.Dictionary result = spaceState.IntersectRay(query);
		if (result.TryGetValue("collider", out Variant colliderVariant) && colliderVariant.AsGodotObject() is SimpleActor actor)
		{
			_targetInfoPanel.ShowActor(actor);
			return;
		}

		_targetInfoPanel.HideActor();
	}

	private static void AddKeyAction(StringName actionName, Key physicalKeycode)
	{
		if (!InputMap.HasAction(actionName))
		{
			InputMap.AddAction(actionName);
		}

		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(actionName))
		{
			if (inputEvent is InputEventKey keyEvent && keyEvent.PhysicalKeycode == physicalKeycode)
			{
				return;
			}
		}

		var keyEventToAdd = new InputEventKey { PhysicalKeycode = physicalKeycode };
		InputMap.ActionAddEvent(actionName, keyEventToAdd);
	}
}
