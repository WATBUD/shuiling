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
		if (_label != null && IsInstanceValid(_label))
		{
			_label.Text = $"Lv{level} {displayName}";
		}
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
