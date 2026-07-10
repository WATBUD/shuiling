using Godot;
using System.Collections.Generic;

public partial class MovementDustEffect : Node3D
{
	[Export] public Color DustColor { get; set; } = new(0.70f, 0.63f, 0.48f, 0.72f);
	[Export] public float Lifetime { get; set; } = 0.42f;
	[Export] public float Radius { get; set; } = 0.18f;
	[Export] public float DirectionYaw { get; set; }
	[Export] public bool IsFastStep { get; set; }

	private readonly List<StandardMaterial3D> _materials = new();
	private float _age;

	public override void _Ready()
	{
		RotationDegrees = new Vector3(0.0f, DirectionYaw, 0.0f);
		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		float t = Mathf.Clamp(_age / Mathf.Max(Lifetime, 0.01f), 0.0f, 1.0f);
		float alpha = 1.0f - t;

		Scale = new Vector3(1.0f + t * 1.25f, 1.0f + t * 0.25f, 1.0f + t * 1.55f);
		Position += new Vector3(0.0f, step * 0.18f, step * 0.42f);

		foreach (StandardMaterial3D material in _materials)
		{
			material.AlbedoColor = new Color(DustColor.R, DustColor.G, DustColor.B, DustColor.A * alpha);
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}

	private void BuildVisuals()
	{
		int puffCount = IsFastStep ? 4 : 3;
		for (int index = 0; index < puffCount; index++)
		{
			float side = index % 2 == 0 ? -1.0f : 1.0f;
			float forward = -0.08f - index * 0.07f;
			float size = Radius * (1.0f - index * 0.12f);
			AddDustMesh(
				$"DustPuff{index}",
				new SphereMesh { Radius = size, Height = size * 0.75f },
				new Vector3(side * Radius * (0.55f + index * 0.18f), size * 0.28f, forward),
				new Vector3(1.25f, 0.32f, 0.85f),
				new Color(DustColor.R, DustColor.G, DustColor.B, DustColor.A * (0.85f - index * 0.12f))
			);
		}

		if (IsFastStep)
		{
			AddDustMesh(
				"SpeedStreak",
				new BoxMesh { Size = new Vector3(Radius * 1.9f, 0.035f, Radius * 3.2f) },
				new Vector3(0.0f, Radius * 0.12f, -Radius * 0.9f),
				new Vector3(1.0f, 1.0f, 1.0f),
				new Color(1.0f, 0.94f, 0.68f, DustColor.A * 0.45f)
			);
		}
	}

	private void AddDustMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 scale, Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.95f,
		};
		_materials.Add(material);

		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		AddChild(meshInstance);
	}
}
