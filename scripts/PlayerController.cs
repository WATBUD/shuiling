using Godot;
using System.Collections.Generic;

public partial class PlayerController : CharacterBody3D
{
	[Export] public float WalkSpeed { get; set; } = 6.5f;
	[Export] public float SprintSpeed { get; set; } = 10.0f;
	[Export] public float JumpVelocity { get; set; } = 5.2f;
	[Export] public float MouseSensitivity { get; set; } = 0.0022f;
	[Export] public float Acceleration { get; set; } = 18.0f;
	[Export] public float CaptureCooldown { get; set; } = 0.55f;
	[Export] public float TargetInfoRange { get; set; } = 30.0f;

	private readonly List<SimpleActor> _capturedActors = new();
	private float _pitch;
	private float _gravity;
	private float _captureCooldownRemaining;
	private Node3D _cameraPivot = null!;
	private Camera3D _camera = null!;
	private RayCast3D _targetInfoRay = null!;
	private TargetInfoPanel _targetInfoPanel = null!;

	public override void _Ready()
	{
		_gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
		_cameraPivot = GetNode<Node3D>("CameraPivot");
		_camera = GetNode<Camera3D>("CameraPivot/Camera3D");
		CreateTargetInfoRay();
		CreateTargetInfoPanel();

		AddToGroup("player");
		EnsureInputActions();
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public override void _Process(double delta)
	{
		UpdateTargetInfoPanel();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-mouseMotion.Relative.X * MouseSensitivity);
			_pitch = Mathf.Clamp(
				_pitch - mouseMotion.Relative.Y * MouseSensitivity,
				Mathf.DegToRad(-82.0f),
				Mathf.DegToRad(82.0f)
			);

			Vector3 pivotRotation = _cameraPivot.Rotation;
			pivotRotation.X = _pitch;
			_cameraPivot.Rotation = pivotRotation;
		}

		if (@event is InputEventMouseButton { Pressed: true })
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		if (@event.IsActionPressed("ui_cancel"))
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		if (@event.IsActionPressed("capture_net"))
		{
			ThrowCaptureNet();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
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
		Vector3 forward = -GlobalTransform.Basis.Z;
		Vector3 right = GlobalTransform.Basis.X;
		Vector3 direction = (right * inputDirection.X + forward * -inputDirection.Y).Normalized();
		float targetSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;

		velocity.X = Mathf.MoveToward(velocity.X, direction.X * targetSpeed, Acceleration * step);
		velocity.Z = Mathf.MoveToward(velocity.Z, direction.Z * targetSpeed, Acceleration * step);

		Velocity = velocity;
		MoveAndSlide();
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
	}

	private void ThrowCaptureNet()
	{
		if (_captureCooldownRemaining > 0.0f)
		{
			return;
		}

		_captureCooldownRemaining = CaptureCooldown;
		Vector3 direction = -_camera.GlobalTransform.Basis.Z.Normalized();
		Vector3 spawnPosition = _camera.GlobalPosition + direction * 1.1f;
		var net = new CaptureNet
		{
			OwnerPlayer = this,
			Direction = direction,
		};

		Node projectileParent = GetTree().CurrentScene ?? GetParent();
		projectileParent.AddChild(net);
		net.GlobalPosition = spawnPosition;
		net.AlignToDirection();
	}

	public bool CaptureActor(SimpleActor actor)
	{
		if (!actor.CanBeCaptured || _capturedActors.Contains(actor))
		{
			return false;
		}

		int followSlot = _capturedActors.Count;
		_capturedActors.Add(actor);
		actor.Capture(this, followSlot);
		return true;
	}

	private void CreateTargetInfoRay()
	{
		_targetInfoRay = new RayCast3D
		{
			Name = "TargetInfoRay",
			Enabled = true,
			ExcludeParent = true,
			CollideWithAreas = false,
			CollideWithBodies = true,
			TargetPosition = new Vector3(0.0f, 0.0f, -TargetInfoRange),
		};
		_camera.AddChild(_targetInfoRay);
		_targetInfoRay.AddException(this);
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

	private void UpdateTargetInfoPanel()
	{
		_targetInfoRay.ForceRaycastUpdate();
		if (_targetInfoRay.IsColliding() && _targetInfoRay.GetCollider() is SimpleActor actor)
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
