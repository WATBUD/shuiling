using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private static readonly string[] NpcNames =
	{
		"name.npc.guard",
		"name.npc.hunter",
		"name.npc.gatherer",
		"name.npc.apprentice",
	};

	private static readonly string[] NpcAbilities =
	{
		"ability.npc.heal",
		"ability.npc.guard",
		"ability.npc.gather",
		"ability.npc.command",
		"ability.npc.supply",
	};

	private static readonly string[] MonsterAbilities =
	{
		"ability.monster.claw",
		"ability.monster.poison",
		"ability.monster.hide",
		"ability.monster.charge",
		"ability.monster.track",
	};

	private static readonly string[] NpcRoles =
	{
		"Support",
		"Ranged",
		"Ranged",
		"Tank",
		"Gatherer",
		"Builder",
		"DPS",
	};

	private static readonly string[] Personalities =
	{
		"personality.calm",
		"personality.brave",
		"personality.cautious",
		"personality.impulsive",
		"personality.friendly",
		"personality.stubborn",
	};

	private static readonly string[] PassiveAbilities =
	{
		"passive.danger_sense",
		"passive.night_instinct",
		"passive.tough_body",
		"passive.combo_rhythm",
		"passive.protector",
		"passive.fast_growth",
	};

	[Export] public float MapSize { get; set; } = 150.0f;
	[Export] public int PropCount { get; set; } = 110;
	// Total initial monster population shared evenly by all wild maps.
	// Four maps currently receive 18 monsters each (72 total).
	[Export] public int ActorCount { get; set; } = 72;
	[Export] public int CityNpcCount { get; set; } = 28;
	[Export] public float MonsterRespawnInterval { get; set; } = 14.0f;
	[Export] public float MonsterRespawnThresholdRatio { get; set; } = 0.55f;
	[Export] public int MonsterRespawnBatchSize { get; set; } = 6;
	[Export] public float BossRespawnInterval { get; set; } = 180.0f;
	[Export] public int SeedValue { get; set; }

	private readonly RandomNumberGenerator _rng = new();
	// Active world-slot identity/seed for saving (Minecraft-style worlds).
	private int _activeWorldSeed;
	private string _worldSaveId = string.Empty;
	private string _worldSaveName = string.Empty;
	private bool _autoSaveOnExit = true;
	private MusicPlayer _musicPlayer = null!;

	// Whether leaving this world (to menu or app close) saves automatically.
	public bool AutoSaveOnExit => _autoSaveOnExit;
	private readonly List<Vector3> _obstaclePositions = new();
	private readonly List<Vector3> _wildObstaclePositions = new();
	private readonly Dictionary<string, Node3D> _wildMapRootsById = new();
	private readonly Dictionary<string, List<Vector3>> _wildObstaclePositionsById = new();
	private readonly Dictionary<string, int> _wildMonsterTargetCountsById = new();
	// World Tier progression (docs/world_progression.md): every wild map supports
	// Tiers 1-10. Each PLAYER has their own unlocked/selected tier per map (their
	// save), and each populated (map, tier) pair is a parallel "instance": players
	// only share monsters/see each other when on the same map AND the same tier.
	// Instance keys are WildInstanceKey(mapId, tier).
	private readonly Dictionary<string, SimpleActor> _wildBossesByInstance = new();
	private readonly Dictionary<string, int> _wildMapUnlockedTiersById = new();
	private readonly Dictionary<string, int> _wildMapSelectedTiersById = new();
	private readonly Dictionary<string, (string MapId, int Tier)> _spawnedWildInstancesByKey = new();
	private readonly Dictionary<string, float> _wildBossRespawnRemainingByInstance = new();
	private readonly List<string> _instanceCleanupScratch = new();
	private readonly Dictionary<string, Vector3> _wildBossSpawnPositionsByMapId = new();
	private readonly Dictionary<CollisionObject3D, (uint Layer, uint Mask)> _mapCollisionDefaults = new();
	private readonly Vector3 _spawnCampCenter = new(0.0f, 0.0f, 8.0f);
	private readonly Vector3 _mainCityCenter = new(0.0f, 0.0f, -20.0f);
	private readonly Vector3 _citySpawnPosition = new(5.2f, 0.0f, -16.2f);
	private readonly Vector3 _wildSpawnPosition = new(0.0f, 0.0f, 8.0f);
	private Vector3 CityPortalPosition => _mainCityCenter + new Vector3(0.0f, 0.0f, -28.0f);
	private Vector3 CityPortalArrivalPosition => CityPortalPosition + new Vector3(0.0f, 0.0f, 4.2f);

	private Node3D _mapRoot = null!;
	private Node3D _propsRoot = null!;
	private Node3D _actorsRoot = null!;
	private Node3D _mapsRoot = null!;
	private Node3D _wildMapRoot = null!;
	private Node3D _cityMapRoot = null!;
	private string _activeMapId = "city";
	private float _monsterRespawnRemaining;
	private PlayerController _player = null!;
	private bool _worldActorsGenerated;

	public string ActiveMapId => _activeMapId;
	public string GetActiveMapDisplayName()
	{
		if (IsCaveMapId(_activeMapId))
		{
			return GetCaveMapDisplayName(_activeMapId);
		}
		return _activeMapId == "city"
			? LocaleText.T("map.city")
			: GetWildMapDisplayName(_activeMapId);
	}
	public int CurrentLivingMonsterCount
	{
		get
		{
			foreach (WildMapDefinition wildMap in WildMaps)
			{
				if (wildMap.Id == _activeMapId)
				{
					return CountLivingMonstersInInstance(_activeMapId, GetSelectedTier(_activeMapId));
				}
			}
			if (IsCaveMapId(_activeMapId))
			{
				return CountLivingMonsters(_activeMapId);
			}

			return 0;
		}
	}

	private static readonly WildMapDefinition[] WildMaps =
	{
		new("wild_forest", "map.wild.forest", "WildForestMap", new Color(0.24f, 0.46f, 0.29f)),
		new("wild_marsh", "map.wild.marsh", "WildMarshMap", new Color(0.18f, 0.38f, 0.34f)),
		new("wild_badlands", "map.wild.badlands", "WildBadlandsMap", new Color(0.42f, 0.30f, 0.20f)),
		new("wild_snow", "map.wild.snow", "WildSnowMap", new Color(0.76f, 0.84f, 0.90f)),
	};

	private static readonly BossDefinition[] WildBosses =
	{
		new("wild_forest", "boss.forest.name", "name.monster.boar", "DPS", "loot.beast_hide", 12, 1450, 52, 27, 190, 120, 2.78f, new Color(0.42f, 0.92f, 0.30f, 0.94f)),
		new("wild_marsh", "boss.marsh.name", "name.monster.slime", "Support", "loot.water_core", 14, 1750, 58, 34, 230, 150, 3.05f, new Color(0.24f, 0.88f, 0.82f, 0.94f)),
		new("wild_badlands", "boss.badlands.name", "name.monster.lion", "DPS", "loot.red_horn", 17, 2250, 76, 40, 310, 210, 2.92f, new Color(1.0f, 0.32f, 0.06f, 0.96f)),
		new("wild_snow", "boss.snow.name", "name.monster.bear", "Tank", "loot.dragon_scale", 19, 2750, 72, 55, 380, 260, 3.15f, new Color(0.54f, 0.86f, 1.0f, 0.96f)),
	};

	private readonly record struct WildMapDefinition(string Id, string NameKey, string RootName, Color GroundColor);
	private readonly record struct BossDefinition(string MapId, string NameKey, string SpeciesNameKey, string CombatRole, string PrimaryLootId, int Level, int MaxHealth, int Attack, int Defense, int ExperienceReward, int GoldReward, float VisualScale, Color AuraColor);
	public readonly record struct BossStatusSnapshot(string MapId, string MapName, string BossName, bool IsAlive, int RespawnSeconds);
	private readonly record struct CityNpcStation(string NameKey, Vector3 Offset, float YawDegrees, float WanderRadius, string Role);

	private StandardMaterial3D _matGround = null!;
	private StandardMaterial3D _matMeadow = null!;
	private StandardMaterial3D _matField = null!;
	private StandardMaterial3D _matPath = null!;
	private StandardMaterial3D _matCobblestone = null!;
	private StandardMaterial3D _matRoadEdge = null!;
	private StandardMaterial3D _matWall = null!;
	private StandardMaterial3D _matTrunk = null!;
	private StandardMaterial3D _matLeaf = null!;
	private StandardMaterial3D _matRock = null!;
	private StandardMaterial3D _matWater = null!;
	private StandardMaterial3D _matShallowWater = null!;
	private StandardMaterial3D _matNpc = null!;
	private StandardMaterial3D _matMonster = null!;
	private StandardMaterial3D _matActorDark = null!;
	private StandardMaterial3D _matHorn = null!;
	private StandardMaterial3D _matSkin = null!;
	private StandardMaterial3D _matLeather = null!;
	private StandardMaterial3D _matMetal = null!;
	private StandardMaterial3D _matNpcAccent = null!;
	private StandardMaterial3D _matMonsterBelly = null!;
	private StandardMaterial3D _matMonsterClaw = null!;
	private StandardMaterial3D _matEyeWhite = null!;
	private StandardMaterial3D _matGrassBright = null!;
	private StandardMaterial3D _matGrassDark = null!;
	private StandardMaterial3D _matFlowerWarm = null!;
	private StandardMaterial3D _matFlowerCool = null!;
	private StandardMaterial3D _matMushroomCap = null!;
	private StandardMaterial3D _matWood = null!;
	private StandardMaterial3D _matTorchFire = null!;
	private StandardMaterial3D _matCrystal = null!;
	private StandardMaterial3D _matRune = null!;
	private StandardMaterial3D _matTentCloth = null!;
	private StandardMaterial3D _matNest = null!;
	private StandardMaterial3D _matPondBank = null!;

	public override void _Ready()
	{
		LocaleText.LanguageChanged += RefreshLocalizedWorldLabels;

		NetworkBeforeWorldGeneration();
		// Offline: seed from the chosen/loaded world slot (online: NetworkBefore…
		// already forced SeedValue to the shared Net.WorldSeed).
		if (SeedValue == 0 && GameLaunchOptions.ActiveSeed != 0)
		{
			SeedValue = GameLaunchOptions.ActiveSeed;
		}
		if (SeedValue == 0)
		{
			_rng.Randomize();
		}
		else
		{
			_rng.Seed = (ulong)SeedValue;
		}
		_activeWorldSeed = unchecked((int)_rng.Seed);
		_worldSaveId = GameLaunchOptions.ActiveWorldId;
		_worldSaveName = GameLaunchOptions.NewWorldName;
		_autoSaveOnExit = GameLaunchOptions.NewWorldAutoSave;

		CreateMaterials();
		BuildEnvironment();
		BuildMap();
		CreatePlayer();
		SpawnActors();
		AddCrosshair();
		_musicPlayer = new MusicPlayer { Name = "MusicPlayer" };
		AddChild(_musicPlayer);
		if (GameLaunchOptions.LoadSaveOnWorldReady)
		{
			LoadRequestedSave();
			GameLaunchOptions.StartNewGame();
		}
		else if (!string.IsNullOrEmpty(_worldSaveId) && NetworkManager.Instance is not { IsClient: true })
		{
			// A brand-new world (single-player or fresh host): persist it once so
			// it appears in the world list even before the first manual save.
			CallDeferred(nameof(AutoSaveNewWorld));
		}
		NetworkAfterWorldReady();
		_musicPlayer.PlayForMap(_activeMapId);

		// Multiplayer: broadcast the player's chosen character name (not OS name).
		if (NetworkManager.Instance is { IsOnline: true } && _player != null && IsInstanceValid(_player))
		{
			NetworkManager.Instance.SetLocalPlayerName(LocaleText.T(_player.PlayerName));
		}
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshLocalizedWorldLabels;
		NetworkOnWorldExit();
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		UpdateMapTravelCooldown(step);
		UpdateMonsterRespawns(step);
		UpdateWildBosses(step);
		UpdateCaveRespawns(step);
	}

	private void CreateMaterials()
	{
		_matGround = MakeMaterial(new Color(0.24f, 0.46f, 0.29f));
		_matMeadow = MakeMaterial(new Color(0.31f, 0.56f, 0.26f));
		_matField = MakeMaterial(new Color(0.50f, 0.43f, 0.24f));
		_matPath = MakeMaterial(new Color(0.47f, 0.36f, 0.22f));
		_matCobblestone = MakeMaterial(new Color(0.48f, 0.46f, 0.40f));
		_matRoadEdge = MakeMaterial(new Color(0.24f, 0.22f, 0.18f));
		_matWall = MakeMaterial(new Color(0.36f, 0.38f, 0.40f));
		_matTrunk = MakeMaterial(new Color(0.33f, 0.21f, 0.12f));
		_matLeaf = MakeMaterial(new Color(0.12f, 0.44f, 0.22f));
		_matRock = MakeMaterial(new Color(0.43f, 0.44f, 0.43f));
		_matWater = MakeMaterial(new Color(0.13f, 0.38f, 0.66f, 0.72f), 0.08f);
		_matShallowWater = MakeMaterial(new Color(0.34f, 0.62f, 0.78f, 0.52f), 0.06f);
		_matNpc = MakeMaterial(new Color(0.18f, 0.68f, 0.92f));
		_matMonster = MakeMaterial(new Color(0.84f, 0.16f, 0.13f));
		_matActorDark = MakeMaterial(new Color(0.08f, 0.08f, 0.09f));
		_matHorn = MakeMaterial(new Color(0.94f, 0.86f, 0.58f));
		_matSkin = MakeMaterial(new Color(0.86f, 0.62f, 0.44f));
		_matLeather = MakeMaterial(new Color(0.26f, 0.16f, 0.10f));
		_matMetal = MakeMaterial(new Color(0.72f, 0.76f, 0.78f), 0.38f);
		_matNpcAccent = MakeMaterial(new Color(0.94f, 0.76f, 0.28f));
		_matMonsterBelly = MakeMaterial(new Color(0.46f, 0.08f, 0.08f));
		_matMonsterClaw = MakeMaterial(new Color(0.95f, 0.88f, 0.70f), 0.45f);
		_matEyeWhite = MakeMaterial(new Color(0.98f, 0.96f, 0.88f), 0.35f);
		_matGrassBright = MakeMaterial(new Color(0.36f, 0.64f, 0.24f));
		_matGrassDark = MakeMaterial(new Color(0.11f, 0.34f, 0.17f));
		_matFlowerWarm = MakeMaterial(new Color(1.0f, 0.63f, 0.24f));
		_matFlowerCool = MakeMaterial(new Color(0.62f, 0.72f, 1.0f));
		_matMushroomCap = MakeMaterial(new Color(0.75f, 0.16f, 0.18f));
		_matWood = MakeMaterial(new Color(0.40f, 0.27f, 0.14f));
		_matTorchFire = MakeMaterial(new Color(1.0f, 0.44f, 0.12f, 0.78f), 0.18f);
		_matCrystal = MakeMaterial(new Color(0.36f, 0.86f, 1.0f, 0.82f), 0.12f);
		_matRune = MakeMaterial(new Color(0.72f, 0.42f, 1.0f, 0.8f), 0.12f);
		_matTentCloth = MakeMaterial(new Color(0.66f, 0.18f, 0.18f));
		_matNest = MakeMaterial(new Color(0.18f, 0.12f, 0.10f));
		_matPondBank = MakeMaterial(new Color(0.30f, 0.26f, 0.17f));
		CreateBiomeMaterials();
	}

	private void BuildEnvironment()
	{
		_skyMaterial = new ProceduralSkyMaterial
		{
			SkyTopColor = new Color(0.20f, 0.45f, 0.86f),
			SkyHorizonColor = new Color(0.82f, 0.90f, 1.0f),
			SkyCurve = 0.22f,
			GroundBottomColor = new Color(0.10f, 0.18f, 0.18f),
			GroundHorizonColor = new Color(0.52f, 0.64f, 0.50f),
			GroundCurve = 0.18f,
			SunAngleMax = 32.0f,
			SunCurve = 0.08f,
		};
		var sky = new Sky
		{
			SkyMaterial = _skyMaterial,
		};
		var environment = new Environment
		{
			BackgroundMode = Environment.BGMode.Sky,
			Sky = sky,
			BackgroundEnergyMultiplier = 0.92f,
			AmbientLightSource = Environment.AmbientSource.Sky,
			AmbientLightColor = new Color(0.72f, 0.80f, 0.90f),
			AmbientLightEnergy = 0.72f,
			FogEnabled = true,
			FogLightColor = new Color(0.74f, 0.82f, 0.88f),
			FogLightEnergy = 0.32f,
			FogDensity = 0.008f,
			GlowEnabled = true,
			GlowIntensity = 0.16f,
			GlowStrength = 0.42f,
			TonemapMode = Environment.ToneMapper.Filmic,
		};

		_worldEnvironment = new WorldEnvironment
		{
			Name = "WorldEnvironment",
			Environment = environment,
		};
		AddChild(_worldEnvironment);

		_sunLight = new DirectionalLight3D
		{
			Name = "Sun",
			LightEnergy = 2.4f,
			LightColor = new Color(1.0f, 0.91f, 0.76f),
			ShadowEnabled = true,
			RotationDegrees = new Vector3(-50.0f, -35.0f, 0.0f),
		};
		AddChild(_sunLight);

		CreateSkylineBackdrop();
	}

	private void CreateSkylineBackdrop()
	{
		var backdrop = new Node3D { Name = "SkylineBackdrop" };
		AddChild(backdrop);
		var mountainMaterial = MakeMaterial(new Color(0.20f, 0.31f, 0.38f, 0.70f));
		var cloudMaterial = MakeMaterial(new Color(0.92f, 0.96f, 1.0f, 0.82f), 0.04f);

		for (int index = 0; index < 14; index++)
		{
			float x = -92.0f + index * 14.0f;
			float height = 10.0f + (index % 4) * 2.2f;
			AddMesh(
				backdrop,
				$"DistantMountain{index}",
				CylinderMeshFor(0.0f, 9.0f + (index % 3) * 1.4f, height),
				new Vector3(x, height * 0.5f - 0.4f, -105.0f - (index % 2) * 5.0f),
				new Vector3(0.0f, 30.0f + index * 7.0f, 0.0f),
				new Vector3(1.6f, 1.0f, 0.38f),
				mountainMaterial
			);
		}

		for (int index = 0; index < 9; index++)
		{
			float x = -78.0f + index * 19.5f;
			float y = 31.0f + (index % 3) * 2.4f;
			float z = -92.0f - (index % 2) * 6.0f;
			AddMesh(backdrop, $"CloudCore{index}", new SphereMesh { Radius = 2.6f, Height = 1.1f }, new Vector3(x, y, z), Vector3.Zero, new Vector3(2.1f, 0.42f, 0.72f), cloudMaterial);
			AddMesh(backdrop, $"CloudLeft{index}", new SphereMesh { Radius = 1.8f, Height = 0.9f }, new Vector3(x - 3.0f, y - 0.2f, z + 0.4f), Vector3.Zero, new Vector3(1.8f, 0.38f, 0.7f), cloudMaterial);
			AddMesh(backdrop, $"CloudRight{index}", new SphereMesh { Radius = 2.0f, Height = 0.9f }, new Vector3(x + 3.2f, y - 0.1f, z - 0.3f), Vector3.Zero, new Vector3(1.7f, 0.36f, 0.68f), cloudMaterial);
		}
	}

	private void BuildMap()
	{
		_mapsRoot = new Node3D { Name = "Maps" };
		AddChild(_mapsRoot);

		_actorsRoot = new Node3D { Name = "Actors" };
		AddChild(_actorsRoot);

		foreach (WildMapDefinition wildMap in WildMaps)
		{
			BuildWildMapScene(wildMap);
		}
		BuildCityMapScene();
		SetMapVisibility("city");
	}

	private void BuildWildMapScene(WildMapDefinition wildMap)
	{
		_obstaclePositions.Clear();
		_currentThemeMapId = wildMap.Id;
		_wildMapRoot = new Node3D { Name = wildMap.RootName };
		_mapsRoot.AddChild(_wildMapRoot);
		_wildMapRootsById[wildMap.Id] = _wildMapRoot;
		_mapRoot = _wildMapRoot;

		_propsRoot = new Node3D { Name = "WildProps" };
		_mapRoot.AddChild(_propsRoot);

		// Biome ground palette recolours the whole floor + terrain overlays so
		// each map reads as its ecosystem (e.g. snow = all white).
		_wildGroundPalette = BuildWildGroundPalette(wildMap.Id);
		BeginVegetationBatch(_propsRoot);
		CreateStaticBox(_mapRoot, "Ground", new Vector3(0.0f, -0.5f, 0.0f), new Vector3(MapSize, 1.0f, MapSize), _wildGroundPalette.Base);
		CreateBoundaries();
		CreateWildTerrainDressing();
		CreateLandmarks();
		CreateSpawnCamp();
		CreateRuinSite();
		CreateMonsterDen();
		CreateWildMapThemeDressing(wildMap.Id);
		CreateWildernessCaveEntrance(wildMap.Id);
		CreateMapPortal("ReturnToCityPortal", _wildSpawnPosition + new Vector3(0.0f, 0.0f, 5.0f), "city", "portal.return_city");
		ScatterProps();
		ScatterDetailProps();
		CreateWildScenicEdges();
		EndVegetationBatch();

		var obstacleCopy = new List<Vector3>(_obstaclePositions);
		_wildObstaclePositionsById[wildMap.Id] = obstacleCopy;
		if (wildMap.Id == "wild_forest")
		{
			_wildObstaclePositions.Clear();
			_wildObstaclePositions.AddRange(obstacleCopy);
		}
	}

	private void BuildCityMapScene()
	{
		_obstaclePositions.Clear();
		_currentThemeMapId = "city";
		_cityMapRoot = new Node3D { Name = "MainCityMap" };
		_mapsRoot.AddChild(_cityMapRoot);
		_mapRoot = _cityMapRoot;

		_propsRoot = new Node3D { Name = "CityProps" };
		_mapRoot.AddChild(_propsRoot);

		BeginVegetationBatch(_propsRoot);
		CreateStaticBox(_mapRoot, "CityGround", new Vector3(0.0f, -0.5f, 0.0f), new Vector3(MapSize, 1.0f, MapSize), _matGround);
		CreateBoundaries();
		CreateCityTerrainDressing();
		CreateMesh(_mapRoot, "CityMainRoadEdge", BoxMeshFor(new Vector3(10.8f, 0.075f, 48.0f)), _mainCityCenter + new Vector3(0.0f, 0.048f, 8.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "CityMainRoad", BoxMeshFor(new Vector3(8.6f, 0.08f, 46.0f)), _mainCityCenter + new Vector3(0.0f, 0.055f, 8.0f), _matCobblestone);
		CreateMesh(_mapRoot, "CityOuterPlazaEdge", CylinderMeshFor(11.4f, 11.4f, 0.09f), _citySpawnPosition + new Vector3(0.0f, 0.07f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "CityOuterPlaza", CylinderMeshFor(9.8f, 9.8f, 0.10f), _citySpawnPosition + new Vector3(0.0f, 0.085f, 0.0f), _matCobblestone);
		CreateMainCity();
		CreateCityScenicEdges();
		CreateMapPortal("WildMapGate", CityPortalPosition, "wild_select", "portal.travel_wild");
		EndVegetationBatch();

		_obstaclePositions.Clear();
		_obstaclePositions.AddRange(_wildObstaclePositions);
	}

	private void CreateWildTerrainDressing()
	{
		// Use the current biome's ground palette so overlays match the ecosystem
		// (snow stays white, badlands stays red, etc.) instead of forest greens.
		BiomeGroundPalette palette = _wildGroundPalette;
		CreateTerrainPatch("WildNorthMeadow", new Vector3(-28.0f, 0.0f, -46.0f), 17.0f, new Vector3(1.55f, 1.0f, 0.72f), -18.0f, palette.Meadow, 0.035f);
		CreateTerrainPatch("WildEastMeadow", new Vector3(42.0f, 0.0f, 7.0f), 20.0f, new Vector3(1.2f, 1.0f, 0.88f), 22.0f, palette.Meadow, 0.034f);
		CreateTerrainPatch("WildSouthField", new Vector3(25.0f, 0.0f, 50.0f), 16.0f, new Vector3(1.35f, 1.0f, 0.62f), -34.0f, palette.Field, 0.036f);
		CreateTerrainPatch("WildWestField", new Vector3(-50.0f, 0.0f, 3.0f), 15.0f, new Vector3(1.0f, 1.0f, 0.68f), 12.0f, palette.Field, 0.036f);

		CreateTerrainPatch("WildRiverBankA", new Vector3(-58.0f, 0.0f, -44.0f), 9.0f, new Vector3(1.85f, 1.0f, 0.42f), 34.0f, palette.Bank, 0.052f);
		CreateTerrainPatch("WildRiverBankB", new Vector3(-43.0f, 0.0f, -33.0f), 9.0f, new Vector3(1.9f, 1.0f, 0.44f), 34.0f, palette.Bank, 0.052f);
		CreateTerrainPatch("WildRiverBankC", new Vector3(-27.0f, 0.0f, -22.0f), 9.0f, new Vector3(1.8f, 1.0f, 0.43f), 34.0f, palette.Bank, 0.052f);
		CreateTerrainPatch("WildRiverA", new Vector3(-58.0f, 0.0f, -44.0f), 7.0f, new Vector3(1.76f, 1.0f, 0.30f), 34.0f, palette.Shallow, 0.068f);
		CreateTerrainPatch("WildRiverB", new Vector3(-43.0f, 0.0f, -33.0f), 7.0f, new Vector3(1.82f, 1.0f, 0.31f), 34.0f, palette.Water, 0.07f);
		CreateTerrainPatch("WildRiverC", new Vector3(-27.0f, 0.0f, -22.0f), 7.0f, new Vector3(1.72f, 1.0f, 0.30f), 34.0f, palette.Shallow, 0.068f);

		CreateTerrainPatch("WildCampClearing", _spawnCampCenter + new Vector3(0.0f, 0.0f, 6.0f), 16.0f, new Vector3(1.18f, 1.0f, 0.82f), 0.0f, palette.Path, 0.042f);
		CreateTerrainPatch("WildRuinOvergrowth", new Vector3(-45.0f, 0.0f, -34.0f), 12.0f, new Vector3(1.0f, 1.0f, 0.72f), -8.0f, palette.Meadow, 0.038f);
		CreateTerrainPatch("WildDenAsh", new Vector3(43.0f, 0.0f, 37.0f), 13.0f, new Vector3(1.05f, 1.0f, 0.78f), 12.0f, palette.Ash, 0.039f);

		for (int index = 0; index < 10; index++)
		{
			float x = -62.0f + index * 13.5f;
			float z = index % 2 == 0 ? -62.0f : 62.0f;
			CreateTerrainPatch($"WildTreeLinePatch{index}", new Vector3(x, 0.0f, z), 8.0f, new Vector3(1.4f, 1.0f, 0.5f), index * 17.0f, palette.Meadow, 0.033f);
		}
	}

	// Per-biome theme dressing lives in World.Biomes.cs (CreateWildMapThemeDressing).

	private void CreateCityTerrainDressing()
	{
		CreateTerrainPatch("CityDistrictGreenNorth", _mainCityCenter + new Vector3(0.0f, 0.0f, -8.0f), 34.0f, new Vector3(1.55f, 1.0f, 0.95f), 0.0f, _matMeadow, 0.033f);
		CreateTerrainPatch("CityDistrictGreenSouth", _mainCityCenter + new Vector3(0.0f, 0.0f, 22.0f), 29.0f, new Vector3(0.95f, 1.0f, 1.34f), 0.0f, _matMeadow, 0.034f);
		CreateTerrainPatch("CityWestField", _mainCityCenter + new Vector3(-43.0f, 0.0f, 18.0f), 16.0f, new Vector3(1.2f, 1.0f, 0.64f), 18.0f, _matField, 0.036f);
		CreateTerrainPatch("CityEastField", _mainCityCenter + new Vector3(43.0f, 0.0f, 14.0f), 16.0f, new Vector3(1.14f, 1.0f, 0.66f), -18.0f, _matField, 0.036f);
		CreateTerrainPatch("CityWaterBank", _mainCityCenter + new Vector3(-31.0f, 0.0f, 10.0f), 13.0f, new Vector3(1.35f, 1.0f, 0.65f), 20.0f, _matPondBank, 0.043f);
		CreateTerrainPatch("CityMillPond", _mainCityCenter + new Vector3(-31.0f, 0.0f, 10.0f), 10.0f, new Vector3(1.25f, 1.0f, 0.52f), 20.0f, _matWater, 0.06f);
	}

	private void CreateWildScenicEdges()
	{
		Vector3[] riverRocks =
		{
			new(-63.0f, 0.0f, -39.0f),
			new(-53.0f, 0.0f, -47.0f),
			new(-45.0f, 0.0f, -27.0f),
			new(-34.0f, 0.0f, -34.0f),
			new(-23.0f, 0.0f, -17.0f),
		};

		foreach (Vector3 position in riverRocks)
		{
			if (IsPositionClear(position, 2.8f))
			{
				CreateRock(position);
				_obstaclePositions.Add(position);
			}
		}

		for (int index = 0; index < 12; index++)
		{
			float angle = index / 12.0f * Mathf.Tau;
			Vector3 position = new(Mathf.Cos(angle) * 58.0f, 0.0f, Mathf.Sin(angle) * 58.0f);
			if (IsPositionClear(position, 4.0f))
			{
				// Biome-appropriate boundary trees (pines on snow, spires in the
				// badlands, …) instead of forcing green oaks onto every map.
				CreateBiomePrimaryProp(position);
				_obstaclePositions.Add(position);
			}
		}

		for (int index = 0; index < 18; index++)
		{
			Vector3 position = new(
				(float)_rng.RandfRange(-58.0f, 58.0f),
				0.0f,
				(float)_rng.RandfRange(-58.0f, 58.0f)
			);

			if (Mathf.Abs(position.X) < 10.0f || Mathf.Abs(position.Z) < 10.0f)
			{
				continue;
			}

			// Biome-appropriate ground detail (snow lumps/ice on snow, dry shrubs
			// in the badlands, grass/flowers in the forest, …).
			CreateBiomeDetailProp(position);
		}
	}

	private void CreateCityScenicEdges()
	{
		Vector3 center = _mainCityCenter;
		Vector3[] treePositions =
		{
			center + new Vector3(-36.0f, 0.0f, -12.0f),
			center + new Vector3(-38.0f, 0.0f, 2.0f),
			center + new Vector3(-34.0f, 0.0f, 23.0f),
			center + new Vector3(36.0f, 0.0f, -12.0f),
			center + new Vector3(38.0f, 0.0f, 2.0f),
			center + new Vector3(34.0f, 0.0f, 23.0f),
			center + new Vector3(-12.0f, 0.0f, 25.0f),
			center + new Vector3(12.0f, 0.0f, 25.0f),
		};

		foreach (Vector3 position in treePositions)
		{
			CreateTree(position);
			_obstaclePositions.Add(position);
		}

		for (int side = -1; side <= 1; side += 2)
		{
			for (int index = 0; index < 5; index++)
			{
				Vector3 flowerPosition = center + new Vector3(side * (26.0f + index * 2.3f), 0.0f, 12.0f + (index % 2) * 2.4f);
				CreateFlowerPatch(flowerPosition);
			}

			CreateCrateStack(center + new Vector3(side * 28.5f, 0.0f, -7.4f), side * -12.0f);
			CreateBanner(center + new Vector3(side * 27.5f, 0.0f, 2.5f), side * -18.0f, _matNpcAccent);
			CreateExternalProp($"CityOuterFence{side}A", "res://assets/models/environment/fence.glb", center + new Vector3(side * 31.0f, 0.0f, 13.0f), new Vector3(0.0f, 90.0f, 0.0f), new Vector3(1.25f, 1.25f, 1.25f), new Vector3(0.45f, 1.0f, 2.8f), new Vector3(0.0f, 0.5f, 0.0f));
			CreateExternalProp($"CityOuterFence{side}B", "res://assets/models/environment/fence.glb", center + new Vector3(side * 34.0f, 0.0f, 13.0f), new Vector3(0.0f, 90.0f, 0.0f), new Vector3(1.25f, 1.25f, 1.25f), new Vector3(0.45f, 1.0f, 2.8f), new Vector3(0.0f, 0.5f, 0.0f));
		}
	}

	private void CreateBoundaries()
	{
		float half = MapSize * 0.5f;
		const float wallHeight = 5.0f;
		const float wallThickness = 2.0f;

		CreateStaticBox(_mapRoot, "NorthWall", new Vector3(0.0f, wallHeight * 0.5f, -half), new Vector3(MapSize, wallHeight, wallThickness), _matWall);
		CreateStaticBox(_mapRoot, "SouthWall", new Vector3(0.0f, wallHeight * 0.5f, half), new Vector3(MapSize, wallHeight, wallThickness), _matWall);
		CreateStaticBox(_mapRoot, "WestWall", new Vector3(-half, wallHeight * 0.5f, 0.0f), new Vector3(wallThickness, wallHeight, MapSize), _matWall);
		CreateStaticBox(_mapRoot, "EastWall", new Vector3(half, wallHeight * 0.5f, 0.0f), new Vector3(wallThickness, wallHeight, MapSize), _matWall);
	}

	private void CreateLandmarks()
	{
		CreateMesh(_mapRoot, "MainPathNSEdge", BoxMeshFor(new Vector3(10.4f, 0.07f, MapSize - 12.0f)), new Vector3(0.0f, 0.038f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "MainPathNS", BoxMeshFor(new Vector3(7.4f, 0.08f, MapSize - 15.0f)), new Vector3(0.0f, 0.05f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "MainPathEWEdge", BoxMeshFor(new Vector3(MapSize - 12.0f, 0.07f, 10.4f)), new Vector3(0.0f, 0.04f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "MainPathEW", BoxMeshFor(new Vector3(MapSize - 15.0f, 0.08f, 7.4f)), new Vector3(0.0f, 0.055f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "SpawnPlazaEdge", CylinderMeshFor(13.6f, 13.6f, 0.09f), new Vector3(0.0f, 0.075f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "SpawnPlaza", CylinderMeshFor(11.6f, 11.6f, 0.12f), new Vector3(0.0f, 0.095f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "PondBank", CylinderMeshFor(18.0f, 18.0f, 0.08f), new Vector3(-34.0f, 0.10f, 28.0f), _matPondBank);
		CreateMesh(_mapRoot, "PondShallowRing", CylinderMeshFor(15.0f, 15.0f, 0.075f), new Vector3(-34.0f, 0.125f, 28.0f), _matShallowWater);
		CreateMesh(_mapRoot, "Pond", CylinderMeshFor(11.7f, 11.7f, 0.08f), new Vector3(-34.0f, 0.145f, 28.0f), _matWater);
		CreateStaticBox(_mapRoot, "WatchTowerBase", new Vector3(34.0f, 1.0f, -31.0f), new Vector3(7.0f, 2.0f, 7.0f), _matWall);

		Vector3 towerPosition = new(34.0f, 0.0f, -31.0f);
		CreateStaticBox(_mapRoot, "WatchTowerLevel", towerPosition + new Vector3(0.0f, 2.6f, 0.0f), new Vector3(5.0f, 0.8f, 5.0f), _matWall);
		CreateStaticBox(_mapRoot, "WatchTowerLevel", towerPosition + new Vector3(0.0f, 5.0f, 0.0f), new Vector3(5.0f, 0.8f, 5.0f), _matWall);

		_obstaclePositions.Add(new Vector3(-34.0f, 0.0f, 28.0f));
		_obstaclePositions.Add(new Vector3(34.0f, 0.0f, -31.0f));
	}

	private void CreateSpawnCamp()
	{
		CreateTent(new Vector3(-7.0f, 0.0f, 15.0f), 22.0f);
		CreateTent(new Vector3(7.0f, 0.0f, 15.5f), -24.0f);
		CreateCampfire(new Vector3(0.0f, 0.0f, 17.2f));
		CreateBanner(new Vector3(-10.0f, 0.0f, 8.8f), 10.0f, _matNpcAccent);
		CreateBanner(new Vector3(10.0f, 0.0f, 8.8f), -10.0f, _matNpcAccent);
		CreateCrateStack(new Vector3(-4.8f, 0.0f, 10.6f), 16.0f);
		CreateCrateStack(new Vector3(5.2f, 0.0f, 11.2f), -11.0f);
		CreateTorch(new Vector3(-12.0f, 0.0f, 12.2f));
		CreateTorch(new Vector3(12.0f, 0.0f, 12.2f));

		_obstaclePositions.Add(new Vector3(-7.0f, 0.0f, 15.0f));
		_obstaclePositions.Add(new Vector3(7.0f, 0.0f, 15.5f));
		_obstaclePositions.Add(new Vector3(0.0f, 0.0f, 17.2f));
	}

	private void CreateMainCity()
	{
		Vector3 center = _mainCityCenter;
		const float shopRadius = 31.0f;
		CreateMesh(_mapRoot, "MainCityOuterRingEdge", CylinderMeshFor(35.4f, 35.4f, 0.09f), center + new Vector3(0.0f, 0.075f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "MainCityOuterRingRoad", CylinderMeshFor(33.0f, 33.0f, 0.10f), center + new Vector3(0.0f, 0.095f, 0.0f), _matCobblestone);
		CreateMesh(_mapRoot, "MainCityInnerGardenEdge", CylinderMeshFor(22.8f, 22.8f, 0.11f), center + new Vector3(0.0f, 0.115f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "MainCityInnerGarden", CylinderMeshFor(20.7f, 20.7f, 0.12f), center + new Vector3(0.0f, 0.135f, 0.0f), _matMeadow);
		CreateMesh(_mapRoot, "MainCityInnerWalk", CylinderMeshFor(13.4f, 13.4f, 0.13f), center + new Vector3(0.0f, 0.155f, 0.0f), _matPath);
		CreateCityRoad("CityNorthSpoke", center + new Vector3(0.0f, 0.0f, -27.0f), new Vector2(12.4f, 18.0f));
		CreateCityRoad("CitySouthSpoke", center + new Vector3(0.0f, 0.0f, 29.0f), new Vector2(12.4f, 22.0f));
		CreateMesh(_mapRoot, "CityPortalPlazaEdge", CylinderMeshFor(9.4f, 9.4f, 0.09f), center + new Vector3(0.0f, 0.08f, -28.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "CityPortalPlaza", CylinderMeshFor(7.8f, 7.8f, 0.10f), center + new Vector3(0.0f, 0.105f, -28.0f), _matCobblestone);
		CreateBanner(center + new Vector3(-5.2f, 0.0f, -27.6f), 8.0f, _matCrystal);
		CreateBanner(center + new Vector3(5.2f, 0.0f, -27.6f), -8.0f, _matCrystal);
		CreateTorch(center + new Vector3(-7.1f, 0.0f, -24.4f));
		CreateTorch(center + new Vector3(7.1f, 0.0f, -24.4f));

		Vector3 itemShopOffset = RingOffset(54.0f, shopRadius);
		Vector3 blacksmithOffset = RingOffset(306.0f, shopRadius);
		Vector3 mercenaryOffset = RingOffset(126.0f, shopRadius);
		Vector3 petShopOffset = RingOffset(234.0f, shopRadius);
		Vector3 revivalOffset = RingOffset(0.0f, shopRadius);
		CreateItemShop(center + itemShopOffset, YawFacingCenter(itemShopOffset));
		CreateBlacksmithShop(center + blacksmithOffset, YawFacingCenter(blacksmithOffset));
		CreateMercenaryGuild(center + mercenaryOffset, YawFacingCenter(mercenaryOffset));
		CreatePetShop(center + petShopOffset, YawFacingCenter(petShopOffset));
		// This spot is now the warehouse (the warehouse keeper stands here);
		// revival moved to the pet merchant.
		CreateWarehouseBuilding(center + revivalOffset, YawFacingCenter(revivalOffset));

		for (int index = 0; index < 8; index++)
		{
			Vector3 offset = RingOffset(index * 45.0f + 22.5f, 23.5f);
			CreateTorch(center + offset);
		}

		for (int index = 0; index < 8; index++)
		{
			Vector3 offset = RingOffset(index * 45.0f, 29.0f);
			CreateExternalProp($"CityRingLantern{index}", "res://assets/models/environment/lantern.glb", center + offset, Vector3.Zero, new Vector3(1.18f, 1.18f, 1.18f), new Vector3(0.6f, 2.2f, 0.6f), new Vector3(0.0f, 1.1f, 0.0f));
		}

		CreateCityFountain(center);
		CreateCityMarket(center);
		CreateCityGardens(center);

		_obstaclePositions.Add(center);
	}

	private void CreateCityRoad(string name, Vector3 center, Vector2 size)
	{
		CreateMesh(_mapRoot, $"{name}Edge", BoxMeshFor(new Vector3(size.X + 2.0f, 0.075f, size.Y + 2.0f)), center + new Vector3(0.0f, 0.072f, 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, name, BoxMeshFor(new Vector3(size.X, 0.08f, size.Y)), center + new Vector3(0.0f, 0.09f, 0.0f), _matCobblestone);
	}

	private static Vector3 RingOffset(float degrees, float radius)
	{
		float radians = Mathf.DegToRad(degrees);
		return new Vector3(Mathf.Sin(radians) * radius, 0.0f, Mathf.Cos(radians) * radius);
	}

	private static float YawFacingCenter(Vector3 offsetFromCenter)
	{
		Vector2 direction = new(-offsetFromCenter.X, -offsetFromCenter.Z);
		if (direction.LengthSquared() <= 0.001f)
		{
			return 0.0f;
		}

		direction = direction.Normalized();
		return Mathf.RadToDeg(Mathf.Atan2(-direction.X, -direction.Y));
	}

	private static Vector3 RingFrontOffset(float degrees, float shopRadius, float frontDistance)
	{
		Vector3 offset = RingOffset(degrees, shopRadius);
		Vector3 inward = -offset.Normalized();
		return offset + inward * frontDistance;
	}

	private void CreateCityFountain(Vector3 position)
	{
		var fountain = new StaticBody3D
		{
			Name = "CityFountain",
			Position = position,
		};
		_propsRoot.AddChild(fountain);

		AddExternalModelTo(fountain, "res://assets/models/environment/fountain-round-detail.glb", "KenneyRoundBasin", Vector3.Zero, Vector3.Zero, new Vector3(2.55f, 2.55f, 2.55f));
		AddExternalModelTo(fountain, "res://assets/models/environment/fountain-center.glb", "KenneyCenterNozzle", new Vector3(0.0f, 0.28f, 0.0f), Vector3.Zero, new Vector3(2.25f, 2.25f, 2.25f));
		AddExternalModelTo(fountain, "res://assets/models/environment/fountain-square-detail.glb", "KenneyLowerPedestal", new Vector3(0.0f, -0.02f, 0.0f), new Vector3(0.0f, 45.0f, 0.0f), new Vector3(1.85f, 0.82f, 1.85f));

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.42f, 0.0f),
			Shape = new CylinderShape3D
			{
				Radius = 2.55f,
				Height = 0.85f,
			},
		};
		fountain.AddChild(collisionShape);

		CreateFountainWaterEffect(fountain);
	}

	private void CreateFountainWaterEffect(Node3D fountain)
	{
		var waterMaterial = MakeEmissiveMaterial(new Color(0.42f, 0.88f, 1.0f, 0.46f), 0.62f, 0.12f);
		var streamMaterial = MakeEmissiveMaterial(new Color(0.70f, 0.94f, 1.0f, 0.62f), 0.86f, 0.08f);
		var foamMaterial = MakeEmissiveMaterial(new Color(0.94f, 0.99f, 1.0f, 0.82f), 0.52f, 0.20f);
		var mistMaterial = MakeEmissiveMaterial(new Color(0.78f, 0.94f, 1.0f, 0.34f), 0.44f, 0.22f);
		Color[] lightColors =
		{
			new(1.0f, 0.20f, 0.18f),
			new(1.0f, 0.72f, 0.18f),
			new(0.22f, 0.92f, 0.36f),
			new(0.24f, 0.72f, 1.0f),
			new(0.74f, 0.42f, 1.0f),
			new(1.0f, 0.36f, 0.82f),
		};

		AddMesh(fountain, "OuterWaterSurface", CylinderMeshFor(2.08f, 2.08f, 0.035f), new Vector3(0.0f, 0.37f, 0.0f), Vector3.Zero, new Vector3(1.0f, 0.18f, 1.0f), waterMaterial);
		AddMesh(fountain, "InnerWaterSurface", CylinderMeshFor(0.78f, 0.78f, 0.032f), new Vector3(0.0f, 0.84f, 0.0f), Vector3.Zero, new Vector3(1.0f, 0.18f, 1.0f), waterMaterial);
		AddMesh(fountain, "OuterRippleRingA", new TorusMesh { InnerRadius = 0.018f, OuterRadius = 1.48f }, new Vector3(0.0f, 0.405f, 0.0f), Vector3.Zero, new Vector3(1.0f, 0.06f, 1.0f), foamMaterial);
		AddMesh(fountain, "OuterRippleRingB", new TorusMesh { InnerRadius = 0.014f, OuterRadius = 1.92f }, new Vector3(0.0f, 0.415f, 0.0f), Vector3.Zero, new Vector3(1.0f, 0.045f, 1.0f), mistMaterial);
		AddMesh(fountain, "MainPressureJet", CreateFountainVerticalJetMesh(0.065f, 2.75f, 18, 10), new Vector3(0.0f, 0.86f, 0.0f), Vector3.Zero, Vector3.One, streamMaterial);
		AddMesh(fountain, "UpperSpillFoam", CylinderMeshFor(0.88f, 0.88f, 0.025f), new Vector3(0.0f, 0.92f, 0.0f), Vector3.Zero, Vector3.One, foamMaterial);

		for (int index = 0; index < 18; index++)
		{
			float angle = index / 18.0f * Mathf.Tau;
			float yaw = Mathf.RadToDeg(angle);
			Vector3 direction = new(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
			AddMesh(
				fountain,
				$"OuterParabolicWaterJet{index}",
				CreateFountainArcMesh(0.50f, 1.78f, 1.05f, index % 2 == 0 ? 1.86f : 1.62f, 0.50f, 0.030f, 18, 8),
				Vector3.Zero,
				new Vector3(0.0f, yaw, 0.0f),
				Vector3.One,
				streamMaterial
			);
			AddMesh(fountain, $"OuterSplashFoam{index}", new SphereMesh { Radius = 0.105f, Height = 0.066f }, direction * 1.82f + new Vector3(0.0f, 0.51f, 0.0f), Vector3.Zero, new Vector3(1.70f, 0.26f, 1.05f), foamMaterial);
		}

		for (int index = 0; index < 12; index++)
		{
			float angle = index / 12.0f * Mathf.Tau + Mathf.Pi / 12.0f;
			float yaw = Mathf.RadToDeg(angle);
			Vector3 direction = new(Mathf.Sin(angle), 0.0f, Mathf.Cos(angle));
			AddMesh(
				fountain,
				$"InnerFineWaterJet{index}",
				CreateFountainArcMesh(0.22f, 0.92f, 1.22f, 1.72f, 0.88f, 0.018f, 14, 6),
				Vector3.Zero,
				new Vector3(0.0f, yaw, 0.0f),
				Vector3.One,
				streamMaterial
			);
			AddMesh(fountain, $"InnerSplashFoam{index}", new SphereMesh { Radius = 0.065f, Height = 0.040f }, direction * 0.95f + new Vector3(0.0f, 0.90f, 0.0f), Vector3.Zero, new Vector3(1.35f, 0.24f, 0.95f), foamMaterial);
		}

		for (int index = 0; index < 16; index++)
		{
			float angle = index / 16.0f * Mathf.Tau;
			Vector3 position = new(Mathf.Sin(angle) * 2.12f, 0.48f, Mathf.Cos(angle) * 2.12f);
			AddMesh(fountain, $"FountainColorLens{index}", new SphereMesh { Radius = 0.055f, Height = 0.04f }, position, Vector3.Zero, new Vector3(1.0f, 0.30f, 1.0f), MakeEmissiveMaterial(lightColors[index % lightColors.Length], 0.82f, 0.16f));
			if (index % 4 == 0)
			{
				var light = new OmniLight3D
				{
					Name = $"FountainColorLight{index}",
					Position = position + new Vector3(0.0f, 0.35f, 0.0f),
					LightColor = lightColors[index % lightColors.Length],
					LightEnergy = 0.32f,
					OmniRange = 3.4f,
				};
				fountain.AddChild(light);
			}
		}

		AddFountainMistParticles(fountain, mistMaterial, new Vector3(0.0f, 2.65f, 0.0f), 260, 1.45, 1.7f, 4.4f, 0.16f, 48.0f);
		AddFountainMistParticles(fountain, foamMaterial, new Vector3(0.0f, 0.56f, 0.0f), 160, 1.05, 0.45f, 1.35f, 1.95f, 82.0f);
	}

	private static ArrayMesh CreateFountainArcMesh(float startRadius, float endRadius, float startY, float apexY, float endY, float radius, int segments, int radialSegments)
	{
		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var indices = new List<int>();

		for (int segment = 0; segment <= segments; segment++)
		{
			float t = segment / (float)segments;
			float z = Mathf.Lerp(startRadius, endRadius, t);
			float baseY = Mathf.Lerp(startY, endY, t);
			float arcY = baseY + (apexY - Mathf.Lerp(startY, endY, 0.5f)) * Mathf.Sin(t * Mathf.Pi);
			Vector3 center = new(0.0f, arcY, z);

			float nextT = Mathf.Clamp(t + 0.01f, 0.0f, 1.0f);
			float nextZ = Mathf.Lerp(startRadius, endRadius, nextT);
			float nextBaseY = Mathf.Lerp(startY, endY, nextT);
			float nextArcY = nextBaseY + (apexY - Mathf.Lerp(startY, endY, 0.5f)) * Mathf.Sin(nextT * Mathf.Pi);
			Vector3 tangent = (new Vector3(0.0f, nextArcY, nextZ) - center).Normalized();
			Vector3 side = Vector3.Right;
			Vector3 up = tangent.Cross(side).Normalized();

			for (int ring = 0; ring < radialSegments; ring++)
			{
				float angle = ring / (float)radialSegments * Mathf.Tau;
				Vector3 normal = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)).Normalized();
				vertices.Add(center + normal * radius);
				normals.Add(normal);
			}
		}

		for (int segment = 0; segment < segments; segment++)
		{
			int current = segment * radialSegments;
			int next = (segment + 1) * radialSegments;
			for (int ring = 0; ring < radialSegments; ring++)
			{
				int ringNext = (ring + 1) % radialSegments;
				indices.Add(current + ring);
				indices.Add(next + ring);
				indices.Add(next + ringNext);
				indices.Add(current + ring);
				indices.Add(next + ringNext);
				indices.Add(current + ringNext);
			}
		}

		return BuildArrayMesh(vertices, normals, indices);
	}

	private static ArrayMesh CreateFountainVerticalJetMesh(float radius, float height, int segments, int radialSegments)
	{
		var vertices = new List<Vector3>();
		var normals = new List<Vector3>();
		var indices = new List<int>();

		for (int segment = 0; segment <= segments; segment++)
		{
			float t = segment / (float)segments;
			float y = t * height;
			float pulse = 0.82f + 0.18f * Mathf.Sin(t * Mathf.Pi * 5.0f);
			float ringRadius = radius * Mathf.Lerp(1.08f, 0.52f, t) * pulse;
			for (int ring = 0; ring < radialSegments; ring++)
			{
				float angle = ring / (float)radialSegments * Mathf.Tau;
				Vector3 normal = new(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle));
				vertices.Add(new Vector3(normal.X * ringRadius, y, normal.Z * ringRadius));
				normals.Add(normal);
			}
		}

		for (int segment = 0; segment < segments; segment++)
		{
			int current = segment * radialSegments;
			int next = (segment + 1) * radialSegments;
			for (int ring = 0; ring < radialSegments; ring++)
			{
				int ringNext = (ring + 1) % radialSegments;
				indices.Add(current + ring);
				indices.Add(next + ring);
				indices.Add(next + ringNext);
				indices.Add(current + ring);
				indices.Add(next + ringNext);
				indices.Add(current + ringNext);
			}
		}

		return BuildArrayMesh(vertices, normals, indices);
	}

	private static ArrayMesh BuildArrayMesh(List<Vector3> vertices, List<Vector3> normals, List<int> indices)
	{
		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
		arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
		arrays[(int)Mesh.ArrayType.Index] = indices.ToArray();

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}


	private void AddFountainMistParticles(Node3D fountain, Material waterMaterial, Vector3 position, int amount, double lifetime, float minVelocity, float maxVelocity, float emissionRadius, float spread)
	{
		var dropletMesh = new SphereMesh
		{
			Radius = 0.045f,
			Height = 0.055f,
			Material = waterMaterial,
		};
		var particles = new GpuParticles3D
		{
			Name = "FountainSprayParticles",
			Amount = amount,
			Lifetime = lifetime,
			Preprocess = lifetime,
			Emitting = true,
			Position = position,
			DrawPass1 = dropletMesh,
		};
		var process = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = emissionRadius,
			Direction = Vector3.Up,
			Spread = spread,
			InitialVelocityMin = minVelocity,
			InitialVelocityMax = maxVelocity,
			Gravity = new Vector3(0.0f, -5.2f, 0.0f),
			ScaleMin = 0.45f,
			ScaleMax = 1.15f,
		};
		particles.ProcessMaterial = process;
		fountain.AddChild(particles);
	}

	private static Vector3 LocalOffset(float yawDegrees, Vector3 offset)
	{
		return new Basis(Vector3.Up, Mathf.DegToRad(yawDegrees)) * offset;
	}

	private void CreateBlacksmithShop(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityBlacksmithShop",
			position,
			yawDegrees,
			new Vector3(6.8f, 3.1f, 6.0f),
			_matWood,
			_matActorDark,
			"shop.blacksmith",
			new Color(1.0f, 0.58f, 0.28f),
			false
		);

		AddMesh(shop, "ToolRack", BoxMeshFor(new Vector3(1.8f, 0.12f, 0.08f)), new Vector3(0.0f, 1.8f, -3.08f), Vector3.Zero, Vector3.One, _matMetal);
		AddMesh(shop, "HammerA", BoxMeshFor(new Vector3(0.14f, 0.78f, 0.08f)), new Vector3(-0.55f, 1.45f, -3.14f), new Vector3(0.0f, 0.0f, 16.0f), Vector3.One, _matMetal);
		AddMesh(shop, "HammerB", BoxMeshFor(new Vector3(0.14f, 0.70f, 0.08f)), new Vector3(0.15f, 1.45f, -3.14f), new Vector3(0.0f, 0.0f, -16.0f), Vector3.One, _matMetal);
		AddMesh(shop, "MetalSignAnvil", BoxMeshFor(new Vector3(0.72f, 0.18f, 0.10f)), new Vector3(0.0f, 2.25f, -3.18f), Vector3.Zero, Vector3.One, _matMetal);
		AddMesh(shop, "MetalSignHornLeft", BoxMeshFor(new Vector3(0.26f, 0.10f, 0.10f)), new Vector3(-0.42f, 2.33f, -3.20f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, _matMetal);
		AddMesh(shop, "MetalSignHornRight", BoxMeshFor(new Vector3(0.26f, 0.10f, 0.10f)), new Vector3(0.42f, 2.33f, -3.20f), new Vector3(0.0f, 0.0f, 18.0f), Vector3.One, _matMetal);
		AddMesh(shop, "ForgeWallGlow", BoxMeshFor(new Vector3(1.12f, 0.30f, 0.055f)), new Vector3(0.0f, 1.04f, -3.18f), Vector3.Zero, Vector3.One, _matTorchFire);
		CreateExternalProp("BlacksmithSideChimney", "res://assets/models/environment/chimney.glb", position + LocalOffset(yawDegrees, new Vector3(-2.25f, 0.0f, 0.55f)), new Vector3(0.0f, yawDegrees, 0.0f), new Vector3(1.7f, 1.7f, 1.7f), new Vector3(0.55f, 1.8f, 0.55f), new Vector3(0.0f, 0.9f, 0.0f));
	}

	private void CreateItemShop(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityItemShop",
			position,
			yawDegrees,
			new Vector3(7.4f, 3.2f, 6.2f),
			_matWall,
			_matNpcAccent,
			"shop.item",
			new Color(1.0f, 0.86f, 0.38f),
			false
		);

		AddMesh(shop, "ShelfBack", BoxMeshFor(new Vector3(3.6f, 1.8f, 0.24f)), new Vector3(0.0f, 1.45f, 3.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "ShelfLineA", BoxMeshFor(new Vector3(3.8f, 0.10f, 0.28f)), new Vector3(0.0f, 1.18f, 2.84f), Vector3.Zero, Vector3.One, _matNpcAccent);
		AddMesh(shop, "ShelfLineB", BoxMeshFor(new Vector3(3.8f, 0.10f, 0.28f)), new Vector3(0.0f, 1.78f, 2.84f), Vector3.Zero, Vector3.One, _matNpcAccent);
		AddMesh(shop, "PotionBlue", new SphereMesh { Radius = 0.18f, Height = 0.26f }, new Vector3(-1.1f, 1.38f, 2.66f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(shop, "PotionGold", new SphereMesh { Radius = 0.17f, Height = 0.25f }, new Vector3(0.0f, 1.38f, 2.66f), Vector3.Zero, Vector3.One, _matNpcAccent);
		AddMesh(shop, "PotionRed", new SphereMesh { Radius = 0.17f, Height = 0.25f }, new Vector3(1.1f, 1.38f, 2.66f), Vector3.Zero, Vector3.One, _matTorchFire);
		AddMesh(shop, "PotionSignBoard", BoxMeshFor(new Vector3(1.36f, 0.42f, 0.08f)), new Vector3(0.0f, 2.23f, -3.28f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "PotionSignBlue", new SphereMesh { Radius = 0.11f, Height = 0.16f }, new Vector3(-0.34f, 2.23f, -3.34f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(shop, "PotionSignGold", new SphereMesh { Radius = 0.11f, Height = 0.16f }, new Vector3(0.0f, 2.23f, -3.34f), Vector3.Zero, Vector3.One, _matNpcAccent);
		AddMesh(shop, "PotionSignRed", new SphereMesh { Radius = 0.11f, Height = 0.16f }, new Vector3(0.34f, 2.23f, -3.34f), Vector3.Zero, Vector3.One, _matTorchFire);
	}

	private void CreatePetShop(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityPetShop",
			position,
			yawDegrees,
			new Vector3(8.2f, 2.9f, 5.8f),
			_matWall,
			_matCrystal,
			"shop.pet",
			new Color(0.64f, 1.0f, 0.82f)
		);

		AddMesh(shop, "PawPad", new SphereMesh { Radius = 0.32f, Height = 0.16f }, new Vector3(0.0f, 2.05f, -3.08f), Vector3.Zero, new Vector3(1.25f, 0.28f, 0.7f), _matNpcAccent);
		for (int index = 0; index < 4; index++)
		{
			float x = index < 2 ? -0.34f : 0.34f;
			float y = index % 2 == 0 ? 2.36f : 2.26f;
			AddMesh(shop, $"PawToe{index}", new SphereMesh { Radius = 0.12f, Height = 0.10f }, new Vector3(x, y, -3.10f), Vector3.Zero, new Vector3(1.0f, 0.3f, 0.7f), _matNpcAccent);
		}

		AddMesh(shop, "PetStableLeft", BoxMeshFor(new Vector3(1.05f, 0.72f, 1.1f)), new Vector3(-2.65f, 0.46f, -3.35f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "PetStableRight", BoxMeshFor(new Vector3(1.05f, 0.72f, 1.1f)), new Vector3(2.65f, 0.46f, -3.35f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "CareCrystalLeft", new SphereMesh { Radius = 0.22f, Height = 0.32f }, new Vector3(-2.65f, 1.0f, -3.35f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(shop, "CareCrystalRight", new SphereMesh { Radius = 0.22f, Height = 0.32f }, new Vector3(2.65f, 1.0f, -3.35f), Vector3.Zero, Vector3.One, _matCrystal);
		AddExternalModelTo(shop, "res://assets/models/pets/cube_pets/animal-dog.glb", "DisplayDog", new Vector3(-2.65f, 0.92f, -3.35f), new Vector3(0.0f, 180.0f, 0.0f), new Vector3(0.62f, 0.62f, 0.62f));
		AddExternalModelTo(shop, "res://assets/models/pets/cube_pets/animal-cat.glb", "DisplayCat", new Vector3(2.65f, 0.92f, -3.35f), new Vector3(0.0f, 180.0f, 0.0f), new Vector3(0.62f, 0.62f, 0.62f));
		AddExternalModelTo(shop, "res://assets/models/pets/cube_pets/animal-bunny.glb", "DisplayBunny", new Vector3(0.0f, 0.50f, -3.62f), new Vector3(0.0f, 180.0f, 0.0f), new Vector3(0.48f, 0.48f, 0.48f));
		AddMesh(shop, "ClinicCrossVertical", BoxMeshFor(new Vector3(0.18f, 0.72f, 0.06f)), new Vector3(0.0f, 1.68f, -3.06f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(shop, "ClinicCrossHorizontal", BoxMeshFor(new Vector3(0.58f, 0.18f, 0.065f)), new Vector3(0.0f, 1.68f, -3.08f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(shop, "PetShopRibbonLeft", BoxMeshFor(new Vector3(0.10f, 0.70f, 0.055f)), new Vector3(-1.55f, 2.16f, -3.14f), new Vector3(0.0f, 0.0f, -14.0f), Vector3.One, _matCrystal);
		AddMesh(shop, "PetShopRibbonRight", BoxMeshFor(new Vector3(0.10f, 0.70f, 0.055f)), new Vector3(1.55f, 2.16f, -3.14f), new Vector3(0.0f, 0.0f, 14.0f), Vector3.One, _matCrystal);
	}

	private void CreateWarehouseBuilding(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityWarehouse",
			position,
			yawDegrees,
			new Vector3(7.4f, 3.0f, 5.8f),
			_matWall,
			_matCrystal,
			"shop.warehouse",
			new Color(0.72f, 0.9f, 1.0f)
		);

		// Storage crates instead of a revival altar.
		AddMesh(shop, "WarehouseCrateA", BoxMeshFor(new Vector3(0.9f, 0.9f, 0.9f)), new Vector3(-0.9f, 0.45f, -3.3f), new Vector3(0.0f, 14.0f, 0.0f), Vector3.One, _matWood);
		AddMesh(shop, "WarehouseCrateB", BoxMeshFor(new Vector3(0.8f, 0.8f, 0.8f)), new Vector3(0.5f, 0.4f, -3.4f), new Vector3(0.0f, -22.0f, 0.0f), Vector3.One, _matWood);
		AddMesh(shop, "WarehouseCrateC", BoxMeshFor(new Vector3(0.7f, 0.7f, 0.7f)), new Vector3(0.2f, 1.15f, -3.35f), new Vector3(0.0f, 8.0f, 0.0f), Vector3.One, _matWood);
	}

	private void CreateMercenaryGuild(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityMercenaryGuild",
			position,
			yawDegrees,
			new Vector3(8.4f, 3.25f, 6.4f),
			_matWood,
			_matLeather,
			"shop.mercenary_guild",
			new Color(1.0f, 0.78f, 0.42f)
		);

		AddMesh(shop, "ContractBoard", BoxMeshFor(new Vector3(3.7f, 1.45f, 0.12f)), new Vector3(0.0f, 1.42f, -3.58f), Vector3.Zero, Vector3.One, _matWall);
		for (int index = 0; index < 5; index++)
		{
			float x = -1.35f + index * 0.68f;
			AddMesh(shop, $"ContractPaper{index}", BoxMeshFor(new Vector3(0.42f, 0.56f, 0.035f)), new Vector3(x, 1.46f + (index % 2) * 0.12f, -3.66f), new Vector3(0.0f, 0.0f, index % 2 == 0 ? -3.0f : 4.0f), Vector3.One, _matSkin);
		}

		AddMesh(shop, "GuildSpearLeft", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.85f }, new Vector3(-2.9f, 1.15f, -3.42f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, _matWood);
		AddMesh(shop, "GuildSpearRight", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.85f }, new Vector3(2.9f, 1.15f, -3.42f), new Vector3(0.0f, 0.0f, 18.0f), Vector3.One, _matWood);
		AddMesh(shop, "GuildSpearTipLeft", CylinderMeshFor(0.0f, 0.10f, 0.30f), new Vector3(-3.15f, 1.98f, -3.42f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, _matMetal);
		AddMesh(shop, "GuildSpearTipRight", CylinderMeshFor(0.0f, 0.10f, 0.30f), new Vector3(3.15f, 1.98f, -3.42f), new Vector3(0.0f, 0.0f, 18.0f), Vector3.One, _matMetal);
		AddMesh(shop, "GuildShield", new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.10f }, new Vector3(0.0f, 2.02f, -3.64f), new Vector3(90.0f, 0.0f, 0.0f), new Vector3(0.85f, 1.0f, 1.14f), _matNpcAccent);
	}

	private StaticBody3D CreateCityShopShell(string name, Vector3 position, float yawDegrees, Vector3 size, Material wallMaterial, Material roofMaterial, string signKey, Color signColor, bool includeFrontStep = true)
	{
		var shop = new StaticBody3D
		{
			Name = name,
			Position = position,
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
		};
		_propsRoot.AddChild(shop);

		AddMesh(shop, "Body", BoxMeshFor(size), new Vector3(0.0f, size.Y * 0.5f, 0.0f), Vector3.Zero, Vector3.One, wallMaterial);
		AddExternalModelTo(shop, "res://assets/models/environment/wall-door.glb", "DoorModule", new Vector3(0.0f, 0.0f, -size.Z * 0.53f), Vector3.Zero, new Vector3(1.65f, 1.65f, 1.65f));
		AddExternalModelTo(shop, "res://assets/models/environment/wall-window-shutters.glb", "LeftWindowModule", new Vector3(-size.X * 0.32f, 0.0f, -size.Z * 0.535f), Vector3.Zero, new Vector3(1.25f, 1.25f, 1.25f));
		AddExternalModelTo(shop, "res://assets/models/environment/wall-window-shutters.glb", "RightWindowModule", new Vector3(size.X * 0.32f, 0.0f, -size.Z * 0.535f), Vector3.Zero, new Vector3(1.25f, 1.25f, 1.25f));
		AddSymmetricShopRoof(shop, size, roofMaterial);
		AddMesh(shop, "Awning", BoxMeshFor(new Vector3(size.X * 0.92f, 0.18f, 1.25f)), new Vector3(0.0f, 2.18f, -size.Z * 0.64f), new Vector3(-8.0f, 0.0f, 0.0f), Vector3.One, roofMaterial);
		float signWidth = Mathf.Clamp(size.X * 0.62f, 4.0f, 5.4f);
		AddMesh(shop, "SignBoard", BoxMeshFor(new Vector3(signWidth, 1.02f, 0.16f)), new Vector3(0.0f, 2.72f, -size.Z * 0.71f), Vector3.Zero, Vector3.One, _matWood);

		var sign = new Label3D
		{
			Name = "ShopSignLabel",
			Text = LocaleText.T(signKey),
			Position = new Vector3(0.0f, 2.73f, -size.Z * 0.745f),
			RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
			FontSize = 42,
			PixelSize = 0.012f,
			OutlineSize = 8,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 430.0f,
		};
		sign.OutlineModulate = new Color(0.04f, 0.025f, 0.018f, 0.95f);
		sign.Modulate = signColor;
		shop.AddChild(sign);

		if (includeFrontStep)
		{
			AddMesh(shop, "FrontStep", BoxMeshFor(new Vector3(2.6f, 0.22f, 1.0f)), new Vector3(0.0f, 0.11f, -size.Z * 0.72f), Vector3.Zero, Vector3.One, _matCobblestone);
		}
		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, size.Y * 0.5f, 0.0f),
			Shape = new BoxShape3D { Size = size },
		};
		shop.AddChild(collisionShape);
		_obstaclePositions.Add(position);
		return shop;
	}

	private void AddSymmetricShopRoof(Node3D shop, Vector3 size, Material roofMaterial)
	{
		float roofWidth = size.X + 0.82f;
		float roofDepth = size.Z + 0.92f;
		float halfDepth = roofDepth * 0.5f;
		float rise = Mathf.Clamp(size.Z * 0.22f, 1.18f, 1.58f);
		float slopeLength = Mathf.Sqrt(halfDepth * halfDepth + rise * rise);
		float angleDegrees = Mathf.RadToDeg(Mathf.Atan2(rise, halfDepth));
		float baseY = size.Y + 0.13f;

		AddMesh(shop, "RoofBaseTrim", BoxMeshFor(new Vector3(roofWidth + 0.20f, 0.16f, roofDepth + 0.22f)), new Vector3(0.0f, baseY, 0.0f), Vector3.Zero, Vector3.One, roofMaterial);
		AddMesh(shop, "RoofFrontSlope", BoxMeshFor(new Vector3(roofWidth, 0.16f, slopeLength)), new Vector3(0.0f, baseY + rise * 0.5f, -halfDepth * 0.5f), new Vector3(-angleDegrees, 0.0f, 0.0f), Vector3.One, roofMaterial);
		AddMesh(shop, "RoofBackSlope", BoxMeshFor(new Vector3(roofWidth, 0.16f, slopeLength)), new Vector3(0.0f, baseY + rise * 0.5f, halfDepth * 0.5f), new Vector3(angleDegrees, 0.0f, 0.0f), Vector3.One, roofMaterial);
		AddMesh(shop, "RoofRidgeBeam", BoxMeshFor(new Vector3(roofWidth + 0.22f, 0.18f, 0.22f)), new Vector3(0.0f, baseY + rise, 0.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "RoofFrontEave", BoxMeshFor(new Vector3(roofWidth + 0.18f, 0.20f, 0.18f)), new Vector3(0.0f, baseY + 0.08f, -halfDepth), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "RoofBackEave", BoxMeshFor(new Vector3(roofWidth + 0.18f, 0.20f, 0.18f)), new Vector3(0.0f, baseY + 0.08f, halfDepth), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "RoofLeftGableTrim", BoxMeshFor(new Vector3(0.16f, 0.18f, roofDepth + 0.18f)), new Vector3(-roofWidth * 0.5f, baseY + 0.18f, 0.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(shop, "RoofRightGableTrim", BoxMeshFor(new Vector3(0.16f, 0.18f, roofDepth + 0.18f)), new Vector3(roofWidth * 0.5f, baseY + 0.18f, 0.0f), Vector3.Zero, Vector3.One, _matWood);
	}

	private void CreateCityMarket(Vector3 center)
	{
		CreateExternalProp("CityMarketStallLeft", "res://assets/models/environment/stall-red.glb", center + new Vector3(-16.0f, 0.0f, 18.0f), new Vector3(0.0f, 90.0f, 0.0f), new Vector3(1.35f, 1.35f, 1.35f), new Vector3(2.8f, 1.8f, 2.0f), new Vector3(0.0f, 0.9f, 0.0f));
		CreateExternalProp("CityMarketStallRight", "res://assets/models/environment/stall-green.glb", center + new Vector3(16.0f, 0.0f, 18.0f), new Vector3(0.0f, -90.0f, 0.0f), new Vector3(1.35f, 1.35f, 1.35f), new Vector3(2.8f, 1.8f, 2.0f), new Vector3(0.0f, 0.9f, 0.0f));
		CreateExternalProp("CityCartWest", "res://assets/models/environment/cart.glb", center + new Vector3(-22.8f, 0.0f, 18.0f), new Vector3(0.0f, 90.0f, 0.0f), new Vector3(1.3f, 1.3f, 1.3f), new Vector3(1.9f, 1.2f, 2.8f), new Vector3(0.0f, 0.6f, 0.0f));
		CreateExternalProp("CityCartEast", "res://assets/models/environment/cart-high.glb", center + new Vector3(22.8f, 0.0f, 18.0f), new Vector3(0.0f, -90.0f, 0.0f), new Vector3(1.2f, 1.2f, 1.2f), new Vector3(1.9f, 1.5f, 2.8f), new Vector3(0.0f, 0.75f, 0.0f));
		CreateCrateStack(center + new Vector3(-12.5f, 0.0f, 20.8f), 0.0f);
		CreateCrateStack(center + new Vector3(12.5f, 0.0f, 20.8f), 0.0f);
		_obstaclePositions.Add(center + new Vector3(-16.0f, 0.0f, 18.0f));
		_obstaclePositions.Add(center + new Vector3(16.0f, 0.0f, 18.0f));
	}

	private void CreateCityGardens(Vector3 center)
	{
		for (int side = -1; side <= 1; side += 2)
		{
			CreateExternalProp($"CityFenceGate{side}", "res://assets/models/environment/fence-gate.glb", center + new Vector3(side * 18.0f, 0.0f, 2.2f), new Vector3(0.0f, 90.0f, 0.0f), new Vector3(1.25f, 1.25f, 1.25f), new Vector3(0.8f, 1.2f, 2.4f), new Vector3(0.0f, 0.6f, 0.0f));
			CreateFlowerPatch(center + new Vector3(side * 15.5f, 0.0f, 8.8f));
			CreateFlowerPatch(center + new Vector3(side * 18.0f, 0.0f, 9.4f));
		}
	}

	private void CreateRevivalNpc(Vector3 position, float yawDegrees)
	{
		var npc = new StaticBody3D
		{
			Name = "PetRevivalNpc",
			Position = position,
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
		};
		npc.AddToGroup("revival_npc");
		_propsRoot.AddChild(npc);

		AddMesh(npc, "Robe", new CapsuleMesh { Radius = 0.30f, Height = 1.12f }, new Vector3(0.0f, 0.92f, 0.0f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.78f), _matNpc);
		AddMesh(npc, "Head", new SphereMesh { Radius = 0.24f, Height = 0.48f }, new Vector3(0.0f, 1.62f, 0.0f), Vector3.Zero, Vector3.One, _matSkin);
		AddMesh(npc, "HealerHat", CylinderMeshFor(0.18f, 0.30f, 0.22f), new Vector3(0.0f, 1.88f, 0.0f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(npc, "Staff", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.7f }, new Vector3(0.46f, 0.92f, -0.02f), new Vector3(0.0f, 0.0f, -7.0f), Vector3.One, _matWood);
		AddMesh(npc, "StaffOrb", new SphereMesh { Radius = 0.14f, Height = 0.22f }, new Vector3(0.58f, 1.74f, -0.02f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(npc, "AuraRing", CylinderMeshFor(0.92f, 0.92f, 0.035f), new Vector3(0.0f, 0.06f, 0.0f), Vector3.Zero, Vector3.One, _matCrystal);

		var label = new Label3D
		{
			Name = "RevivalNpcLabel",
			Text = LocaleText.T("npc.revival.name"),
			Position = new Vector3(0.0f, 2.35f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = 22,
			PixelSize = 0.008f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 260.0f,
		};
		label.OutlineModulate = new Color(0.02f, 0.03f, 0.025f, 0.95f);
		label.Modulate = new Color(0.64f, 1.0f, 0.82f);
		npc.AddChild(label);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.9f, 0.0f),
			Shape = new CapsuleShape3D { Radius = 0.42f, Height = 1.8f },
		};
		npc.AddChild(collisionShape);
	}

	private void CreateRuinSite()
	{
		Vector3 center = new(-45.0f, 0.0f, -34.0f);
		CreateMesh(_mapRoot, "RuinFloor", CylinderMeshFor(8.0f, 8.0f, 0.10f), center + new Vector3(0.0f, 0.09f, 0.0f), _matWall);
		CreateStaticBox(_propsRoot, "RuinPillar", center + new Vector3(-3.6f, 1.55f, -1.2f), new Vector3(1.0f, 3.1f, 1.0f), _matWall);
		CreateStaticBox(_propsRoot, "RuinPillar", center + new Vector3(3.6f, 1.15f, -1.1f), new Vector3(1.0f, 2.3f, 1.0f), _matWall);
		CreateStaticBox(_propsRoot, "RuinLintel", center + new Vector3(0.0f, 3.28f, -1.15f), new Vector3(8.2f, 0.75f, 1.05f), _matWall);
		CreateStaticBox(_propsRoot, "BrokenWall", center + new Vector3(-5.0f, 0.7f, 4.0f), new Vector3(4.0f, 1.4f, 0.7f), _matWall);
		CreateStaticBox(_propsRoot, "BrokenWall", center + new Vector3(4.8f, 0.5f, 4.2f), new Vector3(3.2f, 1.0f, 0.7f), _matWall);
		CreateCrystalCluster(center + new Vector3(0.0f, 0.0f, 2.8f), 1.25f, _matRune);
		CreateTorch(center + new Vector3(-6.5f, 0.0f, -3.0f));
		CreateTorch(center + new Vector3(6.5f, 0.0f, -3.0f));

		_obstaclePositions.Add(center);
	}

	private void CreateMonsterDen()
	{
		Vector3 center = new(43.0f, 0.0f, 37.0f);
		CreateMesh(_mapRoot, "DenGround", CylinderMeshFor(9.5f, 9.5f, 0.10f), center + new Vector3(0.0f, 0.08f, 0.0f), _matNest);
		CreateStaticBox(_propsRoot, "DenBackRock", center + new Vector3(0.0f, 1.7f, 4.2f), new Vector3(8.8f, 3.4f, 1.4f), _matRock);
		CreateStaticBox(_propsRoot, "DenLeftRock", center + new Vector3(-4.4f, 1.25f, 1.4f), new Vector3(1.3f, 2.5f, 5.8f), _matRock);
		CreateStaticBox(_propsRoot, "DenRightRock", center + new Vector3(4.4f, 1.25f, 1.4f), new Vector3(1.3f, 2.5f, 5.8f), _matRock);
		AddMesh(_propsRoot, "DenMouth", new SphereMesh { Radius = 3.0f, Height = 4.2f }, center + new Vector3(0.0f, 1.15f, 2.1f), Vector3.Zero, new Vector3(1.2f, 0.58f, 0.42f), _matActorDark);
		CreateCrystalCluster(center + new Vector3(-6.3f, 0.0f, -2.4f), 0.95f, _matMonsterClaw);
		CreateCrystalCluster(center + new Vector3(6.1f, 0.0f, -2.1f), 0.95f, _matMonsterClaw);
		CreateNestBones(center + new Vector3(0.0f, 0.0f, -4.8f));

		_obstaclePositions.Add(center);
	}

	private void ScatterProps()
	{
		float half = MapSize * 0.5f - 8.0f;
		int created = 0;
		int attempts = 0;

		while (created < PropCount && attempts < PropCount * 12)
		{
			attempts++;
			var position = new Vector3(
				(float)_rng.RandfRange(-half, half),
				0.0f,
				(float)_rng.RandfRange(-half, half)
			);

			if (position.DistanceTo(Vector3.Zero) < 13.0f || Mathf.Abs(position.X) < 5.0f || Mathf.Abs(position.Z) < 5.0f)
			{
				continue;
			}

			if (!IsPositionClear(position, 3.8f))
			{
				continue;
			}

			CreateBiomePrimaryProp(position);
			_obstaclePositions.Add(position);
			created++;
		}
	}

	private void ScatterDetailProps()
	{
		float half = MapSize * 0.5f - 7.0f;
		for (int index = 0; index < 220; index++)
		{
			var position = new Vector3(
				(float)_rng.RandfRange(-half, half),
				0.0f,
				(float)_rng.RandfRange(-half, half)
			);

			if (position.DistanceTo(Vector3.Zero) < 8.0f || Mathf.Abs(position.X) < 4.6f || Mathf.Abs(position.Z) < 4.6f)
			{
				continue;
			}

			CreateBiomeDetailProp(position);
		}
	}

	private void CreateTent(Vector3 position, float yawDegrees)
	{
		var tent = new StaticBody3D
		{
			Name = "Tent",
			Position = position,
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
		};
		_propsRoot.AddChild(tent);

		AddMesh(tent, "TentCanopy", CylinderMeshFor(0.0f, 1.85f, 1.9f), new Vector3(0.0f, 0.95f, 0.0f), Vector3.Zero, Vector3.One, _matTentCloth);
		AddMesh(tent, "TentTrim", CylinderMeshFor(1.90f, 1.90f, 0.08f), new Vector3(0.0f, 0.07f, 0.0f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(tent, "TentEntrance", BoxMeshFor(new Vector3(0.72f, 0.88f, 0.05f)), new Vector3(0.0f, 0.50f, -1.63f), Vector3.Zero, Vector3.One, _matActorDark);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.8f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.6f, Height = 1.6f },
		};
		tent.AddChild(collisionShape);
	}

	private void CreateCampfire(Vector3 position)
	{
		var fire = new StaticBody3D
		{
			Name = "Campfire",
			Position = position,
		};
		_propsRoot.AddChild(fire);

		AddMesh(fire, "StoneRing", CylinderMeshFor(1.05f, 1.05f, 0.16f), new Vector3(0.0f, 0.08f, 0.0f), Vector3.Zero, Vector3.One, _matRock);
		AddMesh(fire, "LogA", new CapsuleMesh { Radius = 0.08f, Height = 1.35f }, new Vector3(0.0f, 0.22f, 0.0f), new Vector3(88.0f, 36.0f, 0.0f), Vector3.One, _matWood);
		AddMesh(fire, "LogB", new CapsuleMesh { Radius = 0.08f, Height = 1.35f }, new Vector3(0.0f, 0.25f, 0.0f), new Vector3(88.0f, -36.0f, 0.0f), Vector3.One, _matWood);
		AddMesh(fire, "FlameCore", CylinderMeshFor(0.0f, 0.36f, 0.95f), new Vector3(0.0f, 0.72f, 0.0f), Vector3.Zero, Vector3.One, _matTorchFire);
		AddMesh(fire, "FlameGlow", new SphereMesh { Radius = 0.48f, Height = 0.85f }, new Vector3(0.0f, 0.78f, 0.0f), Vector3.Zero, new Vector3(0.75f, 1.1f, 0.75f), _matTorchFire);

		var light = new OmniLight3D
		{
			Name = "FireLight",
			LightColor = new Color(1.0f, 0.48f, 0.18f),
			LightEnergy = 1.25f,
			OmniRange = 8.0f,
			Position = new Vector3(0.0f, 1.2f, 0.0f),
		};
		fire.AddChild(light);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.25f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.05f, Height = 0.5f },
		};
		fire.AddChild(collisionShape);
	}

	private void CreateBanner(Vector3 position, float yawDegrees, Material clothMaterial)
	{
		var banner = new StaticBody3D
		{
			Name = "Banner",
			Position = position,
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
		};
		_propsRoot.AddChild(banner);

		AddMesh(banner, "Pole", new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.06f, Height = 2.7f }, new Vector3(0.0f, 1.35f, 0.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(banner, "Cloth", BoxMeshFor(new Vector3(0.72f, 0.9f, 0.035f)), new Vector3(0.36f, 1.92f, 0.0f), Vector3.Zero, Vector3.One, clothMaterial);
		AddMesh(banner, "RuneMark", BoxMeshFor(new Vector3(0.32f, 0.08f, 0.04f)), new Vector3(0.36f, 1.95f, -0.03f), new Vector3(0.0f, 0.0f, 35.0f), Vector3.One, _matRune);
	}

	private void CreateTorch(Vector3 position)
	{
		var torch = new StaticBody3D
		{
			Name = "Torch",
			Position = position,
		};
		_propsRoot.AddChild(torch);

		AddMesh(torch, "TorchPole", new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 2.2f }, new Vector3(0.0f, 1.1f, 0.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(torch, "TorchBowl", CylinderMeshFor(0.24f, 0.18f, 0.18f), new Vector3(0.0f, 2.18f, 0.0f), Vector3.Zero, Vector3.One, _matMetal);
		AddMesh(torch, "TorchFlame", CylinderMeshFor(0.0f, 0.18f, 0.55f), new Vector3(0.0f, 2.55f, 0.0f), Vector3.Zero, Vector3.One, _matTorchFire);

		var light = new OmniLight3D
		{
			Name = "TorchLight",
			LightColor = new Color(1.0f, 0.52f, 0.22f),
			LightEnergy = 0.8f,
			OmniRange = 5.5f,
			Position = new Vector3(0.0f, 2.35f, 0.0f),
		};
		torch.AddChild(light);
	}

	private void CreateCrateStack(Vector3 position, float yawDegrees)
	{
		var stack = new StaticBody3D
		{
			Name = "CrateStack",
			Position = position,
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
		};
		_propsRoot.AddChild(stack);

		AddMesh(stack, "CrateA", BoxMeshFor(new Vector3(0.9f, 0.62f, 0.85f)), new Vector3(-0.22f, 0.31f, 0.0f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(stack, "CrateB", BoxMeshFor(new Vector3(0.72f, 0.58f, 0.68f)), new Vector3(0.42f, 0.29f, 0.55f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(stack, "CrateC", BoxMeshFor(new Vector3(0.62f, 0.50f, 0.62f)), new Vector3(0.08f, 0.86f, 0.12f), Vector3.Zero, Vector3.One, _matLeather);
	}

	private void CreateGrassPatch(Vector3 position)
	{
		// Batched into a MultiMesh during map builds (World.Vegetation.cs); the
		// per-node version below is a defensive fallback if no batch is active.
		if (TryBatchGrassPatch(position))
		{
			return;
		}

		var patch = new Node3D
		{
			Name = "GrassPatch",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(patch);

		int bladeCount = _rng.RandiRange(5, 10);
		for (int index = 0; index < bladeCount; index++)
		{
			float height = (float)_rng.RandfRange(0.36f, 0.78f);
			float offsetX = (float)_rng.RandfRange(-0.48f, 0.48f);
			float offsetZ = (float)_rng.RandfRange(-0.48f, 0.48f);
			Material material = _rng.Randf() < 0.55f ? _matGrassBright : _matGrassDark;
			AddMesh(
				patch,
				"GrassBlade",
				BoxMeshFor(new Vector3(0.045f, height, 0.018f)),
				new Vector3(offsetX, height * 0.5f, offsetZ),
				new Vector3((float)_rng.RandfRange(-10.0f, 10.0f), (float)_rng.RandfRange(0.0f, 360.0f), (float)_rng.RandfRange(-18.0f, 18.0f)),
				Vector3.One,
				material
			);
		}
	}

	private void CreateFlowerPatch(Vector3 position)
	{
		if (TryBatchFlowerPatch(position))
		{
			return;
		}

		CreateGrassPatch(position);
		var patch = new Node3D { Name = "FlowerPatch", Position = position };
		_propsRoot.AddChild(patch);

		int flowerCount = _rng.RandiRange(2, 5);
		for (int index = 0; index < flowerCount; index++)
		{
			float offsetX = (float)_rng.RandfRange(-0.45f, 0.45f);
			float offsetZ = (float)_rng.RandfRange(-0.45f, 0.45f);
			float stemHeight = (float)_rng.RandfRange(0.28f, 0.5f);
			Material flowerMaterial = _rng.Randf() < 0.5f ? _matFlowerWarm : _matFlowerCool;
			AddMesh(patch, "FlowerStem", new CylinderMesh { TopRadius = 0.012f, BottomRadius = 0.018f, Height = stemHeight }, new Vector3(offsetX, stemHeight * 0.5f, offsetZ), Vector3.Zero, Vector3.One, _matGrassDark);
			AddMesh(patch, "FlowerHead", new SphereMesh { Radius = 0.07f, Height = 0.10f }, new Vector3(offsetX, stemHeight + 0.04f, offsetZ), Vector3.Zero, new Vector3(1.0f, 0.55f, 1.0f), flowerMaterial);
		}
	}

	private void CreateMushroom(Vector3 position)
	{
		if (TryPlacePropScene("res://assets/scenes/props/Mushroom.tscn", position, (float)_rng.RandfRange(0.0f, 360.0f), 1.0f))
		{
			return;
		}

		var mushroom = new Node3D
		{
			Name = "Mushroom",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(mushroom);

		AddMesh(mushroom, "Stem", new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.06f, Height = 0.36f }, new Vector3(0.0f, 0.18f, 0.0f), Vector3.Zero, Vector3.One, _matSkin);
		AddMesh(mushroom, "Cap", new SphereMesh { Radius = 0.22f, Height = 0.18f }, new Vector3(0.0f, 0.42f, 0.0f), Vector3.Zero, new Vector3(1.0f, 0.45f, 1.0f), _matMushroomCap);
		AddMesh(mushroom, "CapSpot", new SphereMesh { Radius = 0.035f, Height = 0.025f }, new Vector3(0.07f, 0.50f, -0.08f), Vector3.Zero, new Vector3(1.0f, 0.35f, 1.0f), _matEyeWhite);
	}

	private void CreateCrystalCluster(Vector3 position, float scale, Material material)
	{
		var cluster = new Node3D
		{
			Name = "CrystalCluster",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
			Scale = Vector3.One * scale,
		};
		_propsRoot.AddChild(cluster);

		AddMesh(cluster, "CrystalA", CylinderMeshFor(0.0f, 0.18f, 1.2f), new Vector3(0.0f, 0.6f, 0.0f), new Vector3(0.0f, 0.0f, -8.0f), Vector3.One, material);
		AddMesh(cluster, "CrystalB", CylinderMeshFor(0.0f, 0.13f, 0.82f), new Vector3(-0.32f, 0.42f, 0.14f), new Vector3(0.0f, 0.0f, 16.0f), Vector3.One, material);
		AddMesh(cluster, "CrystalC", CylinderMeshFor(0.0f, 0.12f, 0.74f), new Vector3(0.34f, 0.37f, -0.10f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, material);
	}

	private void CreateNestBones(Vector3 position)
	{
		var bones = new Node3D
		{
			Name = "NestBones",
			Position = position,
		};
		_propsRoot.AddChild(bones);

		for (int index = 0; index < 7; index++)
		{
			float angle = index / 7.0f * Mathf.Tau;
			Vector3 offset = new(Mathf.Cos(angle) * 1.8f, 0.16f, Mathf.Sin(angle) * 1.0f);
			AddMesh(bones, "Bone", new CapsuleMesh { Radius = 0.055f, Height = 1.15f }, offset, new Vector3(88.0f, Mathf.RadToDeg(angle), 0.0f), Vector3.One, _matMonsterClaw);
		}
	}

	private void CreatePlayer()
	{
		var player = new PlayerController
		{
			Name = "Player",
			Position = _citySpawnPosition + new Vector3(0.0f, 0.2f, 0.0f),
		};

		var collisionShape = new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Position = new Vector3(0.0f, 0.76f, 0.0f),
			Shape = new CapsuleShape3D { Radius = 0.31f, Height = 1.52f },
		};
		player.AddChild(collisionShape);

		var cameraPivot = new Node3D
		{
			Name = "CameraPivot",
			Position = new Vector3(0.0f, 1.38f, 0.0f),
		};
		player.AddChild(cameraPivot);

		var camera = new Camera3D
		{
			Name = "Camera3D",
			Current = true,
			Fov = 76.0f,
			Near = 0.05f,
		};
		cameraPivot.AddChild(camera);

		AddChild(player);
		_player = player;
	}

	private void SpawnActors()
	{
		// All wild maps share one persistent runtime state. They are populated once
		// when the game world opens; travelling only changes visibility/activity and
		// never rebuilds actors or rerolls their coordinates.
		if (_worldActorsGenerated)
		{
			return;
		}
		_worldActorsGenerated = true;

		// Multiplayer clients never simulate wild monsters/bosses locally —
		// they receive host-authoritative puppets instead (World.Network.cs).
		if (!IsNetworkClientWorld)
		{
			foreach (WildMapDefinition wildMap in WildMaps)
			{
				_wildMonsterTargetCountsById[wildMap.Id] = Mathf.Max(ActorCount / WildMaps.Length, 8);
				EnsureWildInstancePopulated(wildMap.Id, GetSelectedTier(wildMap.Id));
			}
		}

		SpawnCityNpcs();
		_monsterRespawnRemaining = MonsterRespawnInterval;
		UpdateActorMapActivity();
		UpdateActiveBossHud(false);
		_player.RefreshBossWorldStatus(true);
	}

	private SimpleActor SpawnMonsterForMap(string mapId, int forcedTier = 0)
	{
		SimpleActor actor = CreateActor(true, mapId, "", "", 0, forcedTier);

		// Tier evolution cues beyond raw stats: bigger body, sharper AI.
		int tier = actor.WorldTier;
		float tierVisualScale = WorldTierCatalog.GetMonsterVisualScale(tier);
		if (tier > WorldTierCatalog.MinTier)
		{
			if (tierVisualScale > 1.001f)
			{
				ScaleActorVisualChildren(actor, tierVisualScale);
			}
			actor.DetectionRadius += WorldTierCatalog.GetDetectionRadiusBonus(tier);
			actor.ChaseRadius += WorldTierCatalog.GetChaseRadiusBonus(tier);
			actor.AttackCooldown *= WorldTierCatalog.GetAttackCooldownFactor(tier);
		}

		// Roll rarity: boosts stats/drops and marks the monster in the field
		// (nameplate colour + star, and an aura/bigger body for elite/alpha).
		int rarity = MonsterRarity.Roll(_rng);
		if (rarity > MonsterRarity.Common)
		{
			actor.ApplyRarity(rarity);
			float rarityScale = MonsterRarity.VisualScale(rarity);
			if (rarityScale > 1.001f)
			{
				ScaleActorVisualChildren(actor, rarityScale);
			}
			if (MonsterRarity.HasAura(rarity))
			{
				AddBossAura(actor, MonsterRarity.Color(rarity), rarityScale);
			}
		}

		Vector3 spawnPosition = FindOpenMonsterSpawnPosition();
		actor.Position = spawnPosition;
		actor.HomePosition = spawnPosition;
		_actorsRoot.AddChild(actor);
		actor.SetWorldMapActive(IsActorInstanceActive(actor));
		RegisterNetworkMonster(actor, tierVisualScale, Colors.Transparent);
		return actor;
	}

	// A wild monster/boss is only live for the local player when they're on its
	// map AND (for wild maps) on its tier — map+tier pairs are parallel
	// instances (docs/world_progression.md). Captured companions and caves
	// keep the plain map check.
	private bool IsActorInstanceActive(SimpleActor actor)
	{
		if (actor.MapId != _activeMapId)
		{
			return false;
		}

		if (actor.ActorKind == "monster" && !actor.IsCaptured && IsWildMapId(actor.MapId))
		{
			return actor.WorldTier == GetSelectedTier(actor.MapId);
		}

		return true;
	}

	private SimpleActor SpawnBossForMap(BossDefinition definition, int tier, bool announce)
	{
		UseWildMapObstacleContext(definition.MapId);
		// Boss stats are hand-authored per map, so the tier layer is applied
		// explicitly here (docs/world_progression.md).
		tier = WorldTierCatalog.ClampTier(tier);
		int bossLevel = definition.Level + WorldTierCatalog.GetBossLevelBonus(tier);
		float bossMultiplier = WorldTierCatalog.GetBossStatMultiplier(tier);
		float rewardMultiplier = WorldTierCatalog.GetRewardMultiplier(tier);

		SimpleActor boss = CreateActor(true, definition.MapId, definition.SpeciesNameKey, definition.CombatRole, bossLevel, tier);
		boss.Name = $"Boss_{definition.MapId}_t{tier}";
		boss.ConfigureStats(
			definition.SpeciesNameKey,
			bossLevel,
			Mathf.RoundToInt(definition.MaxHealth * bossMultiplier),
			Mathf.RoundToInt(definition.Attack * bossMultiplier),
			Mathf.RoundToInt(definition.Defense * bossMultiplier),
			Mathf.RoundToInt(definition.ExperienceReward * rewardMultiplier),
			Mathf.RoundToInt(definition.GoldReward * rewardMultiplier));
		boss.WorldTier = tier;
		boss.ConfigureGrowth("ability.monster.charge", 5);
		boss.ConfigureBoss(definition.NameKey, definition.PrimaryLootId);
		boss.MoveSpeed = definition.CombatRole == "Tank" ? 6.0f : 6.5f;
		boss.AttackCooldown = definition.CombatRole == "Support" ? 1.45f : 1.25f;
		boss.DetectionRadius = 23.0f;
		boss.ChaseRadius = 32.0f;
		boss.WanderRadius = 17.0f;

		ScaleActorVisualChildren(boss, definition.VisualScale);
		ScaleBossCollision(boss, definition.VisualScale);
		AddBossAura(boss, definition.AuraColor, definition.VisualScale);

		Vector3 spawnPosition;
		if (!_wildBossSpawnPositionsByMapId.TryGetValue(definition.MapId, out spawnPosition))
		{
			spawnPosition = FindOpenBossSpawnPosition();
			_wildBossSpawnPositionsByMapId[definition.MapId] = spawnPosition;
		}
		boss.Position = spawnPosition;
		boss.HomePosition = spawnPosition;
		_actorsRoot.AddChild(boss);
		boss.CurrentHealth = boss.EffectiveMaxHealth;
		boss.SetWorldMapActive(IsActorInstanceActive(boss));
		string instanceKey = WildInstanceKey(definition.MapId, tier);
		_wildBossesByInstance[instanceKey] = boss;
		_wildBossRespawnRemainingByInstance.Remove(instanceKey);
		RegisterNetworkMonster(boss, definition.VisualScale, definition.AuraColor);

		bool isLocalInstance = definition.MapId == _activeMapId && tier == GetSelectedTier(definition.MapId);
		if (isLocalInstance)
		{
			_player.SetActiveBoss(boss);
		}
		if (announce && tier == GetSelectedTier(definition.MapId))
		{
			_player.ShowBossAppeared(boss, GetWildMapDisplayName(definition.MapId));
		}
		_player.RefreshBossWorldStatus(false);
		return boss;
	}

	private static void ScaleBossCollision(SimpleActor boss, float visualScale)
	{
		if (boss.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is not CollisionShape3D collision
			|| collision.Shape is not CapsuleShape3D capsule)
		{
			return;
		}

		// Keep the hit body imposing but slightly tighter than the oversized
		// visual, so the larger boss can still navigate forests and structures.
		float collisionScale = Mathf.Clamp(visualScale * 0.64f, 1.50f, 2.15f);
		capsule.Radius *= collisionScale;
		capsule.Height *= collisionScale;
		collision.Position = new Vector3(0.0f, collision.Position.Y * collisionScale, 0.0f);
	}

	private static void AddBossAura(SimpleActor boss, Color auraColor, float visualScale)
	{
		var auraRoot = new Node3D
		{
			Name = "BossAura",
			Position = new Vector3(0.0f, 0.08f, 0.0f),
		};
		boss.AddChild(auraRoot);

		var moteMaterial = new StandardMaterial3D
		{
			AlbedoColor = auraColor,
			EmissionEnabled = true,
			Emission = new Color(auraColor.R, auraColor.G, auraColor.B),
			EmissionEnergyMultiplier = 3.2f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
		};
		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.74f * visualScale,
			Direction = Vector3.Up,
			Spread = 32.0f,
			InitialVelocityMin = 0.42f,
			InitialVelocityMax = 1.24f,
			Gravity = new Vector3(0.0f, 0.32f, 0.0f),
			ScaleMin = 0.45f,
			ScaleMax = 1.28f,
			Color = auraColor,
		};
		auraRoot.AddChild(new GpuParticles3D
		{
			Name = "BossAuraMotes",
			Amount = 46,
			Lifetime = 1.75f,
			Preprocess = 1.75f,
			Randomness = 0.62f,
			VisibilityAabb = new Aabb(new Vector3(-5.0f, -0.5f, -5.0f), new Vector3(10.0f, 7.0f, 10.0f)),
			ProcessMaterial = processMaterial,
			DrawPass1 = new QuadMesh
			{
				Size = new Vector2(0.085f * visualScale, 0.28f * visualScale),
				Material = moteMaterial,
			},
			Emitting = true,
		});

		auraRoot.AddChild(new OmniLight3D
		{
			Name = "BossAuraLight",
			Position = new Vector3(0.0f, 1.15f * visualScale, 0.0f),
			LightColor = new Color(auraColor.R, auraColor.G, auraColor.B),
			LightEnergy = 1.65f,
			OmniRange = 4.2f * visualScale,
			ShadowEnabled = false,
		});
	}

	private void UseWildMapObstacleContext(string mapId)
	{
		_obstaclePositions.Clear();
		if (_wildObstaclePositionsById.TryGetValue(mapId, out List<Vector3>? obstacles))
		{
			_obstaclePositions.AddRange(obstacles);
		}
	}

	private void SpawnCityNpcs()
	{
		const float shopRadius = 31.0f;
		const float frontDistance = 5.0f;
		CityNpcStation[] stations =
		{
			new("name.npc.blacksmith", RingFrontOffset(306.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(306.0f, shopRadius)), 0.8f, "Tank"),
			new("name.npc.item_merchant", RingFrontOffset(54.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(54.0f, shopRadius)), 0.8f, "Support"),
			new("name.npc.pet_trainer", RingFrontOffset(234.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(234.0f, shopRadius)), 0.7f, "Support"),
			new("name.npc.mercenary_broker", RingFrontOffset(126.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(126.0f, shopRadius)), 0.7f, "DPS"),
			new("name.npc.warehouse_keeper", RingFrontOffset(0.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(0.0f, shopRadius)), 0.8f, "Support"),
		};

		// Functional shop NPCs (always present).
		foreach (CityNpcStation station in stations)
		{
			SpawnCityNpc(station, string.Empty);
		}

		// Quest / recruit NPCs: exactly ONE per distinct NPC model, so no model
		// is repeated (keeps the city from feeling crowded with clones).
		List<string> npcModels = ExternalModelLibrary.GetDistinctNpcModels();
		string[] recruitNames = { "name.npc.gatherer", "name.npc.hunter", "name.npc.apprentice", "name.npc.guard" };
		string[] recruitRoles = { "Gatherer", "Ranged", "DPS", "Tank" };
		for (int index = 0; index < npcModels.Count; index++)
		{
			float angleDeg = index / (float)Mathf.Max(npcModels.Count, 1) * 360.0f;
			Vector3 offset = RingFrontOffset(angleDeg, 20.0f, 1.8f);
			var recruit = new CityNpcStation(
				recruitNames[index % recruitNames.Length],
				offset,
				YawFacingCenter(RingOffset(angleDeg, 20.0f)),
				1.1f,
				recruitRoles[index % recruitRoles.Length]);
			SpawnCityNpc(recruit, npcModels[index]);
		}
	}

	private void SpawnCityNpc(CityNpcStation station, string forcedModelPath)
	{
		SimpleActor actor = CreateActor(false, "city", station.NameKey, station.Role, 0, 0, forcedModelPath);
		Vector3 spawnPosition = _mainCityCenter + station.Offset;
		actor.RotationDegrees = new Vector3(0.0f, station.YawDegrees, 0.0f);
		actor.Position = spawnPosition;
		actor.HomePosition = spawnPosition;
		actor.WanderRadius = station.WanderRadius;
		actor.MoveSpeed = (float)_rng.RandfRange(0.55f, 0.9f);
		_actorsRoot.AddChild(actor);
	}

	private CityNpcStation CreateAmbientCityNpcStation(int index)
	{
		string[] names =
		{
			"name.npc.hunter",
			"name.npc.gatherer",
			"name.npc.apprentice",
		};
		string[] roles =
		{
			"Ranged",
			"Gatherer",
			"DPS",
		};
		float angle = index * 1.37f;
		float radius = 16.0f + index % 4 * 3.2f;
		var offset = new Vector3(Mathf.Sin(angle) * radius, 0.0f, 7.0f + Mathf.Cos(angle) * radius);
		return new CityNpcStation(names[index % names.Length], offset, Mathf.RadToDeg(angle) + 180.0f, 1.2f, roles[index % roles.Length]);
	}

	public SimpleActor SpawnContractCompanion(PlayerController.ContractCompanionOffer offer)
	{
		SimpleActor actor = CreateActor(false, "city", offer.NameKey, offer.CombatRole, offer.Level);
		actor.ConfigureStats(offer.NameKey, offer.Level, offer.MaxHealth, offer.Attack, offer.Defense, offer.Level * 6, 0);
		actor.ConfigureGrowth(offer.CombatRole == "Support" ? "ability.npc.heal" : "ability.npc.guard", Mathf.Max(offer.Level / 2, 1));
		actor.ConfigureCombatProfile(offer.CombatRole, "personality.brave", offer.CombatRole == "Support" ? "passive.protector" : "passive.combo_rhythm", 5);
		Vector3 spawnPosition = _mainCityCenter + RingFrontOffset(126.0f, 31.0f, 2.6f);
		actor.Position = spawnPosition;
		actor.HomePosition = spawnPosition;
		actor.WanderRadius = 0.6f;
		actor.MoveSpeed = 6.7f;
		_actorsRoot.AddChild(actor);
		return actor;
	}

	public SimpleActor SpawnPurchasedPet(string monsterNameKey, int level, int maxHealth, int attack, int defense)
	{
		string combatRole = MonsterSpeciesCatalog.Current.GetDefaultRole(monsterNameKey);
		SimpleActor actor = CreateActor(true, "city", monsterNameKey, combatRole, level);
		actor.ConfigureStats(monsterNameKey, level, maxHealth, attack, defense, level * 8, 0);
		actor.ConfigureGrowth("ability.monster.track", Mathf.Max(level / 2, 1));
		actor.ConfigureCombatProfile(combatRole, "personality.friendly", "passive.fast_growth", 5);
		Vector3 spawnPosition = _mainCityCenter + RingFrontOffset(234.0f, 31.0f, 2.4f);
		actor.Position = spawnPosition;
		actor.HomePosition = spawnPosition;
		actor.WanderRadius = 0.6f;
		actor.MoveSpeed = 7.1f;
		_actorsRoot.AddChild(actor);
		return actor;
	}

	private SimpleActor CreateActor(bool isMonster, string mapId = "wild_forest", string forcedDisplayName = "", string forcedCombatRole = "", int forcedLevel = 0, int forcedTier = 0, string forcedModelPath = "")
	{
		var actor = new SimpleActor
		{
			Name = isMonster ? "Monster" : "NPC",
			ActorKind = isMonster ? "monster" : "npc",
			MapId = isMonster ? mapId : "city",
			MoveSpeed = isMonster ? (float)_rng.RandfRange(6.4f, 8.0f) : (float)_rng.RandfRange(1.1f, 1.8f),
			WanderRadius = (float)_rng.RandfRange(8.0f, 17.0f),
		};
		ConfigureActorStats(actor, isMonster, forcedDisplayName, forcedCombatRole, forcedLevel, forcedTier);

		var collisionShape = new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Position = new Vector3(0.0f, isMonster ? 0.78f : 0.74f, 0.0f),
			Shape = new CapsuleShape3D
			{
				Radius = isMonster ? 0.44f : 0.29f,
				Height = isMonster ? 1.46f : 1.48f,
			},
		};
		actor.AddChild(collisionShape);

		if (isMonster)
		{
			BuildMonsterVisual(actor);
		}
		else
		{
			BuildNpcVisual(actor, forcedModelPath);
		}

		ScaleActorVisualChildren(actor, isMonster ? 0.88f : 0.86f);
		return actor;
	}

	private static void ScaleActorVisualChildren(Node3D actor, float visualScale)
	{
		foreach (Node child in actor.GetChildren())
		{
			if (child is CollisionShape3D || child is Label3D)
			{
				continue;
			}

			if (child is Node3D visualNode)
			{
				visualNode.Position *= visualScale;
				visualNode.Scale *= visualScale;
			}
		}
	}

	private void BuildNpcVisual(Node3D actor, string forcedModelPath = "")
	{
		if (actor is SimpleActor npcActor && ExternalModelLibrary.TryAddActorModel(npcActor, forcedModelPath))
		{
			return;
		}

		AddMesh(actor, "Torso", new CapsuleMesh { Radius = 0.28f, Height = 0.92f }, new Vector3(0.0f, 1.02f, 0.0f), Vector3.Zero, new Vector3(0.92f, 1.0f, 0.78f), _matNpc);
		AddMesh(actor, "ChestTrim", BoxMeshFor(new Vector3(0.58f, 0.08f, 0.06f)), new Vector3(0.0f, 1.20f, -0.24f), Vector3.Zero, Vector3.One, _matNpcAccent);
		AddMesh(actor, "Belt", BoxMeshFor(new Vector3(0.66f, 0.10f, 0.12f)), new Vector3(0.0f, 0.74f, -0.02f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "BeltBuckle", BoxMeshFor(new Vector3(0.14f, 0.12f, 0.05f)), new Vector3(0.0f, 0.74f, -0.28f), Vector3.Zero, Vector3.One, _matMetal);

		AddMesh(actor, "Head", new SphereMesh { Radius = 0.27f, Height = 0.54f }, new Vector3(0.0f, 1.66f, 0.0f), Vector3.Zero, new Vector3(0.94f, 1.05f, 0.92f), _matSkin);
		AddMesh(actor, "Hair", new SphereMesh { Radius = 0.285f, Height = 0.36f }, new Vector3(0.0f, 1.82f, 0.02f), Vector3.Zero, new Vector3(1.02f, 0.48f, 0.92f), _matActorDark);
		AddMesh(actor, "HatBrim", CylinderMeshFor(0.36f, 0.36f, 0.04f), new Vector3(0.0f, 1.87f, 0.0f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "HatTop", CylinderMeshFor(0.20f, 0.28f, 0.20f), new Vector3(0.0f, 1.98f, 0.0f), Vector3.Zero, Vector3.One, _matLeather);

		AddEye(actor, new Vector3(-0.095f, 1.68f, -0.245f), 0.033f);
		AddEye(actor, new Vector3(0.095f, 1.68f, -0.245f), 0.033f);
		AddMesh(actor, "Nose", CylinderMeshFor(0.018f, 0.035f, 0.09f), new Vector3(0.0f, 1.63f, -0.275f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, _matSkin);

		AddMesh(actor, "LeftArm", new CapsuleMesh { Radius = 0.075f, Height = 0.78f }, new Vector3(-0.38f, 1.04f, 0.0f), new Vector3(0.0f, 0.0f, -9.0f), Vector3.One, _matSkin);
		AddMesh(actor, "RightArm", new CapsuleMesh { Radius = 0.075f, Height = 0.78f }, new Vector3(0.38f, 1.04f, 0.0f), new Vector3(0.0f, 0.0f, 9.0f), Vector3.One, _matSkin);
		AddMesh(actor, "LeftShoulder", new SphereMesh { Radius = 0.12f, Height = 0.14f }, new Vector3(-0.33f, 1.32f, -0.02f), Vector3.Zero, new Vector3(1.2f, 0.55f, 0.9f), _matMetal);
		AddMesh(actor, "RightShoulder", new SphereMesh { Radius = 0.12f, Height = 0.14f }, new Vector3(0.33f, 1.32f, -0.02f), Vector3.Zero, new Vector3(1.2f, 0.55f, 0.9f), _matMetal);
		AddMesh(actor, "LeftGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(-0.44f, 0.66f, -0.03f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "RightGlove", new SphereMesh { Radius = 0.10f, Height = 0.18f }, new Vector3(0.44f, 0.66f, -0.03f), Vector3.Zero, Vector3.One, _matLeather);

		AddMesh(actor, "LeftLeg", new CapsuleMesh { Radius = 0.095f, Height = 0.72f }, new Vector3(-0.14f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "RightLeg", new CapsuleMesh { Radius = 0.095f, Height = 0.72f }, new Vector3(0.14f, 0.36f, 0.0f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "LeftBoot", BoxMeshFor(new Vector3(0.20f, 0.12f, 0.32f)), new Vector3(-0.14f, 0.06f, -0.05f), Vector3.Zero, Vector3.One, _matActorDark);
		AddMesh(actor, "RightBoot", BoxMeshFor(new Vector3(0.20f, 0.12f, 0.32f)), new Vector3(0.14f, 0.06f, -0.05f), Vector3.Zero, Vector3.One, _matActorDark);

		AddMesh(actor, "Backpack", BoxMeshFor(new Vector3(0.42f, 0.48f, 0.18f)), new Vector3(0.0f, 1.08f, 0.31f), Vector3.Zero, Vector3.One, _matLeather);
		AddMesh(actor, "Cape", BoxMeshFor(new Vector3(0.48f, 0.78f, 0.055f)), new Vector3(0.0f, 1.04f, 0.38f), new Vector3(-8.0f, 0.0f, 0.0f), Vector3.One, _matNpcAccent);

		if (actor is SimpleActor npc)
		{
			AddNpcRoleAccessory(actor, npc.CombatRole);
		}
	}

	private void BuildMonsterVisual(Node3D actor)
	{
		if (actor is SimpleActor caveMonster && TryBuildCaveMonsterVisual(caveMonster))
		{
			return;
		}
		if (actor is SimpleActor monsterActor && ExternalModelLibrary.TryAddActorModel(monsterActor))
		{
			return;
		}

		AddMesh(actor, "BodyCore", new SphereMesh { Radius = 0.54f, Height = 0.86f }, new Vector3(0.0f, 0.74f, 0.10f), Vector3.Zero, new Vector3(1.34f, 0.72f, 1.72f), _matMonster);
		AddMesh(actor, "ChestMass", new SphereMesh { Radius = 0.42f, Height = 0.62f }, new Vector3(0.0f, 0.86f, -0.50f), Vector3.Zero, new Vector3(1.26f, 0.82f, 1.05f), _matMonster);
		AddMesh(actor, "HindMass", new SphereMesh { Radius = 0.45f, Height = 0.62f }, new Vector3(0.0f, 0.72f, 0.68f), Vector3.Zero, new Vector3(1.36f, 0.78f, 0.98f), _matMonster);
		AddMesh(actor, "BellyPlate", new SphereMesh { Radius = 0.34f, Height = 0.42f }, new Vector3(0.0f, 0.52f, -0.06f), Vector3.Zero, new Vector3(1.10f, 0.42f, 1.58f), _matMonsterBelly);
		AddMesh(actor, "Neck", new CapsuleMesh { Radius = 0.16f, Height = 0.52f }, new Vector3(0.0f, 1.03f, -0.60f), new Vector3(38.0f, 0.0f, 0.0f), new Vector3(1.08f, 1.0f, 0.90f), _matMonster);
		AddMesh(actor, "Head", new SphereMesh { Radius = 0.38f, Height = 0.62f }, new Vector3(0.0f, 1.18f, -0.92f), Vector3.Zero, new Vector3(1.12f, 0.84f, 0.96f), _matMonster);
		AddMesh(actor, "Snout", new CapsuleMesh { Radius = 0.15f, Height = 0.55f }, new Vector3(0.0f, 1.08f, -1.22f), new Vector3(90.0f, 0.0f, 0.0f), new Vector3(1.30f, 0.78f, 1.0f), _matMonsterBelly);
		AddMesh(actor, "Nose", new SphereMesh { Radius = 0.08f, Height = 0.10f }, new Vector3(0.0f, 1.12f, -1.49f), Vector3.Zero, new Vector3(1.35f, 0.70f, 0.75f), _matActorDark);

		AddEye(actor, new Vector3(-0.17f, 1.29f, -1.17f), 0.058f);
		AddEye(actor, new Vector3(0.17f, 1.29f, -1.17f), 0.058f);
		AddMesh(actor, "BrowLeft", BoxMeshFor(new Vector3(0.22f, 0.055f, 0.06f)), new Vector3(-0.16f, 1.38f, -1.12f), new Vector3(0.0f, 0.0f, -10.0f), Vector3.One, _matHorn);
		AddMesh(actor, "BrowRight", BoxMeshFor(new Vector3(0.22f, 0.055f, 0.06f)), new Vector3(0.16f, 1.38f, -1.12f), new Vector3(0.0f, 0.0f, 10.0f), Vector3.One, _matHorn);
		AddMesh(actor, "LowerJaw", BoxMeshFor(new Vector3(0.36f, 0.07f, 0.20f)), new Vector3(0.0f, 1.00f, -1.28f), Vector3.Zero, Vector3.One, _matActorDark);
		AddMesh(actor, "LeftFang", CylinderMeshFor(0.0f, 0.027f, 0.14f), new Vector3(-0.12f, 0.96f, -1.38f), new Vector3(8.0f, 0.0f, 0.0f), Vector3.One, _matMonsterClaw);
		AddMesh(actor, "RightFang", CylinderMeshFor(0.0f, 0.027f, 0.14f), new Vector3(0.12f, 0.96f, -1.38f), new Vector3(8.0f, 0.0f, 0.0f), Vector3.One, _matMonsterClaw);

		AddHorn(actor, new Vector3(-0.24f, 1.49f, -0.78f), new Vector3(28.0f, 0.0f, -28.0f));
		AddHorn(actor, new Vector3(0.24f, 1.49f, -0.78f), new Vector3(28.0f, 0.0f, 28.0f));
		AddMesh(actor, "LeftEar", CylinderMeshFor(0.0f, 0.08f, 0.24f), new Vector3(-0.36f, 1.32f, -0.86f), new Vector3(40.0f, 0.0f, -48.0f), Vector3.One, _matMonster);
		AddMesh(actor, "RightEar", CylinderMeshFor(0.0f, 0.08f, 0.24f), new Vector3(0.36f, 1.32f, -0.86f), new Vector3(40.0f, 0.0f, 48.0f), Vector3.One, _matMonster);
		AddMesh(actor, "BackSpikeA", CylinderMeshFor(0.0f, 0.105f, 0.36f), new Vector3(0.0f, 1.22f, -0.28f), new Vector3(-22.0f, 0.0f, 0.0f), Vector3.One, _matHorn);
		AddMesh(actor, "BackSpikeB", CylinderMeshFor(0.0f, 0.10f, 0.34f), new Vector3(0.0f, 1.20f, 0.03f), new Vector3(-26.0f, 0.0f, 0.0f), Vector3.One, _matHorn);
		AddMesh(actor, "BackSpikeC", CylinderMeshFor(0.0f, 0.09f, 0.30f), new Vector3(0.0f, 1.12f, 0.35f), new Vector3(-30.0f, 0.0f, 0.0f), Vector3.One, _matHorn);
		AddMesh(actor, "BackSpikeD", CylinderMeshFor(0.0f, 0.075f, 0.24f), new Vector3(0.0f, 1.00f, 0.66f), new Vector3(-34.0f, 0.0f, 0.0f), Vector3.One, _matHorn);

		AddMesh(actor, "LeftShoulder", new SphereMesh { Radius = 0.19f, Height = 0.25f }, new Vector3(-0.42f, 0.80f, -0.52f), Vector3.Zero, new Vector3(1.10f, 0.72f, 0.95f), _matMonster);
		AddMesh(actor, "RightShoulder", new SphereMesh { Radius = 0.19f, Height = 0.25f }, new Vector3(0.42f, 0.80f, -0.52f), Vector3.Zero, new Vector3(1.10f, 0.72f, 0.95f), _matMonster);
		AddMesh(actor, "LeftHip", new SphereMesh { Radius = 0.21f, Height = 0.28f }, new Vector3(-0.44f, 0.72f, 0.52f), Vector3.Zero, new Vector3(1.08f, 0.74f, 0.95f), _matMonster);
		AddMesh(actor, "RightHip", new SphereMesh { Radius = 0.21f, Height = 0.28f }, new Vector3(0.44f, 0.72f, 0.52f), Vector3.Zero, new Vector3(1.08f, 0.74f, 0.95f), _matMonster);
		AddMesh(actor, "LeftForeLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.60f }, new Vector3(-0.42f, 0.42f, -0.55f), new Vector3(7.0f, 0.0f, -7.0f), Vector3.One, _matMonster);
		AddMesh(actor, "RightForeLeg", new CapsuleMesh { Radius = 0.105f, Height = 0.60f }, new Vector3(0.42f, 0.42f, -0.55f), new Vector3(7.0f, 0.0f, 7.0f), Vector3.One, _matMonster);
		AddMesh(actor, "LeftBackLeg", new CapsuleMesh { Radius = 0.12f, Height = 0.64f }, new Vector3(-0.44f, 0.40f, 0.52f), new Vector3(-8.0f, 0.0f, -8.0f), Vector3.One, _matMonster);
		AddMesh(actor, "RightBackLeg", new CapsuleMesh { Radius = 0.12f, Height = 0.64f }, new Vector3(0.44f, 0.40f, 0.52f), new Vector3(-8.0f, 0.0f, 8.0f), Vector3.One, _matMonster);
		AddMesh(actor, "LeftFrontPaw", new SphereMesh { Radius = 0.14f, Height = 0.16f }, new Vector3(-0.42f, 0.13f, -0.70f), Vector3.Zero, new Vector3(1.18f, 0.42f, 1.36f), _matMonsterBelly);
		AddMesh(actor, "RightFrontPaw", new SphereMesh { Radius = 0.14f, Height = 0.16f }, new Vector3(0.42f, 0.13f, -0.70f), Vector3.Zero, new Vector3(1.18f, 0.42f, 1.36f), _matMonsterBelly);
		AddMesh(actor, "LeftBackPaw", new SphereMesh { Radius = 0.15f, Height = 0.17f }, new Vector3(-0.46f, 0.13f, 0.68f), Vector3.Zero, new Vector3(1.22f, 0.42f, 1.32f), _matMonsterBelly);
		AddMesh(actor, "RightBackPaw", new SphereMesh { Radius = 0.15f, Height = 0.17f }, new Vector3(0.46f, 0.13f, 0.68f), Vector3.Zero, new Vector3(1.22f, 0.42f, 1.32f), _matMonsterBelly);

		AddClaw(actor, new Vector3(-0.50f, 0.08f, -0.83f), -16.0f);
		AddClaw(actor, new Vector3(-0.38f, 0.08f, -0.86f), 0.0f);
		AddClaw(actor, new Vector3(-0.26f, 0.08f, -0.83f), 16.0f);
		AddClaw(actor, new Vector3(0.26f, 0.08f, -0.83f), -16.0f);
		AddClaw(actor, new Vector3(0.38f, 0.08f, -0.86f), 0.0f);
		AddClaw(actor, new Vector3(0.50f, 0.08f, -0.83f), 16.0f);
		AddClaw(actor, new Vector3(-0.52f, 0.08f, 0.55f), -18.0f);
		AddClaw(actor, new Vector3(0.52f, 0.08f, 0.55f), 18.0f);

		AddMesh(actor, "TailBase", new CapsuleMesh { Radius = 0.105f, Height = 0.88f }, new Vector3(0.0f, 0.73f, 1.06f), new Vector3(64.0f, 0.0f, 0.0f), new Vector3(1.0f, 0.86f, 1.0f), _matMonster);
		AddMesh(actor, "TailTip", new SphereMesh { Radius = 0.15f, Height = 0.22f }, new Vector3(0.0f, 0.38f, 1.42f), Vector3.Zero, new Vector3(1.0f, 0.82f, 1.0f), _matMonsterBelly);

		if (actor is SimpleActor monster)
		{
			AddMonsterRoleDetails(actor, monster.CombatRole);
		}
	}

	private void AddNpcRoleAccessory(Node3D actor, string combatRole)
	{
		switch (combatRole)
		{
			case "Tank":
				AddMesh(actor, "Shield", CylinderMeshFor(0.32f, 0.32f, 0.06f), new Vector3(-0.58f, 1.02f, -0.20f), new Vector3(90.0f, 0.0f, 0.0f), new Vector3(0.82f, 1.18f, 1.0f), _matMetal);
				AddMesh(actor, "ShieldEmblem", CylinderMeshFor(0.16f, 0.16f, 0.065f), new Vector3(-0.58f, 1.02f, -0.235f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, _matNpcAccent);
				break;
			case "Ranged":
				AddMesh(actor, "Bow", new CapsuleMesh { Radius = 0.035f, Height = 1.05f }, new Vector3(0.54f, 1.04f, 0.02f), new Vector3(0.0f, 0.0f, 18.0f), new Vector3(1.0f, 1.0f, 0.6f), _matWood);
				AddMesh(actor, "ArrowBundle", BoxMeshFor(new Vector3(0.18f, 0.62f, 0.10f)), new Vector3(0.22f, 1.12f, 0.38f), new Vector3(-14.0f, 0.0f, 10.0f), Vector3.One, _matLeather);
				AddMesh(actor, "Quiver", CylinderMeshFor(0.11f, 0.14f, 0.62f), new Vector3(0.24f, 1.12f, 0.43f), new Vector3(-18.0f, 0.0f, 12.0f), new Vector3(0.82f, 1.0f, 0.82f), _matLeather);
				AddMesh(actor, "ReadyArrow", new CapsuleMesh { Radius = 0.018f, Height = 0.86f }, new Vector3(0.55f, 1.03f, -0.13f), new Vector3(88.0f, 0.0f, 10.0f), Vector3.One, _matHorn);
				AddMesh(actor, "ArrowTip", CylinderMeshFor(0.0f, 0.045f, 0.13f), new Vector3(0.58f, 1.00f, -0.55f), new Vector3(88.0f, 0.0f, 10.0f), Vector3.One, _matMetal);
				break;
			case "Support":
				AddMesh(actor, "Staff", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 1.35f }, new Vector3(0.55f, 1.06f, -0.04f), new Vector3(0.0f, 0.0f, -8.0f), Vector3.One, _matWood);
				AddMesh(actor, "StaffCrystal", CylinderMeshFor(0.0f, 0.12f, 0.30f), new Vector3(0.65f, 1.74f, -0.04f), new Vector3(0.0f, 0.0f, -8.0f), Vector3.One, _matCrystal);
				break;
			case "Gatherer":
				AddMesh(actor, "HerbPouch", BoxMeshFor(new Vector3(0.24f, 0.32f, 0.16f)), new Vector3(-0.42f, 0.72f, -0.08f), Vector3.Zero, Vector3.One, _matLeather);
				AddMesh(actor, "SickleBlade", CylinderMeshFor(0.0f, 0.10f, 0.28f), new Vector3(0.52f, 0.72f, -0.12f), new Vector3(64.0f, 0.0f, 24.0f), Vector3.One, _matMetal);
				break;
			case "Builder":
				AddMesh(actor, "HammerHandle", new CylinderMesh { TopRadius = 0.035f, BottomRadius = 0.045f, Height = 0.72f }, new Vector3(0.52f, 0.85f, -0.10f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, _matWood);
				AddMesh(actor, "HammerHead", BoxMeshFor(new Vector3(0.34f, 0.16f, 0.16f)), new Vector3(0.62f, 1.17f, -0.10f), new Vector3(0.0f, 0.0f, -18.0f), Vector3.One, _matMetal);
				break;
			default:
				AddMesh(actor, "SwordBlade", BoxMeshFor(new Vector3(0.075f, 0.78f, 0.045f)), new Vector3(0.55f, 0.98f, -0.12f), new Vector3(0.0f, 0.0f, -22.0f), Vector3.One, _matMetal);
				AddMesh(actor, "SwordGuard", BoxMeshFor(new Vector3(0.26f, 0.055f, 0.055f)), new Vector3(0.43f, 0.68f, -0.12f), new Vector3(0.0f, 0.0f, -22.0f), Vector3.One, _matHorn);
				break;
		}
	}

	private void AddMonsterRoleDetails(Node3D actor, string combatRole)
	{
		switch (combatRole)
		{
			case "Tank":
				AddMesh(actor, "ArmorPlateA", BoxMeshFor(new Vector3(0.72f, 0.10f, 0.34f)), new Vector3(0.0f, 1.14f, 0.18f), new Vector3(-18.0f, 0.0f, 0.0f), Vector3.One, _matRock);
				AddMesh(actor, "ArmorPlateB", BoxMeshFor(new Vector3(0.62f, 0.10f, 0.30f)), new Vector3(0.0f, 1.00f, 0.56f), new Vector3(-24.0f, 0.0f, 0.0f), Vector3.One, _matRock);
				AddMesh(actor, "ThickBrow", BoxMeshFor(new Vector3(0.46f, 0.08f, 0.07f)), new Vector3(0.0f, 1.42f, -0.78f), Vector3.Zero, Vector3.One, _matHorn);
				break;
			case "Ranged":
				AddMesh(actor, "ThroatGlow", new SphereMesh { Radius = 0.18f, Height = 0.26f }, new Vector3(0.0f, 1.00f, -0.72f), Vector3.Zero, new Vector3(1.2f, 0.72f, 0.8f), _matCrystal);
				AddMesh(actor, "SpitOrb", new SphereMesh { Radius = 0.12f, Height = 0.18f }, new Vector3(0.0f, 1.08f, -1.10f), Vector3.Zero, Vector3.One, _matCrystal);
				AddMesh(actor, "SpitterJawMark", BoxMeshFor(new Vector3(0.30f, 0.035f, 0.06f)), new Vector3(0.0f, 1.03f, -1.42f), Vector3.Zero, Vector3.One, _matCrystal);
				AddMesh(actor, "BackCrystalLauncher", CylinderMeshFor(0.0f, 0.10f, 0.34f), new Vector3(0.0f, 1.32f, -0.05f), new Vector3(-24.0f, 0.0f, 0.0f), Vector3.One, _matCrystal);
				break;
			case "Support":
				AddMesh(actor, "RuneBand", CylinderMeshFor(0.58f, 0.58f, 0.035f), new Vector3(0.0f, 0.98f, 0.08f), Vector3.Zero, new Vector3(1.12f, 1.0f, 1.42f), _matRune);
				AddMesh(actor, "RuneGem", new SphereMesh { Radius = 0.12f, Height = 0.18f }, new Vector3(0.0f, 1.34f, -0.70f), Vector3.Zero, Vector3.One, _matRune);
				break;
			default:
				AddClaw(actor, new Vector3(-0.20f, 0.12f, -0.84f), -4.0f);
				AddClaw(actor, new Vector3(0.20f, 0.12f, -0.84f), 4.0f);
				AddMesh(actor, "AggroStripe", BoxMeshFor(new Vector3(0.12f, 0.05f, 0.92f)), new Vector3(0.0f, 1.25f, -0.08f), new Vector3(-18.0f, 0.0f, 0.0f), Vector3.One, _matHorn);
				break;
		}
	}

	private MeshInstance3D AddMesh(Node3D parent, string nodeName, Mesh mesh, Vector3 position, Vector3 rotationDegrees, Vector3 scale, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
			RotationDegrees = rotationDegrees,
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(meshInstance);
		return meshInstance;
	}

	private void AddEye(Node3D actor, Vector3 position, float radius)
	{
		AddMesh(actor, "EyeWhite", new SphereMesh { Radius = radius, Height = radius * 2.0f }, position, Vector3.Zero, new Vector3(1.0f, 1.0f, 0.45f), _matEyeWhite);
		AddMesh(actor, "EyePupil", new SphereMesh { Radius = radius * 0.45f, Height = radius * 0.9f }, position + new Vector3(0.0f, 0.0f, -radius * 0.72f), Vector3.Zero, new Vector3(1.0f, 1.0f, 0.35f), _matActorDark);
	}

	private void AddClaw(Node3D actor, Vector3 position, float yawDegrees)
	{
		AddMesh(actor, "Claw", CylinderMeshFor(0.0f, 0.045f, 0.18f), position, new Vector3(72.0f, yawDegrees, 0.0f), Vector3.One, _matMonsterClaw);
	}

	private void ConfigureActorStats(SimpleActor actor, bool isMonster, string forcedDisplayName = "", string forcedCombatRole = "", int forcedLevel = 0, int forcedTier = 0)
	{
		// World Tier scaling (docs/world_progression.md): the instance's tier
		// shifts the level band and multiplies stats/rewards on top of it.
		// Wild instances pass their tier explicitly; caves fall back to the
		// local player's selected tier of the parent map.
		int tier = !isMonster
			? WorldTierCatalog.MinTier
			: forcedTier > 0 ? WorldTierCatalog.ClampTier(forcedTier) : GetSelectedTier(actor.MapId);
		(int minLevel, int maxLevel) = WorldTierCatalog.GetMonsterLevelRange(tier);
		float statMultiplier = WorldTierCatalog.GetStatMultiplier(tier);
		float rewardMultiplier = WorldTierCatalog.GetRewardMultiplier(tier);

		int level = forcedLevel > 0 ? forcedLevel : isMonster ? _rng.RandiRange(minLevel, maxLevel) : _rng.RandiRange(1, 7);
		int maxHealth = isMonster
			? Mathf.RoundToInt((95 + level * 22 + _rng.RandiRange(0, 35)) * statMultiplier)
			: 70 + level * 14 + _rng.RandiRange(0, 24);
		int attack = isMonster
			? Mathf.RoundToInt((9 + level * 4 + _rng.RandiRange(0, 5)) * statMultiplier)
			: 5 + level * 2 + _rng.RandiRange(0, 3);
		int defense = isMonster
			? Mathf.RoundToInt((5 + level * 3 + _rng.RandiRange(0, 4)) * statMultiplier)
			: 4 + level * 2 + _rng.RandiRange(0, 3);
		int experience = isMonster ? Mathf.RoundToInt((level * 9 + _rng.RandiRange(3, 12)) * rewardMultiplier) : level * 4 + _rng.RandiRange(1, 5);
		int gold = isMonster ? Mathf.RoundToInt((level * 3 + _rng.RandiRange(0, 8)) * rewardMultiplier) : level + _rng.RandiRange(0, 4);
		actor.WorldTier = tier;
		string[] namePool = isMonster ? MonsterSpeciesCatalog.Current.GetNamePool(actor.MapId) : NpcNames;
		string displayName = string.IsNullOrWhiteSpace(forcedDisplayName)
			? namePool[_rng.RandiRange(0, namePool.Length - 1)]
			: forcedDisplayName;
		string[] abilityPool = isMonster ? MonsterAbilities : NpcAbilities;
		string specialAbility = abilityPool[_rng.RandiRange(0, abilityPool.Length - 1)];
		string combatRole = string.IsNullOrWhiteSpace(forcedCombatRole)
			? isMonster ? MonsterSpeciesCatalog.Current.GetDefaultRole(displayName) : NpcRoles[_rng.RandiRange(0, NpcRoles.Length - 1)]
			: forcedCombatRole;
		string personality = Personalities[_rng.RandiRange(0, Personalities.Length - 1)];
		string passiveAbility = PassiveAbilities[_rng.RandiRange(0, PassiveAbilities.Length - 1)];
		const int initialAffinity = 5;

		actor.ConfigureStats(displayName, level, maxHealth, attack, defense, experience, gold);
		actor.ConfigureGrowth(specialAbility, _rng.RandiRange(1, 2));
		actor.ConfigureCombatProfile(combatRole, personality, passiveAbility, initialAffinity);
	}

	private void AddHorn(Node3D actor, Vector3 position, Vector3 rotationDegrees)
	{
		var horn = new MeshInstance3D
		{
			Name = "Horn",
			Mesh = CylinderMeshFor(0.0f, 0.12f, 0.38f),
			Position = position,
			RotationDegrees = rotationDegrees,
		};
		horn.SetSurfaceOverrideMaterial(0, _matHorn);
		actor.AddChild(horn);
	}

	private Vector3 FindOpenMonsterSpawnPosition()
	{
		float half = MapSize * 0.5f - 9.0f;

		for (int attempt = 0; attempt < 90; attempt++)
		{
			var position = new Vector3(
				(float)_rng.RandfRange(-half, half),
				0.0f,
				(float)_rng.RandfRange(-half, half)
			);

			if (position.DistanceTo(_spawnCampCenter) < 20.0f || position.DistanceTo(_mainCityCenter) < 26.0f)
			{
				continue;
			}

			if (IsPositionClear(position, 3.4f))
			{
				return position;
			}
		}

		return new Vector3((float)_rng.RandfRange(-half, half), 0.0f, (float)_rng.RandfRange(12.0f, half));
	}

	private Vector3 FindOpenBossSpawnPosition()
	{
		Vector3 fallback = FindOpenMonsterSpawnPosition();
		for (int attempt = 0; attempt < 14; attempt++)
		{
			Vector3 position = FindOpenMonsterSpawnPosition();
			fallback = position;
			if (position.DistanceTo(_wildSpawnPosition) >= 30.0f && IsPositionClear(position, 5.2f))
			{
				return position;
			}
		}

		return fallback;
	}

	private bool IsPositionClear(Vector3 position, float minDistance)
	{
		foreach (Vector3 obstaclePosition in _obstaclePositions)
		{
			if (obstaclePosition.DistanceTo(position) < minDistance)
			{
				return false;
			}
		}

		return true;
	}

	private void CreateMapPortal(string name, Vector3 position, string targetMapId, string labelKey)
	{
		bool isCityGate = targetMapId == "wild_select";
		float portalScale = isCityGate ? 1.55f : 1.0f;
		var portalGlowMaterial = MakeEmissiveMaterial(new Color(0.30f, 0.88f, 1.0f, isCityGate ? 0.70f : 0.58f), isCityGate ? 2.85f : 1.75f, 0.20f);
		var portalCoreMaterial = MakeEmissiveMaterial(new Color(0.62f, 0.42f, 1.0f, isCityGate ? 0.62f : 0.46f), isCityGate ? 2.25f : 1.25f, 0.15f);
		var portalSparkMaterial = MakeEmissiveMaterial(new Color(0.82f, 0.96f, 1.0f, 0.92f), isCityGate ? 3.6f : 2.1f, 0.10f);

		var portal = new StaticBody3D
		{
			Name = name,
			Position = position,
		};
		portal.AddToGroup("map_portal");
		portal.SetMeta("target_map", targetMapId);
		portal.SetMeta("label", labelKey);
		_propsRoot.AddChild(portal);

		AddMesh(portal, "PortalBase", CylinderMeshFor(1.45f * portalScale, 1.45f * portalScale, 0.16f), new Vector3(0.0f, 0.08f, 0.0f), Vector3.Zero, Vector3.One, _matRune);
		var groundAura = AddMesh(portal, "PortalGroundAura", CylinderMeshFor(2.25f * portalScale, 2.25f * portalScale, 0.025f), new Vector3(0.0f, 0.13f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalOuterRune", CylinderMeshFor(1.62f * portalScale, 1.62f * portalScale, 0.030f), new Vector3(0.0f, 0.155f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalGrandRune", CylinderMeshFor(2.88f * portalScale, 2.88f * portalScale, 0.018f), new Vector3(0.0f, 0.145f, 0.0f), Vector3.Zero, Vector3.One, portalCoreMaterial);
		var outerRing = AddMesh(portal, "PortalOuterHalo", CylinderMeshFor(1.12f * portalScale, 1.12f * portalScale, 0.028f), new Vector3(0.0f, 0.19f, 0.0f), Vector3.Zero, Vector3.One, portalCoreMaterial);
		var innerRing = AddMesh(portal, "PortalInnerHalo", CylinderMeshFor(0.68f * portalScale, 0.68f * portalScale, 0.032f), new Vector3(0.0f, 0.225f, 0.0f), Vector3.Zero, Vector3.One, portalGlowMaterial);
		AddMesh(portal, "PortalCenterGlow", new SphereMesh { Radius = 0.50f * portalScale, Height = 0.26f * portalScale }, new Vector3(0.0f, 0.32f, 0.0f), Vector3.Zero, new Vector3(1.25f, 0.24f, 1.25f), portalCoreMaterial);
		// City gate uses the same hexagram/ring/particle design as the wild
		// portals (just a larger, richer version) — no separate light column.
		AddPortalRuneStones(portal, portalGlowMaterial, portalScale);
		AddPortalHexagram(portal, portalSparkMaterial, portalScale);
		AddPortalParticles(portal, portalSparkMaterial, portalScale, isCityGate);

		var portalLight = new OmniLight3D
		{
			Name = "PortalLight",
			LightColor = new Color(0.45f, 0.86f, 1.0f),
			LightEnergy = isCityGate ? 4.2f : 1.8f,
			OmniRange = isCityGate ? 15.0f : 8.5f,
			Position = new Vector3(0.0f, isCityGate ? 1.6f : 0.75f, 0.0f),
		};
		portal.AddChild(portalLight);

		var effect = new MapPortalEffect
		{
			Name = "PortalEffect",
			OuterRing = outerRing,
			InnerRing = innerRing,
			GroundAura = groundAura,
			PortalLight = portalLight,
		};
		portal.AddChild(effect);

		var label = new Label3D
		{
			Name = "PortalLabel",
			Text = LocaleText.T(labelKey),
			Position = new Vector3(0.0f, isCityGate ? 2.72f : 1.65f, 0.0f),
			Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
			FontSize = isCityGate ? 28 : 22,
			PixelSize = 0.008f,
			OutlineSize = 6,
			HorizontalAlignment = HorizontalAlignment.Center,
			Width = 300.0f,
		};
		label.OutlineModulate = new Color(0.02f, 0.03f, 0.025f, 0.95f);
		label.Modulate = new Color(0.72f, 0.92f, 1.0f);
		portal.AddChild(label);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 0.35f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.8f * portalScale, Height = 0.7f },
		};
		portal.AddChild(collisionShape);
	}

	private void RefreshLocalizedWorldLabels()
	{
		foreach (Node node in GetTree().GetNodesInGroup("map_portal"))
		{
			if (node is not Node3D portal || !portal.HasMeta("label"))
			{
				continue;
			}

			Label3D? label = portal.GetNodeOrNull<Label3D>("PortalLabel");
			if (label == null)
			{
				continue;
			}

			string labelKey = portal.GetMeta("label").AsString();
			if (!string.IsNullOrWhiteSpace(labelKey))
			{
				label.Text = LocaleText.T(labelKey);
			}
		}
	}

	private void AddPortalParticles(Node3D portal, Material particleMaterial, float portalScale, bool isCityGate)
	{
		var particleMesh = new SphereMesh { Radius = isCityGate ? 0.065f : 0.045f, Height = isCityGate ? 0.13f : 0.09f };
		particleMesh.SurfaceSetMaterial(0, particleMaterial);

		var processMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 1.1f * portalScale,
			Direction = new Vector3(0.0f, 1.0f, 0.0f),
			Spread = isCityGate ? 76.0f : 58.0f,
			Gravity = new Vector3(0.0f, isCityGate ? 0.34f : 0.18f, 0.0f),
			InitialVelocityMin = isCityGate ? 0.75f : 0.35f,
			InitialVelocityMax = isCityGate ? 2.45f : 1.15f,
			AngularVelocityMin = -90.0f,
			AngularVelocityMax = 90.0f,
			ScaleMin = isCityGate ? 0.75f : 0.55f,
			ScaleMax = isCityGate ? 2.35f : 1.35f,
			Color = new Color(0.72f, 0.94f, 1.0f, 0.86f),
		};

		var risingParticles = new GpuParticles3D
		{
			Name = "PortalRisingParticles",
			Amount = isCityGate ? 180 : 72,
			Lifetime = isCityGate ? 3.4f : 2.2f,
			Randomness = 0.58f,
			Explosiveness = 0.0f,
			VisibilityAabb = isCityGate
				? new Aabb(new Vector3(-5.8f, -0.4f, -5.8f), new Vector3(11.6f, 8.2f, 11.6f))
				: new Aabb(new Vector3(-2.4f, -0.2f, -2.4f), new Vector3(4.8f, 4.2f, 4.8f)),
			ProcessMaterial = processMaterial,
			DrawPass1 = particleMesh,
			Emitting = true,
			Position = new Vector3(0.0f, 0.35f, 0.0f),
		};
		portal.AddChild(risingParticles);

		var orbitMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring,
			EmissionRingRadius = 1.25f * portalScale,
			EmissionRingInnerRadius = 0.85f * portalScale,
			EmissionRingHeight = isCityGate ? 0.42f : 0.12f,
			Direction = new Vector3(0.0f, 0.35f, 1.0f),
			Spread = 90.0f,
			Gravity = Vector3.Zero,
			InitialVelocityMin = isCityGate ? 0.28f : 0.12f,
			InitialVelocityMax = isCityGate ? 0.86f : 0.45f,
			ScaleMin = isCityGate ? 0.55f : 0.35f,
			ScaleMax = isCityGate ? 1.45f : 0.9f,
			Color = new Color(0.94f, 0.84f, 1.0f, 0.78f),
		};

		var orbitParticles = new GpuParticles3D
		{
			Name = "PortalOrbitParticles",
			Amount = isCityGate ? 110 : 42,
			Lifetime = isCityGate ? 2.4f : 1.7f,
			Randomness = 0.42f,
			VisibilityAabb = isCityGate
				? new Aabb(new Vector3(-5.2f, -0.6f, -5.2f), new Vector3(10.4f, 6.8f, 10.4f))
				: new Aabb(new Vector3(-2.2f, -0.4f, -1.6f), new Vector3(4.4f, 3.4f, 3.2f)),
			ProcessMaterial = orbitMaterial,
			DrawPass1 = particleMesh,
			Emitting = true,
			Position = new Vector3(0.0f, 0.34f, 0.0f),
			RotationDegrees = Vector3.Zero,
		};
		portal.AddChild(orbitParticles);
	}

	// Six-pointed star (hexagram) laid flat on the portal floor: two overlapping
	// triangles + a circumscribing ring, matching the "傳送點為六芒星地板" ask.
	private void AddPortalHexagram(Node3D portal, Material material, float portalScale)
	{
		float radius = 1.98f * portalScale;
		float y = 0.165f;
		AddHexagramTriangle(portal, material, radius, y, 90.0f, portalScale, "A");
		AddHexagramTriangle(portal, material, radius, y, 30.0f, portalScale, "B");
		AddMesh(
			portal,
			"HexRing",
			new TorusMesh { InnerRadius = radius * 0.99f, OuterRadius = radius * 1.03f, RingSegments = 6, Rings = 48 },
			new Vector3(0.0f, y, 0.0f),
			Vector3.Zero,
			Vector3.One,
			material);
	}

	private void AddHexagramTriangle(Node3D portal, Material material, float radius, float y, float startDegrees, float portalScale, string tag)
	{
		var vertices = new Vector3[3];
		for (int index = 0; index < 3; index++)
		{
			float angle = Mathf.DegToRad(startDegrees + index * 120.0f);
			vertices[index] = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
		}

		for (int index = 0; index < 3; index++)
		{
			Vector3 p0 = vertices[index];
			Vector3 p1 = vertices[(index + 1) % 3];
			Vector3 mid = (p0 + p1) * 0.5f;
			float length = p0.DistanceTo(p1);
			float yaw = Mathf.Atan2(-(p1.Z - p0.Z), p1.X - p0.X);
			AddMesh(
				portal,
				$"HexEdge{tag}{index}",
				BoxMeshFor(new Vector3(length, 0.03f, 0.10f * portalScale)),
				new Vector3(mid.X, y, mid.Z),
				new Vector3(0.0f, Mathf.RadToDeg(yaw), 0.0f),
				Vector3.One,
				material);
		}
	}

	private void AddPortalRuneStones(Node3D portal, Material material, float portalScale)
	{
		for (int index = 0; index < 12; index++)
		{
			float angle = index / 12.0f * Mathf.Tau;
			float radius = (index % 2 == 0 ? 1.42f : 1.18f) * portalScale;
			Vector3 position = new(Mathf.Cos(angle) * radius, 0.245f, Mathf.Sin(angle) * radius);
			AddMesh(
				portal,
				$"PortalRune{index}",
				BoxMeshFor(new Vector3(0.28f * portalScale, 0.026f, 0.07f * portalScale)),
				position,
				new Vector3(0.0f, Mathf.RadToDeg(-angle), 0.0f),
				Vector3.One,
				material
			);
		}
	}

	public void RequestMapTravel(string targetMapId)
	{
		RequestMapTravel(targetMapId, 0);
	}

	public void RequestMapTravel(string targetMapId, int requestedTier)
	{
		if (TryHandleCaveTravel(targetMapId))
		{
			return;
		}
		targetMapId = NormalizeMapId(targetMapId);
		if (!IsKnownMapId(targetMapId))
		{
			return;
		}

		bool tierChanged = requestedTier > 0 && ApplySelectedTier(targetMapId, requestedTier);
		if (_activeMapId == targetMapId && !tierChanged)
		{
			return;
		}

		SetMapVisibility(targetMapId);
		Vector3 spawnPosition = targetMapId == "city" ? CityPortalArrivalPosition : _wildSpawnPosition;
		if (_player != null && IsInstanceValid(_player))
		{
			_player.TeleportPartyTo(spawnPosition + new Vector3(0.0f, 0.2f, 0.0f));
			_player.PostSystemMessage(LocaleText.T(targetMapId == "city" ? "system.map.enter_city" : "system.map.enter_wild"), new Color(0.72f, 0.92f, 1.0f));
		}

		UpdateActorMapActivity();
		UpdateActiveBossHud(false);
		if (_player != null && IsInstanceValid(_player))
		{
			_player.RefreshBossWorldStatus(true);
		}
	}

	public SaveGameData ExportSaveData()
	{
		return new SaveGameData
		{
			WorldId = _worldSaveId,
			WorldName = _worldSaveName,
			WorldSeed = _activeWorldSeed,
			LastMode = NetworkManager.Instance is { IsOnline: true } ? "multiplayer" : "single",
			AutoSaveOnExit = _autoSaveOnExit,
			ActiveMapId = _activeMapId,
			PlayerPosition = ToSaveVector(_player.GlobalPosition),
			Player = _player.ExportSaveData(),
			UnlockedMapTiers = new Dictionary<string, int>(_wildMapUnlockedTiersById),
			SelectedMapTiers = new Dictionary<string, int>(_wildMapSelectedTiersById),
			PendingMail = NetworkManager.Instance?.ExportPendingMail() ?? new List<PendingMailSaveData>(),
		};
	}

	public override void _Notification(int what)
	{
		// App/window close: auto-save the world first if the player opted in.
		if (what == NotificationWMCloseRequest && _autoSaveOnExit
			&& NetworkManager.Instance is not { IsClient: true }
			&& _player != null && IsInstanceValid(_player))
		{
			_player.SaveGameToActiveWorld(false);
		}
	}

	// Persist a freshly-created world once so it shows up in the world list.
	private void AutoSaveNewWorld()
	{
		if (_player != null && IsInstanceValid(_player))
		{
			_player.SaveGameToActiveWorld(false);
		}
	}

	private void LoadRequestedSave()
	{
		if (!SaveGameManager.TryLoad(GameLaunchOptions.ActiveWorldId, out SaveGameData data, out string error))
		{
			_player.PostSystemMessage(LocaleText.F("system.load.failed", error), new Color(1.0f, 0.42f, 0.34f));
			return;
		}

		ApplySaveData(data);
		_player.PostSystemMessage(LocaleText.T("system.load.success"), new Color(0.72f, 1.0f, 0.78f));
	}

	private void ApplySaveData(SaveGameData data)
	{
		string mapId = NormalizeMapId(data.ActiveMapId);
		EnsureSavedCaveMap(mapId);
		if (!IsKnownMapId(mapId))
		{
			mapId = "city";
		}

		// Restore tier progression, then re-roll any map whose living population
		// was spawned at a different tier than the save selects.
		_wildMapUnlockedTiersById.Clear();
		_wildMapSelectedTiersById.Clear();
		if (data.UnlockedMapTiers != null)
		{
			foreach (KeyValuePair<string, int> entry in data.UnlockedMapTiers)
			{
				if (IsWildMapId(entry.Key))
				{
					_wildMapUnlockedTiersById[entry.Key] = WorldTierCatalog.ClampTier(entry.Value);
				}
			}
		}
		if (data.SelectedMapTiers != null)
		{
			foreach (KeyValuePair<string, int> entry in data.SelectedMapTiers)
			{
				if (IsWildMapId(entry.Key))
				{
					_wildMapSelectedTiersById[entry.Key] = WorldTierCatalog.ClampTier(entry.Value);
				}
			}
		}
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			EnsureWildInstancePopulated(wildMap.Id, GetSelectedTier(wildMap.Id));
		}
		DespawnInactiveWildInstances();

		SetMapVisibility(mapId);
		var loadedCompanions = new List<SimpleActor>();
		foreach (ActorSaveData actorData in data.Player.Companions)
		{
			SimpleActor actor = CreateActor(actorData.ActorKind == "monster");
			actor.Position = FromSaveVector(actorData.IsAwaitingRecovery ? actorData.WorldPosition : data.PlayerPosition);
			actor.HomePosition = actor.Position;
			_actorsRoot.AddChild(actor);
			actor.ApplySaveData(actorData);
			loadedCompanions.Add(actor);
		}

		_player.ApplySaveData(data.Player, loadedCompanions);
		_player.TeleportPartyTo(FromSaveVector(data.PlayerPosition));
		NetworkManager.Instance?.ImportPendingMail(data.PendingMail);
		// Preserve the loaded world's identity so re-saving keeps name/seed.
		if (!string.IsNullOrWhiteSpace(data.WorldName))
		{
			_worldSaveName = data.WorldName;
		}
		if (data.WorldSeed != 0)
		{
			_activeWorldSeed = data.WorldSeed;
		}
		_autoSaveOnExit = data.AutoSaveOnExit;
		UpdateActorMapActivity();
	}

	private static SaveVector3 ToSaveVector(Vector3 vector)
	{
		return new SaveVector3
		{
			X = vector.X,
			Y = vector.Y,
			Z = vector.Z,
		};
	}

	private static Vector3 FromSaveVector(SaveVector3 vector)
	{
		return new Vector3(vector.X, vector.Y, vector.Z);
	}

	private void SetMapVisibility(string mapId)
	{
		_activeMapId = mapId;
		_mapTravelCooldownRemaining = MapTravelCooldownSeconds;
		ApplyMapAtmosphere(mapId);
		_musicPlayer?.PlayForMap(mapId);
		if (_cityMapRoot != null)
		{
			SetMapRootActive(_cityMapRoot, mapId == "city");
		}

		foreach (KeyValuePair<string, Node3D> entry in _wildMapRootsById)
		{
			SetMapRootActive(entry.Value, mapId == entry.Key);
		}

		foreach (KeyValuePair<string, Node3D> entry in _caveMapRootsById)
		{
			SetMapRootActive(entry.Value, mapId == entry.Key);
		}

		if (_player != null && IsInstanceValid(_player))
		{
			_player.RefreshFallenCompanionMapVisibility(_activeMapId);
			_player.RefreshMinimap();
		}
	}

	private void SetMapRootActive(Node3D root, bool active)
	{
		root.Visible = active;
		root.ProcessMode = active ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
		SetMapCollisionActive(root, active);
	}

	private void SetMapCollisionActive(Node node, bool active)
	{
		if (node is CollisionObject3D collisionObject)
		{
			if (!_mapCollisionDefaults.ContainsKey(collisionObject))
			{
				_mapCollisionDefaults[collisionObject] = (collisionObject.CollisionLayer, collisionObject.CollisionMask);
			}

			if (active)
			{
				(uint layer, uint mask) = _mapCollisionDefaults[collisionObject];
				collisionObject.CollisionLayer = layer;
				collisionObject.CollisionMask = mask;
			}
			else
			{
				collisionObject.CollisionLayer = 0;
				collisionObject.CollisionMask = 0;
			}
		}

		foreach (Node child in node.GetChildren())
		{
			SetMapCollisionActive(child, active);
		}
	}

	private void UpdateActorMapActivity()
	{
		bool cityActive = _activeMapId == "city";
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is SimpleActor actor && IsInstanceValid(actor))
			{
				actor.SetWorldMapActive(IsActorInstanceActive(actor));
			}
		}

		foreach (Node node in GetTree().GetNodesInGroup("npcs"))
		{
			if (node is SimpleActor actor && IsInstanceValid(actor))
			{
				actor.SetWorldMapActive(cityActive);
			}
		}
	}

	private void UpdateActiveBossHud(bool announce)
	{
		if (_player == null || !IsInstanceValid(_player))
		{
			return;
		}

		if (_wildBossesByInstance.TryGetValue(GetLocalWildInstanceKey(_activeMapId), out SimpleActor? boss)
			&& IsInstanceValid(boss)
			&& !boss.IsDefeated)
		{
			_player.SetActiveBoss(boss);
			if (announce)
			{
				_player.ShowBossAppeared(boss, GetWildMapDisplayName(_activeMapId));
			}
			return;
		}

		_player.SetActiveBoss(null);
	}

	private string GetWildMapDisplayName(string mapId)
	{
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			if (wildMap.Id == mapId)
			{
				return LocaleText.T(wildMap.NameKey);
			}
		}

		return mapId;
	}

	public IReadOnlyList<BossStatusSnapshot> GetBossStatusSnapshots()
	{
		var snapshots = new List<BossStatusSnapshot>(WildBosses.Length);
		foreach (BossDefinition definition in WildBosses)
		{
			// The HUD reflects the local player's instance of each map.
			string instanceKey = GetLocalWildInstanceKey(definition.MapId);
			bool alive = _wildBossesByInstance.TryGetValue(instanceKey, out SimpleActor? boss)
				&& IsInstanceValid(boss)
				&& !boss.IsDefeated;
			float remaining = 0.0f;
			if (!alive)
			{
				remaining = _wildBossRespawnRemainingByInstance.TryGetValue(instanceKey, out float savedRemaining)
					? savedRemaining
					: Mathf.Max(BossRespawnInterval, 15.0f);
			}

			snapshots.Add(new BossStatusSnapshot(
				definition.MapId,
				GetWildMapDisplayName(definition.MapId),
				LocaleText.T(definition.NameKey),
				alive,
				alive ? 0 : Mathf.Max(Mathf.CeilToInt(remaining), 1)));
		}

		return snapshots;
	}

	private void UpdateMonsterRespawns(float step)
	{
		if (_actorsRoot == null || WildMaps.Length == 0 || IsNetworkClientWorld)
		{
			return;
		}

		_monsterRespawnRemaining -= step;
		if (_monsterRespawnRemaining > 0.0f)
		{
			return;
		}

		_monsterRespawnRemaining = Mathf.Max(MonsterRespawnInterval, 3.0f);
		DespawnInactiveWildInstances();
		foreach (KeyValuePair<string, (string MapId, int Tier)> entry in _spawnedWildInstancesByKey)
		{
			RespawnMonstersIfNeeded(entry.Value.MapId, entry.Value.Tier);
		}
	}

	private void UpdateWildBosses(float step)
	{
		if (_actorsRoot == null || WildBosses.Length == 0 || IsNetworkClientWorld)
		{
			return;
		}

		// One boss per populated (map, tier) instance.
		foreach (KeyValuePair<string, (string MapId, int Tier)> instanceEntry in _spawnedWildInstancesByKey)
		{
			string instanceKey = instanceEntry.Key;
			(string mapId, int tier) = instanceEntry.Value;
			BossDefinition? definition = FindBossDefinition(mapId);
			if (definition == null)
			{
				continue;
			}

			bool bossAlive = _wildBossesByInstance.TryGetValue(instanceKey, out SimpleActor? boss)
				&& IsInstanceValid(boss)
				&& !boss.IsDefeated;
			if (bossAlive)
			{
				_wildBossRespawnRemainingByInstance.Remove(instanceKey);
				continue;
			}

			if (!_wildBossRespawnRemainingByInstance.TryGetValue(instanceKey, out float remaining))
			{
				_wildBossRespawnRemainingByInstance[instanceKey] = Mathf.Max(BossRespawnInterval, 15.0f);
				if (mapId == _activeMapId && tier == GetSelectedTier(mapId))
				{
					_player.SetActiveBoss(null);
				}
				_player.RefreshBossWorldStatus(false);
				continue;
			}

			remaining -= step;
			if (remaining > 0.0f)
			{
				_wildBossRespawnRemainingByInstance[instanceKey] = remaining;
				continue;
			}

			SpawnBossForMap(definition.Value, tier, true);
		}
	}

	private static BossDefinition? FindBossDefinition(string mapId)
	{
		foreach (BossDefinition definition in WildBosses)
		{
			if (definition.MapId == mapId)
			{
				return definition;
			}
		}

		return null;
	}

	private void RespawnMonstersIfNeeded(string mapId, int tier)
	{
		int targetCount = GetWildMonsterTargetCount(mapId);
		int livingCount = CountLivingMonstersInInstance(mapId, tier, false);
		int threshold = Mathf.Max(1, Mathf.FloorToInt(targetCount * Mathf.Clamp(MonsterRespawnThresholdRatio, 0.1f, 0.95f)));
		if (livingCount >= threshold)
		{
			return;
		}

		int spawnCount = Mathf.Min(targetCount - livingCount, Mathf.Max(MonsterRespawnBatchSize, 1));
		if (spawnCount <= 0)
		{
			return;
		}

		UseWildMapObstacleContext(mapId);
		for (int index = 0; index < spawnCount; index++)
		{
			SpawnMonsterForMap(mapId, tier);
		}
	}

	private int GetWildMonsterTargetCount(string mapId)
	{
		if (_wildMonsterTargetCountsById.TryGetValue(mapId, out int targetCount))
		{
			return Mathf.Max(targetCount, 1);
		}

		return Mathf.Max(ActorCount / Mathf.Max(WildMaps.Length, 1), 8);
	}

	private int CountLivingMonsters(string mapId, bool includeBosses = true)
	{
		int count = 0;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is SimpleActor actor
				&& IsInstanceValid(actor)
				&& actor.MapId == mapId
				&& !actor.IsDefeated
				&& !actor.IsCaptured
				&& (includeBosses || !actor.IsBoss))
			{
				count++;
			}
		}

		return count;
	}

	private int CountLivingMonstersInInstance(string mapId, int tier, bool includeBosses = true)
	{
		int count = 0;
		foreach (Node node in GetTree().GetNodesInGroup("monsters"))
		{
			if (node is SimpleActor actor
				&& IsInstanceValid(actor)
				&& actor.MapId == mapId
				&& actor.WorldTier == tier
				&& !actor.IsDefeated
				&& !actor.IsCaptured
				&& (includeBosses || !actor.IsBoss))
			{
				count++;
			}
		}

		return count;
	}

	public IReadOnlyList<(string Id, string Label)> GetWildMapTravelOptions()
	{
		var options = new List<(string Id, string Label)>();
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			options.Add((wildMap.Id, LocaleText.T(wildMap.NameKey)));
		}

		return options;
	}

	public IReadOnlyList<(string Id, string Label, int UnlockedTier, int SelectedTier)> GetWildMapTravelTierOptions()
	{
		var options = new List<(string Id, string Label, int UnlockedTier, int SelectedTier)>();
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			options.Add((wildMap.Id, LocaleText.T(wildMap.NameKey), GetUnlockedTier(wildMap.Id), GetSelectedTier(wildMap.Id)));
		}

		return options;
	}

	// One row per tier (1..Max) for a map, given a player level. Shared by the
	// portal travel dialog and the M-key world map. Unlock is purely
	// progression-based: a tier is available only after the previous tier's boss
	// has been defeated — NOT gated by player or monster level. The level range
	// is shown for information only. playerLevel is unused (kept for callers).
	public readonly record struct TierMenuEntry(
		int Tier, int LevelMin, int LevelMax,
		bool Unlocked, bool Available, bool IsSelected);

	public IReadOnlyList<TierMenuEntry> GetTierMenu(string mapId, int playerLevel)
	{
		mapId = GetTierMapId(mapId);
		int unlockedTier = GetUnlockedTier(mapId);
		int selectedTier = GetSelectedTier(mapId);
		var entries = new List<TierMenuEntry>(WorldTierCatalog.MaxTier);
		for (int tier = WorldTierCatalog.MinTier; tier <= WorldTierCatalog.MaxTier; tier++)
		{
			(int min, int max) = WorldTierCatalog.GetMonsterLevelRange(tier);
			bool unlocked = tier <= unlockedTier;
			entries.Add(new TierMenuEntry(tier, min, max, unlocked, unlocked, tier == selectedTier));
		}

		return entries;
	}

	public IReadOnlyList<(string Id, string Label)> GetWildMapList()
	{
		var list = new List<(string Id, string Label)>();
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			list.Add((wildMap.Id, LocaleText.T(wildMap.NameKey)));
		}

		return list;
	}

	public int GetUnlockedTier(string mapId)
	{
		mapId = GetTierMapId(mapId);
		return _wildMapUnlockedTiersById.TryGetValue(mapId, out int tier)
			? WorldTierCatalog.ClampTier(tier)
			: WorldTierCatalog.MinTier;
	}

	public int GetSelectedTier(string mapId)
	{
		mapId = GetTierMapId(mapId);
		int selected = _wildMapSelectedTiersById.TryGetValue(mapId, out int tier)
			? WorldTierCatalog.ClampTier(tier)
			: WorldTierCatalog.MinTier;
		return Mathf.Min(selected, GetUnlockedTier(mapId));
	}

	// Caves inherit the tier of the wild map that owns them ("<wildId>_cave_...").
	private static string GetTierMapId(string mapId)
	{
		int caveIndex = mapId.IndexOf("_cave_", System.StringComparison.Ordinal);
		return caveIndex > 0 ? mapId[..caveIndex] : mapId;
	}

	private bool IsWildMapId(string mapId)
	{
		foreach (WildMapDefinition wildMap in WildMaps)
		{
			if (wildMap.Id == mapId)
			{
				return true;
			}
		}

		return false;
	}

	// Public wrapper for UI (e.g. the wild return-portal choice dialog).
	public bool IsWildMap(string mapId)
	{
		return IsWildMapId(mapId);
	}

	private static string WildInstanceKey(string mapId, int tier)
	{
		return $"{mapId}#t{tier}";
	}

	// The instance key of THIS player's view of a map (their selected tier).
	private string GetLocalWildInstanceKey(string mapId)
	{
		return WildInstanceKey(mapId, GetSelectedTier(mapId));
	}

	// Selecting a tier is a per-player choice: it never despawns other tiers'
	// populations (other players may be in them) — it just points this player
	// at a different parallel instance and makes sure it's populated.
	// Returns true when the selection changed.
	private bool ApplySelectedTier(string mapId, int requestedTier)
	{
		mapId = GetTierMapId(mapId);
		if (!IsWildMapId(mapId))
		{
			return false;
		}

		// Only progression matters: clamp to the highest tier unlocked by
		// defeating bosses. No player/monster level gate.
		int tier = Mathf.Clamp(requestedTier, WorldTierCatalog.MinTier, GetUnlockedTier(mapId));
		int previousTier = GetSelectedTier(mapId);
		_wildMapSelectedTiersById[mapId] = tier;
		if (tier == previousTier)
		{
			return false;
		}

		EnsureWildInstancePopulated(mapId, tier);
		DespawnInactiveWildInstances();
		UpdateActiveBossHud(false);
		if (_player != null && IsInstanceValid(_player))
		{
			_player.PostSystemMessage(LocaleText.F("system.tier.applied", GetWildMapDisplayName(mapId), tier), new Color(0.72f, 0.92f, 1.0f));
		}
		return true;
	}

	// Host/singleplayer: make sure the (map, tier) instance has a population.
	// No-op on multiplayer clients (the host simulates and streams puppets).
	// Also called by NetworkManager when a remote player enters an instance.
	public void EnsureWildInstancePopulated(string mapId, int tier)
	{
		if (IsNetworkClientWorld || !_worldActorsGenerated || !IsWildMapId(mapId))
		{
			return;
		}

		tier = WorldTierCatalog.ClampTier(tier);
		string instanceKey = WildInstanceKey(mapId, tier);
		if (_spawnedWildInstancesByKey.ContainsKey(instanceKey))
		{
			return;
		}

		_spawnedWildInstancesByKey[instanceKey] = (mapId, tier);
		UseWildMapObstacleContext(mapId);
		int targetCount = GetWildMonsterTargetCount(mapId);
		for (int index = 0; index < targetCount; index++)
		{
			SpawnMonsterForMap(mapId, tier);
		}

		BossDefinition? definition = FindBossDefinition(mapId);
		if (definition != null)
		{
			SpawnBossForMap(definition.Value, tier, false);
		}
	}

	// Frees populations no player is using. An instance stays alive while it is
	// some player's current selection for that map (local player) or a remote
	// player is standing in it.
	private void DespawnInactiveWildInstances()
	{
		if (IsNetworkClientWorld)
		{
			return;
		}

		_instanceCleanupScratch.Clear();
		foreach (KeyValuePair<string, (string MapId, int Tier)> entry in _spawnedWildInstancesByKey)
		{
			if (!IsWildInstanceInUse(entry.Value.MapId, entry.Value.Tier))
			{
				_instanceCleanupScratch.Add(entry.Key);
			}
		}

		foreach (string instanceKey in _instanceCleanupScratch)
		{
			(string mapId, int tier) = _spawnedWildInstancesByKey[instanceKey];
			_spawnedWildInstancesByKey.Remove(instanceKey);
			_wildBossesByInstance.Remove(instanceKey);
			_wildBossRespawnRemainingByInstance.Remove(instanceKey);
			foreach (Node node in GetTree().GetNodesInGroup("monsters"))
			{
				if (node is SimpleActor actor
					&& IsInstanceValid(actor)
					&& actor.MapId == mapId
					&& actor.WorldTier == tier
					&& !actor.IsCaptured)
				{
					actor.QueueFree();
				}
			}
		}
	}

	private bool IsWildInstanceInUse(string mapId, int tier)
	{
		if (GetSelectedTier(mapId) == tier)
		{
			return true;
		}

		return NetworkManager.Instance is { IsHost: true } net && net.IsRemoteInstanceInUse(mapId, tier);
	}

	// Shared unlock rule: beating a map's boss at your highest unlocked tier
	// unlocks the next tier for YOU (per-player progression, saved locally).
	public bool TryUnlockNextTier(string mapId, int bossTier)
	{
		mapId = GetTierMapId(mapId);
		if (!IsWildMapId(mapId))
		{
			return false;
		}

		int unlockedTier = GetUnlockedTier(mapId);
		if (bossTier < unlockedTier || unlockedTier >= WorldTierCatalog.MaxTier)
		{
			return false;
		}

		_wildMapUnlockedTiersById[mapId] = unlockedTier + 1;
		if (_player != null && IsInstanceValid(_player))
		{
			_player.PostSystemMessage(LocaleText.F("system.tier.unlocked", GetWildMapDisplayName(mapId), unlockedTier + 1), new Color(1.0f, 0.9f, 0.45f));
		}
		return true;
	}

	// Called when the LOCAL player's party defeats a wild boss (remote players'
	// kills unlock via a network RPC to their own machine instead).
	public void OnWildBossDefeated(SimpleActor boss)
	{
		TryUnlockNextTier(boss.MapId, boss.WorldTier);
	}

	private bool IsKnownMapId(string mapId)
	{
		return mapId == "city" || _wildMapRootsById.ContainsKey(mapId) || _caveMapRootsById.ContainsKey(mapId);
	}

	private static string NormalizeMapId(string mapId)
	{
		return mapId == "wild" ? "wild_forest" : mapId;
	}

	private void CreateTree(Vector3 position)
	{
		var tree = new StaticBody3D
		{
			Name = "Tree",
			Position = position,
		};
		_propsRoot.AddChild(tree);

		if (ExternalModelLibrary.TryAddPropModel(tree, "tree", unchecked((int)_rng.Randi()), Vector3.Zero, new Vector3(1.15f, 1.15f, 1.15f)))
		{
			var modelCollisionShape = new CollisionShape3D
			{
				Position = new Vector3(0.0f, 1.35f, 0.0f),
				Shape = new BoxShape3D { Size = new Vector3(0.95f, 2.7f, 0.95f) },
			};
			tree.AddChild(modelCollisionShape);
			return;
		}

		var trunk = new MeshInstance3D
		{
			Name = "Trunk",
			Mesh = CylinderMeshFor(0.32f, 0.42f, 2.6f),
			Position = new Vector3(0.0f, 1.3f, 0.0f),
		};
		trunk.SetSurfaceOverrideMaterial(0, _matTrunk);
		tree.AddChild(trunk);

		AddMesh(tree, "RootA", new CapsuleMesh { Radius = 0.08f, Height = 1.35f }, new Vector3(0.52f, 0.18f, 0.08f), new Vector3(86.0f, 78.0f, 0.0f), Vector3.One, _matTrunk);
		AddMesh(tree, "RootB", new CapsuleMesh { Radius = 0.07f, Height = 1.1f }, new Vector3(-0.46f, 0.18f, -0.08f), new Vector3(86.0f, -63.0f, 0.0f), Vector3.One, _matTrunk);
		AddMesh(tree, "BranchA", new CapsuleMesh { Radius = 0.07f, Height = 1.2f }, new Vector3(0.42f, 2.25f, 0.0f), new Vector3(58.0f, 34.0f, -30.0f), Vector3.One, _matTrunk);
		AddMesh(tree, "BranchB", new CapsuleMesh { Radius = 0.06f, Height = 1.05f }, new Vector3(-0.38f, 2.05f, 0.02f), new Vector3(60.0f, -42.0f, 28.0f), Vector3.One, _matTrunk);

		float crownRadius = (float)_rng.RandfRange(1.25f, 1.95f);
		var crown = new MeshInstance3D
		{
			Name = "Crown",
			Mesh = new SphereMesh { Radius = crownRadius, Height = crownRadius * 1.7f },
			Position = new Vector3(0.0f, 3.0f, 0.0f),
			Scale = new Vector3(1.0f, (float)_rng.RandfRange(0.85f, 1.2f), 1.0f),
		};
		crown.SetSurfaceOverrideMaterial(0, _matLeaf);
		tree.AddChild(crown);
		AddMesh(tree, "CrownLeft", new SphereMesh { Radius = crownRadius * 0.62f, Height = crownRadius * 0.92f }, new Vector3(-0.78f, 2.82f, 0.16f), Vector3.Zero, new Vector3(1.0f, 0.82f, 1.0f), _matGrassDark);
		AddMesh(tree, "CrownRight", new SphereMesh { Radius = crownRadius * 0.55f, Height = crownRadius * 0.86f }, new Vector3(0.82f, 2.7f, -0.12f), Vector3.Zero, new Vector3(1.0f, 0.78f, 1.0f), _matGrassBright);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, 1.35f, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(0.95f, 2.7f, 0.95f) },
		};
		tree.AddChild(collisionShape);
	}

	private void CreateRock(Vector3 position)
	{
		var rock = new StaticBody3D
		{
			Name = "Rock",
			Position = position,
		};
		_propsRoot.AddChild(rock);

		var size = new Vector3(
			(float)_rng.RandfRange(1.0f, 2.6f),
			(float)_rng.RandfRange(0.6f, 1.4f),
			(float)_rng.RandfRange(1.0f, 2.4f)
		);

		if (ExternalModelLibrary.TryAddPropModel(rock, "rock", unchecked((int)_rng.Randi()), Vector3.Zero, new Vector3(size.X * 0.55f, size.Y * 0.75f, size.Z * 0.55f)))
		{
			var modelCollisionShape = new CollisionShape3D
			{
				Position = new Vector3(0.0f, size.Y * 0.5f, 0.0f),
				Shape = new BoxShape3D { Size = size },
			};
			rock.AddChild(modelCollisionShape);
			return;
		}

		var meshInstance = new MeshInstance3D
		{
			Name = "RockMesh",
			Mesh = BoxMeshFor(size),
			Position = new Vector3(0.0f, size.Y * 0.5f, 0.0f),
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		meshInstance.SetSurfaceOverrideMaterial(0, _matRock);
		rock.AddChild(meshInstance);
		AddMesh(rock, "MossA", BoxMeshFor(new Vector3(size.X * 0.45f, 0.035f, size.Z * 0.28f)), new Vector3(-size.X * 0.12f, size.Y + 0.025f, -size.Z * 0.08f), new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f), Vector3.One, _matGrassDark);
		AddMesh(rock, "MossB", BoxMeshFor(new Vector3(size.X * 0.24f, 0.03f, size.Z * 0.22f)), new Vector3(size.X * 0.18f, size.Y + 0.04f, size.Z * 0.12f), new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f), Vector3.One, _matGrassBright);
		AddMesh(rock, "PebbleA", new SphereMesh { Radius = 0.18f, Height = 0.24f }, new Vector3(size.X * 0.55f, 0.12f, -size.Z * 0.45f), Vector3.Zero, new Vector3(1.3f, 0.48f, 1.0f), _matRock);
		AddMesh(rock, "PebbleB", new SphereMesh { Radius = 0.13f, Height = 0.18f }, new Vector3(-size.X * 0.52f, 0.09f, size.Z * 0.42f), Vector3.Zero, new Vector3(1.0f, 0.5f, 1.3f), _matRock);

		var collisionShape = new CollisionShape3D
		{
			Position = new Vector3(0.0f, size.Y * 0.5f, 0.0f),
			Shape = new BoxShape3D { Size = size },
		};
		rock.AddChild(collisionShape);
	}

	private StaticBody3D CreateStaticBox(Node parent, string nodeName, Vector3 position, Vector3 size, Material material)
	{
		var body = new StaticBody3D
		{
			Name = nodeName,
			Position = position,
		};
		parent.AddChild(body);

		var meshInstance = new MeshInstance3D
		{
			Name = "Mesh",
			Mesh = BoxMeshFor(size),
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		body.AddChild(meshInstance);

		var collisionShape = new CollisionShape3D
		{
			Shape = new BoxShape3D { Size = size },
		};
		body.AddChild(collisionShape);

		return body;
	}

	private StaticBody3D CreateExternalProp(string nodeName, string modelPath, Vector3 position, Vector3 rotationDegrees, Vector3 modelScale, Vector3 collisionSize, Vector3 collisionPosition)
	{
		var body = new StaticBody3D
		{
			Name = nodeName,
			Position = position,
			RotationDegrees = rotationDegrees,
		};
		_propsRoot.AddChild(body);

		if (!ExternalModelLibrary.TryAddModel(body, modelPath, "ExternalModel", Vector3.Zero, Vector3.Zero, modelScale))
		{
			AddMesh(body, "FallbackMesh", BoxMeshFor(collisionSize), collisionPosition, Vector3.Zero, Vector3.One, _matWood);
		}

		var collisionShape = new CollisionShape3D
		{
			Position = collisionPosition,
			Shape = new BoxShape3D { Size = collisionSize },
		};
		body.AddChild(collisionShape);

		return body;
	}

	private void AddExternalModelTo(Node3D parent, string modelPath, string nodeName, Vector3 position, Vector3 rotationDegrees, Vector3 scale)
	{
		if (!ExternalModelLibrary.TryAddModel(parent, modelPath, nodeName, position, rotationDegrees, scale))
		{
			AddMesh(parent, nodeName, BoxMeshFor(new Vector3(1.0f, 1.0f, 0.12f)), position + new Vector3(0.0f, 0.5f, 0.0f), rotationDegrees, Vector3.One, _matWood);
		}
	}

	private MeshInstance3D CreateMesh(Node parent, string nodeName, Mesh mesh, Vector3 position, Material material)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = mesh,
			Position = position,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		parent.AddChild(meshInstance);
		return meshInstance;
	}

	private MeshInstance3D CreateTerrainPatch(string nodeName, Vector3 position, float radius, Vector3 scale, float yawDegrees, Material material, float height)
	{
		var meshInstance = new MeshInstance3D
		{
			Name = nodeName,
			Mesh = CylinderMeshFor(radius, radius, height),
			Position = position + new Vector3(0.0f, height * 0.5f, 0.0f),
			RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f),
			Scale = scale,
		};
		meshInstance.SetSurfaceOverrideMaterial(0, material);
		_mapRoot.AddChild(meshInstance);
		return meshInstance;
	}

	private void AddCrosshair()
	{
		var layer = new CanvasLayer { Name = "HUD" };
		AddChild(layer);

		var reticle = new Panel
		{
			Name = "ReticleDot",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -3.0f,
			OffsetRight = 3.0f,
			OffsetTop = -3.0f,
			OffsetBottom = 3.0f,
		};
		var dotStyle = new StyleBoxFlat
		{
			BgColor = new Color(1.0f, 1.0f, 1.0f, 0.72f),
			BorderColor = new Color(0.04f, 0.05f, 0.06f, 0.62f),
		};
		dotStyle.SetBorderWidthAll(1);
		dotStyle.SetCornerRadiusAll(3);
		reticle.AddThemeStyleboxOverride("panel", dotStyle);
		layer.AddChild(reticle);
	}

	private static BoxMesh BoxMeshFor(Vector3 size)
	{
		return new BoxMesh { Size = size };
	}

	private static CylinderMesh CylinderMeshFor(float topRadius, float bottomRadius, float height)
	{
		return new CylinderMesh
		{
			TopRadius = topRadius,
			BottomRadius = bottomRadius,
			Height = height,
			RadialSegments = 24,
		};
	}

	private static StandardMaterial3D MakeMaterial(Color color, float roughness = 0.85f)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};

		if (color.A < 1.0f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}

	private static StandardMaterial3D MakeEmissiveMaterial(Color color, float emissionEnergy, float roughness = 0.35f)
	{
		return new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			BlendMode = BaseMaterial3D.BlendModeEnum.Add,
			EmissionEnabled = true,
			Emission = color,
			EmissionEnergyMultiplier = emissionEnergy,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		};
	}
}
