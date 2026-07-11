using Godot;

public partial class MapPortalEffect : Node3D
{
	public Node3D? OuterRing { get; set; }
	public Node3D? InnerRing { get; set; }
	public Node3D? GroundAura { get; set; }
	public Light3D? PortalLight { get; set; }

	private float _time;

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_time += step;

		if (OuterRing != null)
		{
			OuterRing.RotateY(step * 0.75f);
			OuterRing.RotateZ(step * 0.35f);
		}

		if (InnerRing != null)
		{
			InnerRing.RotateY(-step * 1.15f);
			InnerRing.RotateZ(-step * 0.5f);
		}

		float pulse = 1.0f + Mathf.Sin(_time * 2.6f) * 0.08f;
		if (GroundAura != null)
		{
			GroundAura.Scale = new Vector3(pulse, 1.0f, pulse);
		}

		if (PortalLight != null)
		{
			PortalLight.LightEnergy = 1.8f + Mathf.Sin(_time * 3.2f) * 0.35f;
		}
	}
}
