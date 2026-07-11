using Godot;
using System.Collections.Generic;

public partial class CombatEffect : Node3D
{
	[Export] public string Text { get; set; } = string.Empty;
	[Export] public Color EffectColor { get; set; } = new(1.0f, 0.55f, 0.18f, 0.9f);
	[Export] public float Lifetime { get; set; } = 0.52f;
	[Export] public float Radius { get; set; } = 0.55f;

	private readonly List<StandardMaterial3D> _materials = new();
	private float _age;
	private Label3D? _label;

	public override void _Ready()
	{
		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		float t = Mathf.Clamp(_age / Mathf.Max(Lifetime, 0.01f), 0.0f, 1.0f);
		float alpha = 1.0f - t;

		Scale = Vector3.One * (0.65f + t * 1.7f);
		Position += Vector3.Up * step * 1.35f;
		RotationDegrees += new Vector3(0.0f, 220.0f * step, 0.0f);

		foreach (StandardMaterial3D material in _materials)
		{
			material.AlbedoColor = new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * alpha);
		}

		if (_label != null)
		{
			_label.Position = new Vector3(0.0f, 0.72f + t * 0.45f, 0.0f);
			_label.Modulate = new Color(1.0f, 0.96f, 0.72f, alpha);
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}

	private void BuildVisuals()
	{
		AddFxMesh(
			"ImpactCore",
			new SphereMesh { Radius = Radius * 0.23f, Height = Radius * 0.46f },
			new Vector3(0.0f, Radius * 0.35f, 0.0f),
			Vector3.Zero,
			new Vector3(1.0f, 0.55f, 1.0f),
			EffectColor
		);

		AddFxMesh(
			"ImpactRing",
			new CylinderMesh
			{
				TopRadius = Radius,
				BottomRadius = Radius,
				Height = 0.035f,
				RadialSegments = 36,
			},
			new Vector3(0.0f, 0.05f, 0.0f),
			Vector3.Zero,
			Vector3.One,
			new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * 0.55f)
		);

		AddFxMesh(
			"Slash",
			new BoxMesh { Size = new Vector3(Radius * 1.75f, 0.055f, 0.16f) },
			new Vector3(0.0f, Radius * 0.48f, 0.0f),
			new Vector3(18.0f, 34.0f, 18.0f),
			Vector3.One,
			new Color(1.0f, 0.92f, 0.58f, 0.82f)
		);

		if (!string.IsNullOrEmpty(Text))
		{
			_label = new Label3D
			{
				Text = Text,
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FixedSize = true,
				FontSize = 32,
				PixelSize = 0.009f,
				OutlineSize = 7,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				Width = 140.0f,
				Position = new Vector3(0.0f, 0.72f, 0.0f),
			};
			_label.OutlineModulate = new Color(0.05f, 0.025f, 0.015f, 0.92f);
			AddChild(_label);
		}
	}

	private void AddFxMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.22f,
		};
		_materials.Add(material);

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
}
