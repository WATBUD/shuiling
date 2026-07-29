using Godot;
using System.Collections.Generic;

// Converts Kenney Particle Pack's monochrome PNGs into reusable 3D billboard
// particles. The source pack contains individual textures (not sprite sheets), so
// motion comes from particle velocity, rotation, scale variation and short lifetimes.
public static class KenneyParticleVfx
{
	private const string Root =
		"res://assets/_downloads/kenney-particle-pack/kenney-particle-pack-7e801dc538996622a91327bb1dd5879cf977aa09/addons/kenney_particle_pack/";
	private const string PresetPath = "res://assets/effects/kenney_particle_vfx_preset.tres";

	private static readonly Dictionary<string, Texture2D?> TextureCache = new();
	private static KenneyParticleVfxPreset? _preset;

	private static KenneyParticleVfxPreset Preset
	{
		get
		{
			_preset ??= ResourceLoader.Exists(PresetPath)
				? ResourceLoader.Load<KenneyParticleVfxPreset>(PresetPath)
				: null;
			return _preset ??= new KenneyParticleVfxPreset();
		}
	}

	// Lets the editor-only preview use the exact Resource instance currently
	// being edited in the Inspector, including unsaved slider changes.
	public static void ApplyPreset(KenneyParticleVfxPreset preset)
	{
		_preset = preset;
	}

	public static float ImpactFlashScale => Preset.ImpactFlashScale;

	public static string TextureFor(string effectName, string skillId, string elementId)
	{
		string token = effectName.ToLowerInvariant();
		if (token.Contains("smoke") || token.Contains("dust") || token.Contains("dissipat"))
		{
			return "smoke_06.png";
		}
		if (token.Contains("slash") || token.Contains("split") || skillId == "gem.skill.whirlwind")
		{
			return "slash_03.png";
		}
		if (token.Contains("chain") || token.Contains("spark") || elementId == "lightning")
		{
			return "spark_06.png";
		}
		if (token.Contains("frost") || token.Contains("ice") || elementId == "ice")
		{
			return "star_08.png";
		}
		if (token.Contains("life") || token.Contains("heal"))
		{
			return "magic_05.png";
		}
		if (token.Contains("cast") || token.Contains("magic"))
		{
			return "magic_03.png";
		}
		if (token.Contains("blast") || token.Contains("explosion") || token.Contains("meteor"))
		{
			return "fire_01.png";
		}
		if (token.Contains("flame") || token.Contains("ember") || elementId == "fire")
		{
			return "flame_05.png";
		}
		if (token.Contains("laser") || elementId == "light")
		{
			return "flare_01.png";
		}

		return "spark_03.png";
	}

	public static GpuParticles3D CreateBurst(
		string name,
		string textureFile,
		Color color,
		int amount,
		float lifetime,
		float minimumSpeed,
		float maximumSpeed,
		float spread,
		Vector3 gravity,
		float width,
		float height,
		float emissionRadius,
		bool localCoords = true,
		Vector3? direction = null)
	{
		bool softBlend = textureFile.StartsWith("smoke_");
		var material = CreateMaterial(textureFile, color, true, !softBlend, true);
		var mesh = new QuadMesh
		{
			Size = new Vector2(
				Mathf.Max(width * Preset.ParticleTextureScale, 0.04f),
				Mathf.Max(height * Preset.ParticleTextureScale, 0.04f)),
			Material = material,
		};
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = Mathf.Max(emissionRadius * 1.12f, 0.04f),
			Direction = direction ?? Vector3.Up,
			Spread = spread,
			InitialVelocityMin = minimumSpeed,
			InitialVelocityMax = maximumSpeed,
			Gravity = gravity,
			ScaleMin = 0.60f,
			ScaleMax = 1.60f,
			AngleMin = -180.0f,
			AngleMax = 180.0f,
			AngularVelocityMin = -220.0f,
			AngularVelocityMax = 220.0f,
			Color = Colors.White,
		};
		return new GpuParticles3D
		{
			Name = name,
			Amount = Mathf.Max(amount, 1),
			Lifetime = Mathf.Max(lifetime, 0.10f),
			OneShot = true,
			Explosiveness = 0.94f,
			Randomness = 0.64f,
			LocalCoords = localCoords,
			ProcessMaterial = process,
			DrawPass1 = mesh,
			VisibilityAabb = new Aabb(new Vector3(-12.0f, -12.0f, -12.0f), new Vector3(24.0f, 24.0f, 24.0f)),
			Emitting = true,
		};
	}

	public static MeshInstance3D CreateSprite(
		string name,
		string textureFile,
		Color color,
		Vector2 size,
		bool billboard = true,
		bool additive = true)
	{
		var mesh = new QuadMesh
		{
			Size = size * Preset.SpriteTextureScale,
			Material = CreateMaterial(textureFile, color, billboard, additive),
		};
		return new MeshInstance3D { Name = name, Mesh = mesh };
	}

	public static StandardMaterial3D CreateMaterial(
		string textureFile,
		Color color,
		bool billboard,
		bool additive,
		bool particleBillboard = false)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			AlbedoTexture = LoadTexture(textureFile),
			EmissionEnabled = true,
			Emission = new Color(color.R, color.G, color.B),
			EmissionEnergyMultiplier = additive ? Preset.BrightEmissionEnergy : Preset.SoftEmissionEnergy,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = billboard
				? particleBillboard
					? BaseMaterial3D.BillboardModeEnum.Particles
					: BaseMaterial3D.BillboardModeEnum.Enabled
				: BaseMaterial3D.BillboardModeEnum.Disabled,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled,
		};
	}

	private static Texture2D? LoadTexture(string file)
	{
		if (TextureCache.TryGetValue(file, out Texture2D? cached))
		{
			return cached;
		}

		string path = Root + file;
		Texture2D? texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
		TextureCache[file] = texture;
		return texture;
	}
}
