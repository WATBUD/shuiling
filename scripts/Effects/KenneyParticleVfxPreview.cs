using Godot;
using System.Collections.Generic;

[Tool]
public partial class KenneyParticleVfxPreview : Node3D
{
	public enum PreviewDisplayMode
	{
		All,
		Impact,
		Fire,
		Ice,
		Lightning,
		Magic,
		Slash,
		Smoke,
	}

	[Export] public KenneyParticleVfxPreset? VfxPreset { get; set; }
	[Export] public PreviewDisplayMode DisplayMode { get; set; } = PreviewDisplayMode.All;
	[Export(PropertyHint.Range, "0.25,3.0,0.05")] public float PreviewSize { get; set; } = 1.0f;
	[Export(PropertyHint.Range, "0.4,4.0,0.1")] public float ReplayInterval { get; set; } = 1.35f;
	[Export] public bool ShowLabels { get; set; } = true;
	[Export] public bool PlayParticles { get; set; } = true;

	private readonly List<(Node3D Root, PreviewDisplayMode Effect)> _samples = new();
	private readonly List<GpuParticles3D> _bursts = new();
	private Node3D? _generatedRoot;
	private float _replayTimer;
	private PreviewDisplayMode _lastDisplayMode = (PreviewDisplayMode)(-1);
	private float _lastPreviewSize = -1.0f;
	private float _lastReplayInterval = -1.0f;
	private float _lastParticleScale = -1.0f;
	private float _lastSpriteScale = -1.0f;
	private float _lastImpactScale = -1.0f;
	private float _lastBrightEnergy = -1.0f;
	private float _lastSoftEnergy = -1.0f;
	private bool _lastShowLabels;
	private bool _lastPlayParticles;

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		RebuildPreview();
	}

	public override void _Process(double delta)
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		if (PreviewSettingsChanged())
		{
			RebuildPreview();
		}

		if (!PlayParticles)
		{
			return;
		}

		_replayTimer -= (float)delta;
		if (_replayTimer <= 0.0f)
		{
			SpawnBursts();
			_replayTimer = Mathf.Max(ReplayInterval, 0.4f);
		}
	}

	private bool PreviewSettingsChanged()
	{
		KenneyParticleVfxPreset preset = GetPreset();
		bool changed =
			_lastDisplayMode != DisplayMode ||
			!Mathf.IsEqualApprox(_lastPreviewSize, PreviewSize) ||
			!Mathf.IsEqualApprox(_lastReplayInterval, ReplayInterval) ||
			!Mathf.IsEqualApprox(_lastParticleScale, preset.ParticleTextureScale) ||
			!Mathf.IsEqualApprox(_lastSpriteScale, preset.SpriteTextureScale) ||
			!Mathf.IsEqualApprox(_lastImpactScale, preset.ImpactFlashScale) ||
			!Mathf.IsEqualApprox(_lastBrightEnergy, preset.BrightEmissionEnergy) ||
			!Mathf.IsEqualApprox(_lastSoftEnergy, preset.SoftEmissionEnergy) ||
			_lastShowLabels != ShowLabels ||
			_lastPlayParticles != PlayParticles;

		_lastDisplayMode = DisplayMode;
		_lastPreviewSize = PreviewSize;
		_lastReplayInterval = ReplayInterval;
		_lastParticleScale = preset.ParticleTextureScale;
		_lastSpriteScale = preset.SpriteTextureScale;
		_lastImpactScale = preset.ImpactFlashScale;
		_lastBrightEnergy = preset.BrightEmissionEnergy;
		_lastSoftEnergy = preset.SoftEmissionEnergy;
		_lastShowLabels = ShowLabels;
		_lastPlayParticles = PlayParticles;
		return changed;
	}

	private void RebuildPreview()
	{
		KenneyParticleVfxPreset preset = GetPreset();
		KenneyParticleVfx.ApplyPreset(preset);

		if (_generatedRoot != null && IsInstanceValid(_generatedRoot))
		{
			_generatedRoot.Free();
		}

		_samples.Clear();
		_bursts.Clear();
		_generatedRoot = new Node3D { Name = "GeneratedPreview" };
		AddChild(_generatedRoot);

		var effects = new List<PreviewDisplayMode>();
		if (DisplayMode == PreviewDisplayMode.All)
		{
			for (PreviewDisplayMode effect = PreviewDisplayMode.Impact; effect <= PreviewDisplayMode.Smoke; effect++)
			{
				effects.Add(effect);
			}
		}
		else
		{
			effects.Add(DisplayMode);
		}

		float spacing = Mathf.Max(PreviewSize * preset.SpriteTextureScale * 1.35f, 4.25f);
		int columns = DisplayMode == PreviewDisplayMode.All ? 4 : 1;
		for (int index = 0; index < effects.Count; index++)
		{
			int column = index % columns;
			int row = index / columns;
			int itemsInRow = Mathf.Min(columns, effects.Count - row * columns);
			float centeredColumn = column - (itemsInRow - 1) * 0.5f;
			var sample = new Node3D
			{
				Name = effects[index].ToString(),
				Position = new Vector3(centeredColumn * spacing, 0.0f, row * spacing),
			};
			_generatedRoot.AddChild(sample);
			BuildSample(sample, effects[index]);
			_samples.Add((sample, effects[index]));
		}

		SpawnBursts();
		_replayTimer = Mathf.Max(ReplayInterval, 0.4f);
		PreviewSettingsChanged();
	}

	private void BuildSample(Node3D root, PreviewDisplayMode effect)
	{
		(string texture, Color color) = VisualFor(effect);
		Vector2 spriteSize = effect switch
		{
			PreviewDisplayMode.Lightning => new Vector2(PreviewSize * 0.52f, PreviewSize * 3.2f),
			PreviewDisplayMode.Impact => Vector2.One * PreviewSize * GetPreset().ImpactFlashScale,
			_ => Vector2.One * PreviewSize,
		};
		MeshInstance3D sprite = KenneyParticleVfx.CreateSprite(
			$"{effect}Sprite",
			texture,
			color,
			spriteSize);
		sprite.Position = effect == PreviewDisplayMode.Lightning && sprite.Mesh is QuadMesh lightningQuad
			? Vector3.Up * lightningQuad.Size.Y * 0.48f
			: Vector3.Up * PreviewSize;
		root.AddChild(sprite);

		MeshInstance3D ground = KenneyParticleVfx.CreateSprite(
			$"{effect}Ground",
			"symbol_02.png",
			new Color(color.R, color.G, color.B, 0.24f),
			Vector2.One * PreviewSize * 1.15f,
			false);
		ground.Position = Vector3.Up * 0.025f;
		ground.RotationDegrees = new Vector3(-90.0f, 0.0f, 0.0f);
		root.AddChild(ground);

		if (ShowLabels)
		{
			var label = new Label3D
			{
				Name = $"{effect}Label",
				Text = effect.ToString().ToUpperInvariant(),
				Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
				FontSize = 36,
				PixelSize = 0.012f,
				OutlineSize = 8,
				Position = Vector3.Up * PreviewSize * 2.15f,
				Modulate = Colors.White,
			};
			root.AddChild(label);
		}
	}

	private void SpawnBursts()
	{
		foreach (GpuParticles3D burst in _bursts)
		{
			if (IsInstanceValid(burst))
			{
				burst.Free();
			}
		}
		_bursts.Clear();

		if (!PlayParticles)
		{
			return;
		}

		foreach ((Node3D root, PreviewDisplayMode effect) in _samples)
		{
			(string texture, Color color) = VisualFor(effect);
			bool smoke = effect == PreviewDisplayMode.Smoke;
			GpuParticles3D burst = KenneyParticleVfx.CreateBurst(
				$"{effect}Burst",
				texture,
				color,
				smoke ? 18 : 28,
				smoke ? 1.05f : 0.68f,
				smoke ? 0.45f : 1.8f,
				smoke ? 1.8f : 5.2f,
				smoke ? 42.0f : 160.0f,
				smoke ? new Vector3(0.0f, 0.8f, 0.0f) : new Vector3(0.0f, -1.8f, 0.0f),
				PreviewSize * (smoke ? 0.22f : 0.10f),
				PreviewSize * (smoke ? 0.34f : 0.26f),
				PreviewSize * 0.15f);
			burst.Position = Vector3.Up * PreviewSize;
			root.AddChild(burst);
			_bursts.Add(burst);
		}
	}

	private KenneyParticleVfxPreset GetPreset()
	{
		return VfxPreset ??= new KenneyParticleVfxPreset();
	}

	private static (string Texture, Color Color) VisualFor(PreviewDisplayMode effect)
	{
		return effect switch
		{
			PreviewDisplayMode.Fire => ("fire_02.png", new Color(1.0f, 0.30f, 0.04f, 0.95f)),
			PreviewDisplayMode.Ice => ("star_08.png", new Color(0.52f, 0.88f, 1.0f, 0.95f)),
			PreviewDisplayMode.Lightning => ("spark_06.png", new Color(1.0f, 0.94f, 0.34f, 0.98f)),
			PreviewDisplayMode.Magic => ("magic_03.png", new Color(0.68f, 0.34f, 1.0f, 0.95f)),
			PreviewDisplayMode.Slash => ("slash_03.png", new Color(1.0f, 0.82f, 0.28f, 0.95f)),
			PreviewDisplayMode.Smoke => ("smoke_06.png", new Color(0.32f, 0.35f, 0.40f, 0.72f)),
			_ => ("scratch_01.png", new Color(1.0f, 0.54f, 0.12f, 0.95f)),
		};
	}
}
