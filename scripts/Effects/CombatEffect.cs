using Godot;
using System.Collections.Generic;

public partial class CombatEffect : Node3D
{
	private static int _activeEffectCount;
	public const float MinimumDamageTextScale = 0.5f;
	public const float MaximumDamageTextScale = 2.0f;
	public static float DamageTextScale { get; private set; } = 1.0f;
	[Export] public string Text { get; set; } = string.Empty;
	[Export] public Color EffectColor { get; set; } = new(1.0f, 0.55f, 0.18f, 0.9f);
	[Export] public float Lifetime { get; set; } = 0.52f;
	[Export] public float Radius { get; set; } = 0.55f;
	// RO-style critical hit: a dark panel with a red frame behind the number,
	// plus a "CRITICAL!" banner riding above it.
	[Export] public bool CritBanner { get; set; }

	private readonly List<StandardMaterial3D> _materials = new();
	private float _age;
	private float _textDrift;
	private Node3D? _impactRoot;
	private Label3D? _label;
	private Label3D? _labelShadow;
	private Label3D? _labelHighlight;
	private Label3D? _critLabel;
	private MeshInstance3D? _critPanel;
	private MeshInstance3D? _critFrame;
	private StandardMaterial3D? _critPanelMaterial;
	private StandardMaterial3D? _critFrameMaterial;
	private bool _registeredAsActive;

	public static void SetDamageTextScale(float scale)
	{
		DamageTextScale = Mathf.Clamp(scale, MinimumDamageTextScale, MaximumDamageTextScale);
	}

	public override void _Ready()
	{
		if (_activeEffectCount >= PerformanceConfig.MaximumVisibleCombatEffects)
		{
			QueueFree();
			return;
		}

		_activeEffectCount++;
		_registeredAsActive = true;
		_textDrift = (float)GD.RandRange(-0.18, 0.18);
		BuildVisuals();
	}

	public override void _ExitTree()
	{
		if (_registeredAsActive)
		{
			_activeEffectCount = Mathf.Max(_activeEffectCount - 1, 0);
			_registeredAsActive = false;
		}
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
			float configuredTextScale = int.TryParse(Text, out _) ? DamageTextScale : 1.0f;
			float popScale = t < 0.14f
				? Mathf.Lerp(0.55f, 1.12f, t / 0.14f)
				: t < 0.30f
					? Mathf.Lerp(1.12f, 1.0f, (t - 0.14f) / 0.16f)
					: Mathf.Lerp(1.0f, 0.92f, (t - 0.30f) / 0.70f);
			float rise = Mathf.Sin(t * Mathf.Pi) * 0.34f + t * 0.82f;
			Vector3 textPosition = new(_textDrift * t, 0.78f + rise, 0.0f);
			Color mainColor = GetDamageTextColor();
			_label.Position = textPosition;
			_label.Scale = Vector3.One * (popScale * configuredTextScale);
			_label.Modulate = new Color(mainColor.R, mainColor.G, mainColor.B, alpha);
			UpdateLayeredLabel(_labelShadow, textPosition + new Vector3(0.035f, -0.035f, 0.01f), popScale * configuredTextScale, new Color(0.035f, 0.02f, 0.03f, alpha * 0.92f));
			UpdateLayeredLabel(_labelHighlight, textPosition + new Vector3(-0.018f, 0.025f, -0.01f), popScale * 0.985f * configuredTextScale, new Color(1.0f, 1.0f, 0.88f, alpha * 0.34f));
		}

		if (CritBanner)
		{
			float bannerPop = t < 0.12f ? Mathf.Lerp(0.4f, 1.14f, t / 0.12f) : Mathf.Lerp(1.14f, 1.0f, Mathf.Min((t - 0.12f) / 0.18f, 1.0f));
			float bannerRise = 0.78f + Mathf.Sin(t * Mathf.Pi) * 0.34f + t * 0.82f;
			var bannerBase = new Vector3(0.0f, bannerRise, 0.0f);
			if (_critFrame != null)
			{
				_critFrame.Position = bannerBase;
				_critFrame.Scale = Vector3.One * bannerPop;
			}
			if (_critPanel != null)
			{
				_critPanel.Position = bannerBase + new Vector3(0.0f, 0.0f, 0.01f);
				_critPanel.Scale = Vector3.One * bannerPop;
			}
			if (_critLabel != null)
			{
				_critLabel.Position = bannerBase + new Vector3(0.0f, 0.64f, 0.02f);
				_critLabel.Scale = Vector3.One * bannerPop;
				_critLabel.Modulate = new Color(1.0f, 0.92f, 0.30f, alpha);
			}
			if (_critFrameMaterial != null)
			{
				_critFrameMaterial.AlbedoColor = new Color(0.95f, 0.12f, 0.12f, 0.92f * alpha);
			}
			if (_critPanelMaterial != null)
			{
				_critPanelMaterial.AlbedoColor = new Color(0.10f, 0.02f, 0.03f, 0.82f * alpha);
			}
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

		AddImpactSprite();
		AddImpactParticles();

		if (CritBanner)
		{
			BuildCritBanner();
		}

		if (!string.IsNullOrEmpty(Text))
		{
			// Two layers (shadow + main) instead of three: the extra highlight
			// pass barely read and tripled per-hit Label3D glyph work.
			_labelShadow = CreateDamageLabel(12, new Color(0.02f, 0.01f, 0.02f, 0.96f));
			_label = CreateDamageLabel(8, new Color(0.10f, 0.035f, 0.025f, 0.98f));
			AddChild(_labelShadow);
			AddChild(_label);
		}
	}

	// RO-style critical: a dark quad framed in red sits behind the damage number,
	// with a bright "CRITICAL!" banner floating above. All layers fade with the
	// effect via _critPanelMaterial / _critFrameMaterial and _critLabel.Modulate.
	private void BuildCritBanner()
	{
		_critFrame = new MeshInstance3D
		{
			Name = "CritFrame",
			Mesh = new QuadMesh { Size = new Vector2(1.72f, 0.94f) },
		};
		_critFrameMaterial = CreateBannerMaterial(new Color(0.95f, 0.12f, 0.12f, 0.92f));
		_critFrame.MaterialOverride = _critFrameMaterial;
		AddChild(_critFrame);

		_critPanel = new MeshInstance3D
		{
			Name = "CritPanel",
			Mesh = new QuadMesh { Size = new Vector2(1.58f, 0.80f) },
			Position = new Vector3(0.0f, 0.0f, 0.01f),
		};
		_critPanelMaterial = CreateBannerMaterial(new Color(0.10f, 0.02f, 0.03f, 0.82f));
		_critPanel.MaterialOverride = _critPanelMaterial;
		AddChild(_critPanel);

		_critLabel = new Label3D
		{
			Text = "CRITICAL!",
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = true,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
			FontSize = 40,
			PixelSize = 0.01f,
			OutlineSize = 14,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Width = 320.0f,
			Position = new Vector3(0.0f, 1.42f, 0.02f),
		};
		_critLabel.Modulate = new Color(1.0f, 0.92f, 0.30f, 1.0f);
		_critLabel.OutlineModulate = new Color(0.55f, 0.03f, 0.02f, 1.0f);
		AddChild(_critLabel);
	}

	private static StandardMaterial3D CreateBannerMaterial(Color color)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
			NoDepthTest = true,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
	}

	private void AddImpactSprite()
	{
		float size = Mathf.Max(Radius * 0.72f * KenneyParticleVfx.ImpactFlashScale, 0.08f);
		MeshInstance3D sprite = KenneyParticleVfx.CreateSprite(
			"ImpactCore",
			"scratch_01.png",
			EffectColor,
			Vector2.One * size);
		sprite.Position = new Vector3(0.0f, Radius * 0.18f, 0.0f);
		if (sprite.Mesh?.SurfaceGetMaterial(0) is StandardMaterial3D material)
		{
			_materials.Add(material);
		}
		(_impactRoot ?? this).AddChild(sprite);
	}

	private void AddImpactParticles()
	{
		(_impactRoot ?? this).AddChild(KenneyParticleVfx.CreateBurst(
			"ImpactSparks",
			"spark_03.png",
			new Color(EffectColor.R, EffectColor.G, EffectColor.B, 0.92f),
			10,
			Mathf.Clamp(Lifetime * 0.62f, 0.16f, 0.34f),
			Mathf.Max(Radius * 3.2f, 1.4f),
			Mathf.Max(Radius * 5.8f, 2.6f),
			180.0f,
			new Vector3(0.0f, -4.2f, 0.0f),
			Mathf.Max(Radius * 0.14f, 0.05f),
			Mathf.Max(Radius * 0.38f, 0.15f),
			Mathf.Max(Radius * 0.14f, 0.06f)));
	}

	private Label3D CreateDamageLabel(int outlineSize, Color outlineColor)
	{
		var label = new Label3D
		{
			Text = Text,
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FixedSize = false,
			NoDepthTest = true,
			// Plain linear filtering at a smaller font: the anisotropic-mipmap
			// filter at FontSize 96 forced an expensive per-hit texture build.
			// FontSize 48 * PixelSize 0.01 keeps the same on-screen size.
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
			FontSize = 48,
			PixelSize = 0.01f,
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

}
