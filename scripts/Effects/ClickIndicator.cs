using Godot;
using System.Collections.Generic;

// RTS-style ground click feedback for God View (參考世界帝國 / Age of Empires):
// an expanding, fading ring that marks where the player clicked. Fire-and-forget
// — spawn it at the ground point and it animates then frees itself.
public partial class ClickIndicator : Node3D
{
	private const float Lifetime = 0.55f;

	// Set before adding to the tree. Green = ground move point, red = hostile.
	public Color RingColor { get; set; } = new(0.40f, 1.0f, 0.60f, 0.92f);

	private readonly record struct Ring(MeshInstance3D Mesh, StandardMaterial3D Material, float StartScale, float EndScale);

	private readonly List<Ring> _rings = new();
	private float _age;

	public override void _Ready()
	{
		// Two concentric rings expanding at different rates read as a ripple.
		AddRing(0.45f, 1.55f, 0.42f, 0.50f, 0.05f);
		AddRing(0.30f, 1.05f, 0.30f, 0.36f, 0.06f);
	}

	private void AddRing(float startScale, float endScale, float innerRadius, float outerRadius, float yOffset)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = RingColor,
			EmissionEnabled = true,
			Emission = RingColor,
			EmissionEnergyMultiplier = 1.6f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			// Always draw over terrain/props so the ripple stays visible on top
			// instead of being occluded by the ground it sits on.
			NoDepthTest = true,
			RenderPriority = 8,
		};

		// TorusMesh lies flat in the XZ plane (axis = Y), so it sits on the
		// ground without any rotation.
		var mesh = new MeshInstance3D
		{
			Name = "ClickRing",
			Mesh = new TorusMesh { InnerRadius = innerRadius, OuterRadius = outerRadius, RingSegments = 6, Rings = 32 },
			Position = new Vector3(0.0f, yOffset, 0.0f),
			Scale = Vector3.One * startScale,
		};
		mesh.MaterialOverride = material;
		AddChild(mesh);
		_rings.Add(new Ring(mesh, material, startScale, endScale));
	}

	public override void _Process(double delta)
	{
		_age += (float)delta;
		float t = Mathf.Clamp(_age / Lifetime, 0.0f, 1.0f);
		// Ease-out expansion + linear fade.
		float expand = 1.0f - (1.0f - t) * (1.0f - t);
		float alpha = 1.0f - t;

		foreach (Ring ring in _rings)
		{
			float scale = Mathf.Lerp(ring.StartScale, ring.EndScale, expand);
			ring.Mesh.Scale = new Vector3(scale, 1.0f, scale);
			ring.Material.AlbedoColor = new Color(RingColor.R, RingColor.G, RingColor.B, RingColor.A * alpha);
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}
}
