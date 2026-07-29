using Godot;
using System.Collections.Generic;

public partial class WorldDrop : Node3D
{
	private static readonly List<WorldDrop> ActiveDropRegistry = new();
	public static IReadOnlyList<WorldDrop> ActiveDrops => ActiveDropRegistry;

	// Drop visuals reuse a handful of colours, so cache their materials and
	// meshes instead of allocating fresh ones per drop. A defeated monster can
	// spit out 5+ drops (a boss ~8) in a single frame; building unique
	// materials/meshes for each was a large slice of the death-frame hitch.
	// Materials are never mutated after creation, so sharing is safe.
	private static readonly Dictionary<int, StandardMaterial3D> BodyMaterialCache = new();
	private static readonly Dictionary<int, StandardMaterial3D> GlowMaterialCache = new();
	private static Mesh? _sharedGoldMesh;
	private static Mesh? _sharedItemMesh;
	private static Mesh? _sharedGlowMesh;
	private static Mesh? _sharedCardFrameMesh;
	private static PackedScene? _cardDropScene;
	private static SphereMesh? _cardParticleMesh;
	private const string CardDropScenePath = "res://assets/scenes/props/Card.tscn";

	private static StandardMaterial3D GetBodyMaterial(Color color, bool isGold)
	{
		int key = unchecked((int)color.ToRgba32() * 2 + (isGold ? 1 : 0));
		if (BodyMaterialCache.TryGetValue(key, out StandardMaterial3D? cached))
		{
			return cached;
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color * 0.45f,
			Roughness = 0.35f,
			Metallic = isGold ? 0.45f : 0.12f,
		};
		BodyMaterialCache[key] = material;
		return material;
	}

	private static StandardMaterial3D GetGlowMaterial(Color color)
	{
		int key = unchecked((int)color.ToRgba32());
		if (GlowMaterialCache.TryGetValue(key, out StandardMaterial3D? cached))
		{
			return cached;
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(color.R, color.G, color.B, 0.28f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			EmissionEnabled = true,
			Emission = color * 0.25f,
		};
		GlowMaterialCache[key] = material;
		return material;
	}

	[Export] public string ItemId { get; set; } = string.Empty;
	[Export] public int Amount { get; set; } = 1;
	[Export] public int GoldAmount { get; set; }
	[Export] public string CardKey { get; set; } = string.Empty;
	[Export] public float PickupRadius { get; set; } = WorldDropConfig.PickupRadius;
	[Export] public float LifetimeSeconds { get; set; } = WorldDropConfig.LifetimeSeconds;

	private float _age;
	private float _bobPhase;
	private float _visualUpdateAccumulator;
	private Label3D? _label;

	public bool IsGoldDrop => GoldAmount > 0;
	public bool IsCardDrop => !string.IsNullOrEmpty(CardKey);
	public bool IsCollected { get; private set; }
	public float AgeSeconds => _age;

	public override void _Ready()
	{
		if (!ActiveDropRegistry.Contains(this))
		{
			ActiveDropRegistry.Add(this);
		}

		AddToGroup("world_drops");
		_bobPhase = (float)GD.RandRange(0.0, Mathf.Tau);
		BuildVisual();
	}

	public override void _ExitTree()
	{
		ActiveDropRegistry.Remove(this);
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		_age += step;
		if (_age >= LifetimeSeconds)
		{
			QueueFree();
			return;
		}

		_visualUpdateAccumulator += step;
		if (_visualUpdateAccumulator < PerformanceConfig.WorldDropVisualRefreshIntervalSeconds)
		{
			return;
		}

		float visualStep = _visualUpdateAccumulator;
		_visualUpdateAccumulator = 0.0f;
		_bobPhase += visualStep * 3.4f;
		RotationDegrees = new Vector3(0.0f, RotationDegrees.Y + visualStep * 65.0f, 0.0f);
		if (_label != null)
		{
			_label.Position = new Vector3(0.0f, 0.92f + Mathf.Sin(_bobPhase) * 0.08f, 0.0f);
		}
	}

	public bool TryCollect(PlayerController player)
	{
		if (IsCollected || !IsInstanceValid(player) || GlobalPosition.DistanceTo(player.GlobalPosition) > PickupRadius)
		{
			return false;
		}

		IsCollected = true;
		if (IsCardDrop)
		{
			player.AwardMonsterCardByKey(CardKey);
		}
		else if (IsGoldDrop)
		{
			player.AddGold(GoldAmount);
		}
		else if (!string.IsNullOrWhiteSpace(ItemId))
		{
			player.AddInventoryItem(ItemId, Mathf.Max(Amount, 1));
		}

		QueueFree();
		return true;
	}

	private void BuildVisual()
	{
		if (IsCardDrop)
		{
			BuildCardVisual();
			return;
		}

		var color = IsGoldDrop
			? new Color(1.0f, 0.78f, 0.18f, 0.96f)
			: GetItemColor(ItemId);

		Mesh mesh;
		if (IsGoldDrop)
		{
			mesh = _sharedGoldMesh ??= new CylinderMesh { TopRadius = 0.22f, BottomRadius = 0.24f, Height = 0.12f, RadialSegments = 24 };
		}
		else
		{
			mesh = _sharedItemMesh ??= new BoxMesh { Size = new Vector3(0.42f, 0.42f, 0.42f) };
		}

		var body = new MeshInstance3D
		{
			Name = IsGoldDrop ? "GoldDropVisual" : "ItemDropVisual",
			Mesh = mesh,
			Position = new Vector3(0.0f, 0.24f, 0.0f),
			RotationDegrees = IsGoldDrop ? new Vector3(90.0f, 0.0f, 0.0f) : new Vector3(18.0f, 35.0f, 0.0f),
		};
		body.SetSurfaceOverrideMaterial(0, GetBodyMaterial(color, IsGoldDrop));
		AddChild(body);

		if (!IsGoldDrop)
		{
			var glow = new MeshInstance3D
			{
				Name = "ItemGlow",
				Mesh = _sharedGlowMesh ??= new SphereMesh { Radius = 0.32f, Height = 0.42f },
				Position = new Vector3(0.0f, 0.24f, 0.0f),
			};
			glow.SetSurfaceOverrideMaterial(0, GetGlowMaterial(color));
			AddChild(glow);
		}

		_label = new Label3D
		{
			Name = "DropLabel",
			Text = GetDisplayText(),
			Position = new Vector3(0.0f, 0.92f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 18,
			PixelSize = 0.007f,
			OutlineSize = 5,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 260.0f,
		};
		_label.OutlineModulate = new Color(0.02f, 0.02f, 0.018f, 0.96f);
		_label.Modulate = IsGoldDrop ? new Color(1.0f, 0.88f, 0.32f) : color.Lightened(0.25f);
		AddChild(_label);
	}

	// Monster cards use the editor-authored Card.tscn so artists can adjust the
	// drop model without changing code.
	private void BuildCardVisual()
	{
		var frameColor = new Color(1.0f, 0.82f, 0.34f, 1.0f);
		_cardDropScene ??= ResourceLoader.Exists(CardDropScenePath)
			? ResourceLoader.Load<PackedScene>(CardDropScenePath)
			: null;
		if (_cardDropScene?.Instantiate() is Node3D cardModel)
		{
			cardModel.Name = "CardDropModel";
			cardModel.Position = Vector3.Zero;
			AddChild(cardModel);
		}
		else
		{
			// Keep a visible fallback if an editor move/rename temporarily makes
			// the authored scene unavailable.
			var fallback = new MeshInstance3D
			{
				Name = "CardDropFallback",
				Mesh = _sharedCardFrameMesh ??= new BoxMesh { Size = new Vector3(0.40f, 0.56f, 0.035f) },
				Position = new Vector3(0.0f, 0.42f, 0.0f),
			};
			fallback.SetSurfaceOverrideMaterial(0, GetCardMaterial(frameColor, true));
			AddChild(fallback);
		}

		AddCardRisingParticles(frameColor);

		_label = new Label3D
		{
			Name = "DropLabel",
			Text = GetDisplayText(),
			Position = new Vector3(0.0f, 1.05f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 18,
			PixelSize = 0.007f,
			OutlineSize = 5,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 260.0f,
		};
		_label.OutlineModulate = new Color(0.02f, 0.02f, 0.018f, 0.96f);
		_label.Modulate = frameColor.Lightened(0.2f);
		AddChild(_label);
	}

	private void AddCardRisingParticles(Color color)
	{
		if (_cardParticleMesh == null)
		{
			var particleMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(color.R, color.G, color.B, 0.88f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				EmissionEnabled = true,
				Emission = color * 3.2f,
			};
			_cardParticleMesh = new SphereMesh
			{
				Radius = 0.045f,
				Height = 0.09f,
				RadialSegments = 10,
				Rings = 5,
			};
			_cardParticleMesh.SurfaceSetMaterial(0, particleMaterial);
		}

		// Keep the process material instance-local. Sharing one live GPU particle
		// material across drops can race uniform-set creation on Godot 4.7 D3D12.
		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.34f,
			Direction = Vector3.Up,
			Spread = 32.0f,
			Gravity = new Vector3(0.0f, 0.28f, 0.0f),
			InitialVelocityMin = 0.48f,
			InitialVelocityMax = 1.35f,
			ScaleMin = 0.55f,
			ScaleMax = 1.35f,
			Color = new Color(color.R, color.G, color.B, 0.92f),
		};

		var particles = new GpuParticles3D
		{
			Name = "CardRisingParticles",
			Amount = 30,
			Lifetime = 1.65f,
			Preprocess = 0.0f,
			Randomness = 0.72f,
			VisibilityAabb = new Aabb(new Vector3(-1.2f, -0.1f, -1.2f), new Vector3(2.4f, 3.2f, 2.4f)),
			ProcessMaterial = processMaterial,
			DrawPass1 = _cardParticleMesh,
			Emitting = false,
			Position = new Vector3(0.0f, 0.04f, 0.0f),
		};
		AddChild(particles);
		// Start only after RenderingServer has registered the new material and
		// draw-pass resources, avoiding a draw in the construction frame.
		particles.SetDeferred("emitting", true);
	}

	private static StandardMaterial3D GetCardMaterial(Color color, bool isFrame)
	{
		int key = unchecked((int)color.ToRgba32() * 2 + (isFrame ? 1 : 0)) ^ 0x5C0DE;
		if (BodyMaterialCache.TryGetValue(key, out StandardMaterial3D? cached))
		{
			return cached;
		}

		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			EmissionEnabled = true,
			Emission = color * (isFrame ? 0.55f : 0.32f),
			Roughness = 0.3f,
			Metallic = isFrame ? 0.6f : 0.15f,
		};
		BodyMaterialCache[key] = material;
		return material;
	}

	private string GetDisplayText()
	{
		if (IsCardDrop)
		{
			return LocaleText.F("drop.card", ExternalModelLibrary.LocalizedCardName(CardKey));
		}

		if (IsGoldDrop)
		{
			return LocaleText.F("drop.gold", GoldAmount);
		}

		string name = MonsterLootCatalog.IsMonsterLoot(ItemId)
			? LocaleText.T(MonsterLootCatalog.GetNameKey(ItemId))
			: LocaleText.T(BuildCatalog.GetItemNameKey(ItemId));
		return Amount > 1 ? $"{name} x{Amount}" : name;
	}

	private static Color GetItemColor(string itemId)
	{
		if (itemId.StartsWith("equip."))
		{
			return new Color(0.50f, 0.78f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.attribute."))
		{
			return new Color(0.96f, 0.46f, 1.0f, 0.95f);
		}

		if (itemId.StartsWith("gem.skill."))
		{
			return new Color(0.40f, 1.0f, 0.66f, 0.95f);
		}

		if (MonsterLootCatalog.IsMonsterLoot(itemId))
		{
			return MonsterLootCatalog.GetDropColor(itemId);
		}

		return new Color(0.82f, 0.92f, 1.0f, 0.95f);
	}
}
