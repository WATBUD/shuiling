using Godot;

public partial class CaveCreatureAnimator : Node
{
	public Node3D? CreatureVisual { get; set; }
	public bool IsBat { get; set; }
	private Vector3 _basePosition;
	private float _phase;

	public override void _Ready()
	{
		_phase = (GetInstanceId() % 97) * 0.071f;
		if (CreatureVisual != null)
		{
			_basePosition = CreatureVisual.Position;
		}
	}

	public override void _Process(double delta)
	{
		if (CreatureVisual == null || !IsInstanceValid(CreatureVisual))
		{
			return;
		}
		_phase += (float)delta * (IsBat ? 6.5f : 3.2f);
		CreatureVisual.Position = _basePosition + Vector3.Up * (IsBat ? Mathf.Sin(_phase) * 0.20f : Mathf.Sin(_phase) * 0.035f);
		if (IsBat)
		{
			Node3D? leftWing = CreatureVisual.GetNodeOrNull<Node3D>("LeftWing");
			Node3D? rightWing = CreatureVisual.GetNodeOrNull<Node3D>("RightWing");
			float flap = Mathf.Sin(_phase * 1.65f) * 24.0f;
			if (leftWing != null) leftWing.RotationDegrees = new Vector3(4.0f, -8.0f, 10.0f + flap);
			if (rightWing != null) rightWing.RotationDegrees = new Vector3(-4.0f, 8.0f, -10.0f - flap);
		}
	}
}
