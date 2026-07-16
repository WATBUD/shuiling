using Godot;
using System.Collections.Generic;

public partial class CombatEffect : Node3D
{
	private const float DamageTextRenderScale = 1.0f;
	[Export] public string Text { get; set; } = string.Empty;
	[Export] public Color EffectColor { get; set; } = new(1.0f, 0.55f, 0.18f, 0.9f);
	[Export] public float Lifetime { get; set; } = 0.52f;
	[Export] public float Radius { get; set; } = 0.55f;

	private readonly List<StandardMaterial3D> _materials = new();
	private float _age;
	private float _textDrift;
	private Node3D? _impactRoot;
	private Label3D? _label;
	private Label3D? _labelShadow;
	private Label3D? _labelHighlight;

	public override void _Ready()
	{
		_textDrift = (float)GD.RandRange(-0.18, 0.18);
		BuildVisuals();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		float t = Mathf.Clamp(_age / Mathf.Max(Lifetime, 0.01f), 0.0f, 1.0f);
		float alpha = t < 0.68f ? 1.0f : 1.0f - (t - 0.68f) / 0.32f;

		if (_impactRoot != null)
		{
			_impactRoot.Scale = Vector3.One * Mathf.Lerp(0.72f, 1.18f, t);
		}

		foreach (StandardMaterial3D material in _materials)
		{
			material.AlbedoColor = new Color(EffectColor.R, EffectColor.G, EffectColor.B, EffectColor.A * alpha);
		}

		if (_label != null)
		{
			float popScale = t < 0.14f
				? Mathf.Lerp(0.55f, 1.12f, t / 0.14f)
				: t < 0.30f
					? Mathf.Lerp(1.12f, 1.0f, (t - 0.14f) / 0.16f)
					: Mathf.Lerp(1.0f, 0.92f, (t - 0.30f) / 0.70f);
			float rise = Mathf.Sin(t * Mathf.Pi) * 0.34f + t * 0.82f;
			Vector3 textPosition = new(_textDrift * t, 0.78f + rise, 0.0f);
			Color mainColor = GetDamageTextColor();
			_label.Position = textPosition;
			_label.Scale = Vector3.One * (popScale * DamageTextRenderScale);
			_label.Modulate = new Color(mainColor.R, mainColor.G, mainColor.B, alpha);
			UpdateLayeredLabel(_labelShadow, textPosition + new Vector3(0.035f, -0.035f, 0.01f), popScale * DamageTextRenderScale, new Color(0.035f, 0.02f, 0.03f, alpha * 0.92f));
			UpdateLayeredLabel(_labelHighlight, textPosition + new Vector3(-0.018f, 0.025f, -0.01f), popScale * 0.985f * DamageTextRenderScale, new Color(1.0f, 1.0f, 0.88f, alpha * 0.34f));
		}

		if (_age >= Lifetime)
		{
			QueueFree();
		}
	}

	private void BuildVisuals()
	{
		_impactRoot = new Node3D { Name = "ImpactVisuals" };
		AddChild(_impactRoot);

		AddFxMesh(
			"ImpactCore",
			new SphereMesh { Radius = Radius * 0.15f, Height = Radius * 0.30f },
			new Vector3(0.0f, Radius * 0.18f, 0.0f),
			Vector3.Zero,
			new Vector3(1.0f, 0.72f, 1.0f),
			EffectColor
		);
		AddImpactParticles();

		if (!string.IsNullOrEmpty(Text))
		{
			_labelShadow = CreateDamageLabel(12, new Color(0.02f, 0.01f, 0.02f, 0.96f));
			_label = CreateDamageLabel(8, new Color(0.10f, 0.035f, 0.025f, 0.98f));
			_labelHighlight = CreateDamageLabel(0, Colors.Transparent);
			AddChild(_labelShadow);
			AddChild(_label);
			AddChild(_labelHighlight);
		}
	}

	private void AddImpactParticles()
	{
		var particleMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.92f),
			EmissionEnabled = true,
			Emission = EffectColor,
			EmissionEnergyMultiplier = 2.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		var sparkMesh = new QuadMesh
		{
			Size = new Vector2(Mathf.Max(Radius * 0.075f, 0.028f), Mathf.Max(Radius * 0.32f, 0.12f)),
			Material = particleMaterial,
		};
		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = Radius * 0.14f,
			Direction = Vector3.Up,
			Spread = 180.0f,
			InitialVelocityMin = Mathf.Max(Radius * 3.2f, 1.4f),
			InitialVelocityMax = Mathf.Max(Radius * 5.8f, 2.6f),
			Gravity = new Vector3(0.0f, -4.2f, 0.0f),
			ScaleMin = 0.62f,
			ScaleMax = 1.08f,
			Color = new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.92f),
		};
		var sparks = new GpuParticles3D
		{
			Name = "ImpactSparks",
			Amount = 11,
			Lifetime = Mathf.Clamp(Lifetime * 0.62f, 0.16f, 0.34f),
			OneShot = true,
			Explosiveness = 1.0f,
			Randomness = 0.48f,
			ProcessMaterial = processMaterial,
			DrawPass1 = sparkMesh,
			Emitting = true,
		};
		(_impactRoot ?? this).AddChild(sparks);
	}

	private Label3D CreateDamageLabel(int outlineSize, Color outlineColor)
	{
		var label = new Label3D
		{
			Text = Text,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = true,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
			FontSize = 96,
			PixelSize = 0.005f,
			OutlineSize = outlineSize,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Width = 240.0f,
			Position = new Vector3(0.0f, 0.78f, 0.0f),
		};
		label.OutlineModulate = outlineColor;
		return label;
	}

	private Color GetDamageTextColor()
	{
		float maxChannel = Mathf.Max(EffectColor.R, Mathf.Max(EffectColor.G, EffectColor.B));
		float lift = maxChannel < 0.82f ? 0.18f : 0.08f;
		return new Color(
			Mathf.Clamp(EffectColor.R + lift, 0.0f, 1.0f),
			Mathf.Clamp(EffectColor.G + lift, 0.0f, 1.0f),
			Mathf.Clamp(EffectColor.B + lift * 0.55f, 0.0f, 1.0f),
			1.0f
		);
	}

	private static void UpdateLayeredLabel(Label3D? label, Vector3 position, float scale, Color color)
	{
		if (label == null)
		{
			return;
		}

		label.Position = position;
		label.Scale = Vector3.One * scale;
		label.Modulate = color;
	}

	private void AddFxMesh(string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Color color)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = 1.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
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
		(_impactRoot ?? this).AddChild(meshInstance);
	}
}
