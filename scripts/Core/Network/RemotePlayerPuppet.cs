using Godot;

// Visual stand-in for another connected player. Pure display node: position,
// yaw and map id stream in from the network (no physics, no collision).
public partial class RemotePlayerPuppet : Node3D
{
	public string MapId { get; private set; } = "city";
	public int Tier { get; private set; } = 1;

	private Vector3 _targetPosition;
	private float _targetYaw;
	private bool _hasState;
	private Label3D _nameLabel = null!;

	public override void _Ready()
	{
		BuildVisual();

		_nameLabel = new Label3D
		{
			Name = "PlayerNameLabel",
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 52,
			OutlineSize = 10,
			Modulate = new Color(0.55f, 0.9f, 1.0f),
			NoDepthTest = true,
		};
		AddChild(_nameLabel);
		Visible = false;
	}

	private void BuildVisual()
	{
		if (ExternalModelLibrary.TryAddPlayerModel(this) != null)
		{
			return;
		}

		// Fallback: simple capsule figure so remote players are always visible.
		var bodyMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.7f, 1.0f) };
		var body = new MeshInstance3D
		{
			Name = "PuppetBody",
			Mesh = new CapsuleMesh { Radius = 0.32f, Height = 1.5f },
			Position = new Vector3(0.0f, 0.95f, 0.0f),
		};
		body.SetSurfaceOverrideMaterial(0, bodyMaterial);
		AddChild(body);

		var headMaterial = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.75f, 0.6f) };
		var head = new MeshInstance3D
		{
			Name = "PuppetHead",
			Mesh = new SphereMesh { Radius = 0.24f, Height = 0.48f },
			Position = new Vector3(0.0f, 1.92f, 0.0f),
		};
		head.SetSurfaceOverrideMaterial(0, headMaterial);
		AddChild(head);
	}

	public void SetPlayerName(string playerName)
	{
		if (_nameLabel != null && IsInstanceValid(_nameLabel))
		{
			_nameLabel.Text = playerName;
		}
	}

	public void ApplyNetworkState(Vector3 position, float yaw, string mapId, int tier)
	{
		MapId = mapId;
		Tier = tier;
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
