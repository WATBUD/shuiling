using Godot;
using System.Collections.Generic;

public partial class AttackProjectile : Node3D
{
	[Export] public Vector3 StartPosition { get; set; }
	[Export] public Vector3 EndPosition { get; set; }
	[Export] public Color EffectColor { get; set; } = new(1.0f, 0.5f, 0.18f, 0.92f);
	[Export] public float Lifetime { get; set; } = 0.28f;
	[Export] public float Radius { get; set; } = 0.22f;
	[Export] public bool IsMelee { get; set; }
	[Export] public bool IsHealing { get; set; }
	[Export] public bool IsArrow { get; set; }

	private readonly List<StandardMaterial3D> _materials = new();
	private float _age;
	private Vector3 _direction = Vector3.Forward;

	public override void _Ready()
	{
		GlobalPosition = StartPosition;
		Vector3 travel = EndPosition - StartPosition;
		if (travel.LengthSquared() > 0.001f)
		{
			_direction = travel.Normalized();
			LookAt(StartPosition + _direction, Vector3.Up);
		}

		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		float t = Mathf.Clamp(_age / Mathf.Max(Lifetime, 0.01f), 0.0f, 1.0f);
		float eased = 1.0f - Mathf.Pow(1.0f - t, 2.0f);
		float arcHeight = IsMelee ? 0.16f : 0.42f;

		GlobalPosition = StartPosition.Lerp(EndPosition, eased) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * arcHeight;
		RotationDegrees += IsMelee
			? new Vector3(0.0f, 760.0f * step, 0.0f)
			: new Vector3(360.0f * step, 0.0f, 520.0f * step);

		float alpha = 1.0f - Mathf.Clamp((t - 0.68f) / 0.32f, 0.0f, 1.0f);
		foreach (StandardMaterial3D material in _materials)
		{
			material.AlbedoColor = new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * alpha);
		}

		if (_age >= Lifetime)
		{
			SpawnArrivalPulse();
			QueueFree();
		}
	}

	private void BuildVisuals()
	{
		if (IsMelee)
		{
			AddFxMesh(
				"SlashBlade",
				new BoxMesh { Size = new Vector3(Radius * 3.1f, 0.055f, Radius * 0.42f) },
				Vector3.Zero,
				new Vector3(0.0f, 0.0f, 24.0f),
				Vector3.One,
				new Color(1.0f, 0.92f, 0.58f, 0.92f)
			);
			return;
		}

		if (IsArrow)
		{
			AddFxMesh(
				"ArrowShaft",
				new CapsuleMesh { Radius = Radius * 0.13f, Height = Radius * 4.8f },
				Vector3.Zero,
				new Vector3(90.0f, 0.0f, 0.0f),
				Vector3.One,
				new Color(0.62f, 0.38f, 0.16f, 0.96f)
			);
			AddFxMesh(
				"ArrowHead",
				new CylinderMesh { TopRadius = 0.0f, BottomRadius = Radius * 0.32f, Height = Radius * 0.72f, RadialSegments = 16 },
				_direction * Radius * 2.35f,
				new Vector3(90.0f, 0.0f, 0.0f),
				Vector3.One,
				new Color(0.82f, 0.86f, 0.86f, 0.98f)
			);
			AddFxMesh(
				"ArrowFletching",
				new BoxMesh { Size = new Vector3(Radius * 0.72f, Radius * 0.11f, Radius * 0.38f) },
				_direction * -Radius * 2.25f,
				new Vector3(0.0f, 0.0f, 24.0f),
				Vector3.One,
				new Color(0.96f, 0.78f, 0.28f, 0.92f)
			);
			return;
		}

		AddFxMesh(
			IsHealing ? "HealOrb" : "AttackOrb",
			new SphereMesh { Radius = Radius, Height = Radius * 2.0f },
			Vector3.Zero,
			Vector3.Zero,
			new Vector3(1.0f, 0.82f, 1.0f),
			EffectColor
		);
		AddFxMesh(
			"Trail",
			new CapsuleMesh { Radius = Radius * 0.24f, Height = Radius * 3.1f },
			_direction * -Radius * 1.55f,
			new Vector3(90.0f, 0.0f, 0.0f),
			Vector3.One,
			new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * 0.52f)
		);
		AddFxMesh(
			"Spark",
			new BoxMesh { Size = new Vector3(Radius * 1.15f, Radius * 0.12f, Radius * 0.12f) },
			_direction * Radius * 0.42f,
			new Vector3(0.0f, 35.0f, 18.0f),
			Vector3.One,
			new Color(1.0f, 0.96f, 0.72f, 0.86f)
		);
	}

	private void SpawnArrivalPulse()
	{
		Node parent = GetTree().CurrentScene ?? GetParent();
		if (parent == null)
		{
			return;
		}

		var effect = new CombatEffect
		{
			Text = string.Empty,
			EffectColor = EffectColor,
			Lifetime = IsMelee ? 0.22f : 0.30f,
			Radius = IsMelee ? Radius * 1.45f : Radius * 1.85f,
		};
		parent.AddChild(effect);
		effect.GlobalPosition = EndPosition;
	}

	private void AddFxMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.18f,
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
