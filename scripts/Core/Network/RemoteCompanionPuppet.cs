using Godot;

// Visual stand-in for another player's deployed companion. Pure display node:
// its model, name/level and transform stream in from the owner over the network.
public partial class RemoteCompanionPuppet : Node3D
{
	private Vector3 _targetPosition;
	private float _targetYaw;
	private bool _hasState;
	private string _modelPath = string.Empty;
	private Label3D _label = null!;
	private string _baseInfo = string.Empty;
	private float _healthRatio = 1.0f;
	private bool _hasHealth;

	public override void _Ready()
	{
		_label = new Label3D
		{
			Name = "CompanionLabel",
			Position = new Vector3(0.0f, 1.7f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 34,
			OutlineSize = 8,
			Modulate = new Color(0.7f, 1.0f, 0.8f),
			NoDepthTest = true,
		};
		AddChild(_label);
		Visible = false;
	}

	// (Re)build the visual when the model path changes (e.g. after evolution).
	public void SetModel(string modelPath)
	{
		if (modelPath == _modelPath && GetNodeOrNull<Node3D>("Model") != null)
		{
			return;
		}

		_modelPath = modelPath;
		Node3D? existing = GetNodeOrNull<Node3D>("Model");
		if (existing != null)
		{
			existing.Name = "ModelDiscarded";
			RemoveChild(existing);
			existing.QueueFree();
		}

		if (!string.IsNullOrEmpty(modelPath)
			&& ExternalModelLibrary.TryAddModel(this, modelPath, "Model", Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f), Vector3.One))
		{
			return;
		}

		// Fallback capsule so the companion is always visible.
		var body = new MeshInstance3D
		{
			Name = "Model",
			Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.1f },
			Position = new Vector3(0.0f, 0.7f, 0.0f),
		};
		body.SetSurfaceOverrideMaterial(0, new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.9f, 0.7f) });
		AddChild(body);
	}

	public void SetInfo(string displayName, int level)
	{
		_baseInfo = $"Lv{level} {displayName}";
		RefreshLabel();
	}

	// Mirror the owner's companion health so peers see it get hurt: a hit flash on
	// any drop, a HP% suffix, and a label tint that reddens as health falls.
	public void SetHealth(float ratio)
	{
		ratio = Mathf.Clamp(ratio, 0.0f, 1.0f);
		if (_hasHealth && ratio < _healthRatio - 0.001f)
		{
			SpawnHitFlash();
		}

		_healthRatio = ratio;
		_hasHealth = true;
		RefreshLabel();
	}

	private void RefreshLabel()
	{
		if (_label == null || !IsInstanceValid(_label))
		{
			return;
		}

		_label.Text = _hasHealth ? $"{_baseInfo}  {Mathf.RoundToInt(_healthRatio * 100.0f)}%" : _baseInfo;
		// Green when healthy, shifting to red as it takes damage.
		_label.Modulate = new Color(0.7f, 1.0f, 0.8f).Lerp(new Color(1.0f, 0.4f, 0.35f), 1.0f - _healthRatio);
	}

	private void SpawnHitFlash()
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.32f, 0.28f, 0.5f),
			Emission = new Color(1.0f, 0.35f, 0.3f),
			EmissionEnabled = true,
			EmissionEnergyMultiplier = 2.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
		var flash = new MeshInstance3D
		{
			Name = "CompanionHitFlash",
			Mesh = new SphereMesh { Radius = 0.6f, Height = 1.2f },
			Position = new Vector3(0.0f, 0.7f, 0.0f),
			MaterialOverride = material,
		};
		AddChild(flash);
		SceneTreeTimer timer = GetTree().CreateTimer(0.15);
		timer.Timeout += () =>
		{
			if (IsInstanceValid(flash))
			{
				flash.QueueFree();
			}
		};
	}

	public void ApplyNetworkState(Vector3 position, float yaw)
	{
		_targetPosition = position;
		_targetYaw = yaw;
		if (!_hasState)
		{
			_hasState = true;
			GlobalPosition = position;
			Rotation = new Vector3(0.0f, yaw, 0.0f);
		}
	}

	public override void _Process(double delta)
	{
		if (!_hasState)
		{
			return;
		}

		float weight = Mathf.Min((float)delta * 12.0f, 1.0f);
		if (GlobalPosition.DistanceTo(_targetPosition) > 12.0f)
		{
			GlobalPosition = _targetPosition;
		}
		else
		{
			GlobalPosition = GlobalPosition.Lerp(_targetPosition, weight);
		}

		Rotation = new Vector3(0.0f, Mathf.LerpAngle(Rotation.Y, _targetYaw, weight), 0.0f);
	}
}
