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
	// Five maps receive 18 monsters each (90 total).
	[Export] public int ActorCount { get; set; } = 90;
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
	private readonly Dictionary<string, (string MapId, int Tier, int GroupId)> _spawnedWildInstancesByKey = new();
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
		return GetMapDisplayName(_activeMapId);
	}

	// Localized display name for any map id (used by the multiplayer party list).
	public string GetMapDisplayName(string mapId)
	{
		if (IsCaveMapId(mapId))
		{
			return GetCaveMapDisplayName(mapId);
		}

		return mapId == "city"
			? LocaleText.T("map.city")
			: GetWildMapDisplayName(mapId);
	}
	public int CurrentLivingMonsterCount
	{
		get
		{
			foreach (WildMapDefinition wildMap in WildMaps)
			{
				if (wildMap.Id == _activeMapId)
				{
					return CountLivingMonstersInInstance(_activeMapId, GetSelectedTier(_activeMapId), LocalGroupId());
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
		new("wild_skeleton", "map.wild.skeleton", "WildSkeletonMap", new Color(0.22f, 0.20f, 0.27f)),
	};

	private static readonly BossDefinition[] WildBosses =
	{
		new("wild_forest", "boss.forest.name", "name.monster.boar", "DPS", "loot.beast_hide", 12, 1450, 52, 27, 190, 120, 2.78f, new Color(0.42f, 0.92f, 0.30f, 0.94f)),
		new("wild_marsh", "boss.marsh.name", "name.monster.slime", "Support", "loot.water_core", 14, 1750, 58, 34, 230, 150, 3.05f, new Color(0.24f, 0.88f, 0.82f, 0.94f)),
		new("wild_badlands", "boss.badlands.name", "name.monster.lion", "DPS", "loot.red_horn", 17, 2250, 76, 40, 310, 210, 2.92f, new Color(1.0f, 0.32f, 0.06f, 0.96f)),
		new("wild_snow", "boss.snow.name", "name.monster.bear", "Tank", "loot.dragon_scale", 19, 2750, 72, 55, 380, 260, 3.15f, new Color(0.54f, 0.86f, 1.0f, 0.96f)),
		new("wild_skeleton", "boss.skeleton.name", "name.monster.skeleton_warrior", "Tank", "loot.cracked_core", 22, 3450, 88, 68, 470, 330, 3.55f, new Color(0.68f, 0.42f, 1.0f, 0.96f)),
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
		PreGameMusic.Stop(this);
		LocaleText.LanguageChanged += RefreshLocalizedWorldLabels;
		if (NetworkManager.Instance is { } net)
		{
			net.PartyChanged += OnLocalPartyChanged;
		}

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
		// Multiplayer: mirror the player's chosen character (name + model) to every
		// peer BEFORE announcing world-ready, so the host labels/renders this player
		// correctly and the join message uses the right name. The name is only sent
		// if actually chosen (otherwise the unique default name is kept so two
		// default players don't collide); the model is always sent.
		if (NetworkManager.Instance is { IsOnline: true } onlineNet && _player != null && IsInstanceValid(_player))
		{
			if (!string.IsNullOrWhiteSpace(_player.PlayerName) && _player.PlayerName != "player.default_name")
			{
				onlineNet.SetLocalPlayerName(LocaleText.T(_player.PlayerName));
			}

			onlineNet.SetLocalPlayerModel(_player.PlayerModelPath);
		}

		NetworkAfterWorldReady();
		_musicPlayer.PlayForMap(_activeMapId);
		LoadingScreen.Hide(this);
	}

	public override void _ExitTree()
	{
		LocaleText.LanguageChanged -= RefreshLocalizedWorldLabels;
		if (NetworkManager.Instance is { } net)
		{
			net.PartyChanged -= OnLocalPartyChanged;
		}
		NetworkOnWorldExit();
	}

	// The local player's party (and therefore their GroupId) changed. On the host
	// side, make sure the new group's wild instance exists and refresh which
	// monsters simulate/are visible so the player instantly shares — or leaves —
	// a hunting ground. Clients only need their visibility refreshed; the host
	// drives their monster spawns.
	private void OnLocalPartyChanged()
	{
		if (IsWildMapId(_activeMapId) && !IsNetworkClientWorld)
		{
			EnsureWildInstancePopulated(_activeMapId, GetSelectedTier(_activeMapId), LocalGroupId());
		}

		UpdateActorMapActivity();
		UpdateActiveBossHud(false);
	}

	public override void _Process(double delta)
	{
		float step = (float)delta;
		UpdateMapTravelCooldown(step);
		UpdateMonsterRespawns(step);
		UpdateWildBosses(step);
		UpdateCaveRespawns(step);
		UpdateRuntimeCleanup(step);
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
		CreatePrototypeGround(wildMap.Id);
		CreateBoundaries();
		CreateLandmarks();
		CreateSpawnCamp();
		CreateRuinSite();
		CreateMonsterDen();
		CreateWildMapThemeDressing(wildMap.Id);
		CreatePrototypeArchitecture(wildMap.Id);
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
		CreatePrototypeGround("city");
		CreateBoundaries();
		CreateMesh(_mapRoot, "CityMainRoadEdge", BoxMeshFor(new Vector3(10.8f, 0.075f, 48.0f)), _mainCityCenter + new Vector3(0.0f, FlatWalkableCenterY(0.075f, 0.004f), 8.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "CityMainRoad", BoxMeshFor(new Vector3(8.6f, 0.08f, 46.0f)), _mainCityCenter + new Vector3(0.0f, FlatWalkableCenterY(0.08f, 0.008f), 8.0f), _matCobblestone);
		CreateMainCity();
		CreatePrototypeArchitecture("city");
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

	// City build moved to scripts/World/World.CityBuild.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Decor moved to scripts/World/World.Decor.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

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
			// Wild population scales with map area: (length × width) / 800. A 150×150
			// map yields 150*150/800 = 28 monsters.
			int wildMonsterTarget = Mathf.Max(Mathf.FloorToInt(MapSize * MapSize / 800.0f), 8);
			foreach (WildMapDefinition wildMap in WildMaps)
			{
				_wildMonsterTargetCountsById[wildMap.Id] = wildMonsterTarget;
				EnsureWildInstancePopulated(wildMap.Id, GetSelectedTier(wildMap.Id), LocalGroupId());
			}
		}

		SpawnCityNpcs();
		_monsterRespawnRemaining = MonsterRespawnInterval;
		UpdateActorMapActivity();
		UpdateActiveBossHud(false);
		_player.RefreshBossWorldStatus(true);
	}

	private SimpleActor SpawnMonsterForMap(string mapId, int forcedTier = 0, int groupId = 0)
	{
		SimpleActor actor = CreateActor(true, mapId, "", "", 0, forcedTier);
		actor.GroupId = groupId;

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
		ApplyActorInstanceState(actor);
		RegisterNetworkMonster(actor, tierVisualScale, Colors.Transparent);
		return actor;
	}

	// A wild monster/boss is only VISIBLE to the local player on its map AND (for
	// wild maps) on its tier AND in its group instance — (map, tier, group) are
	// parallel instances so different parties/solo players never share monsters.
	// Captured companions and caves keep the plain map check.
	private bool IsActorInstanceActive(SimpleActor actor)
	{
		if (actor.MapId != _activeMapId)
		{
			return false;
		}

		if (actor.ActorKind == "monster" && !actor.IsCaptured && IsWildMapId(actor.MapId))
		{
			return actor.WorldTier == GetSelectedTier(actor.MapId) && actor.GroupId == LocalGroupId();
		}

		return true;
	}

	// Set a wild monster's simulate/visible state: the host keeps every in-use
	// instance simulated (so remote groups have live monsters) but only shows its
	// own; clients never simulate (they render host puppets); single-player shows
	// and simulates its own instance.
	private void ApplyActorInstanceState(SimpleActor actor)
	{
		bool visible = IsActorInstanceActive(actor);
		bool simulate;
		if (NetworkManager.Instance is { IsClient: true })
		{
			// Client monsters are puppets: they don't run AI, but they DO need physics
			// enabled to lerp toward their host-streamed positions and to keep their
			// collision so the local player can target/attack them. So a client
			// simulates exactly its visible (own-instance) puppets.
			simulate = visible;
		}
		else if (NetworkManager.Instance is { IsHost: true }
			&& actor.ActorKind == "monster" && !actor.IsCaptured && IsWildMapId(actor.MapId))
		{
			// Simulate only instances a player is physically standing in right now —
			// not every alive instance. Otherwise the host keeps running its own wild
			// monsters while it's back in the city, and they attack it unseen.
			simulate = IsWildInstanceOccupied(actor.MapId, actor.WorldTier, actor.GroupId);
		}
		else
		{
			simulate = visible;
		}

		actor.SetWorldMapState(simulate, visible);
	}

	private SimpleActor SpawnBossForMap(BossDefinition definition, int tier, int groupId, bool announce)
	{
		UseWildMapObstacleContext(definition.MapId);
		// Boss stats are hand-authored per map, so the tier layer is applied
		// explicitly here (docs/world_progression.md).
		tier = WorldTierCatalog.ClampTier(tier);
		int bossLevel = definition.Level + WorldTierCatalog.GetBossLevelBonus(tier);
		float bossMultiplier = WorldTierCatalog.GetBossStatMultiplier(tier);
		float rewardMultiplier = WorldTierCatalog.GetRewardMultiplier(tier);

		SimpleActor boss = CreateActor(true, definition.MapId, definition.SpeciesNameKey, definition.CombatRole, bossLevel, tier);
		boss.GroupId = groupId;
		boss.Name = $"Boss_{definition.MapId}_t{tier}_g{groupId}";
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
		ApplyActorInstanceState(boss);
		string instanceKey = WildInstanceKey(definition.MapId, tier, groupId);
		_wildBossesByInstance[instanceKey] = boss;
		_wildBossRespawnRemainingByInstance.Remove(instanceKey);
		RegisterNetworkMonster(boss, definition.VisualScale, definition.AuraColor);

		bool isLocalInstance = definition.MapId == _activeMapId && tier == GetSelectedTier(definition.MapId) && groupId == LocalGroupId();
		if (isLocalInstance)
		{
			_player.SetActiveBoss(boss);
		}
		if (announce && isLocalInstance)
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
		boss.AddChild(new BossMagicCircleVfx
		{
			Name = "BossAura",
			Position = new Vector3(0.0f, 0.08f, 0.0f),
			AuraColor = auraColor,
			EffectRadius = Mathf.Clamp(1.18f * visualScale, 1.75f, 4.2f),
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
			new("name.npc.item_merchant", RingFrontOffset(30.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(30.0f, shopRadius)), 0.8f, "Support"),
			new("name.npc.refiner", RingFrontOffset(90.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(90.0f, shopRadius)), 0.7f, "Tank"),
			// 傭兵仲介已移除：傭兵清單併入「夥伴招募所」(pet_trainer) 的傭兵分頁。
			new("name.npc.pet_trainer", RingFrontOffset(210.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(210.0f, shopRadius)), 0.7f, "Support"),
			new("name.npc.warehouse_keeper", RingFrontOffset(270.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(270.0f, shopRadius)), 0.8f, "Support"),
			new("name.npc.blacksmith", RingFrontOffset(330.0f, shopRadius, frontDistance), YawFacingCenter(RingOffset(330.0f, shopRadius)), 0.8f, "Tank"),
		};

		// Functional shop NPCs (always present).
		foreach (CityNpcStation station in stations)
		{
			SpawnCityNpc(station, CityNpcConfig.GetShopModel(station.NameKey));
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
		Vector3 spawnPosition = _mainCityCenter + RingFrontOffset(150.0f, 31.0f, 2.6f);
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
		Vector3 spawnPosition = _mainCityCenter + RingFrontOffset(210.0f, 31.0f, 2.4f);
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
		if (actor is SimpleActor trainingDummy && trainingDummy.DisplayName == "name.training_dummy")
		{
			BuildTrainingDummyVisual(actor);
			return;
		}
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

	private void BuildTrainingDummyVisual(Node3D actor)
	{
		Material straw = MakeMaterial(new Color(0.84f, 0.65f, 0.24f));
		Material strawLight = MakeMaterial(new Color(0.97f, 0.80f, 0.36f));
		Material sack = MakeMaterial(new Color(0.58f, 0.40f, 0.20f));
		Material cloth = MakeMaterial(new Color(0.62f, 0.16f, 0.12f));
		Material rope = MakeMaterial(new Color(0.72f, 0.52f, 0.25f));

		AddMesh(actor, "Stake", new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.10f, Height = 2.65f }, new Vector3(0.0f, 1.28f, 0.16f), Vector3.Zero, Vector3.One, _matWood);
		AddMesh(actor, "ShoulderPole", new CylinderMesh { TopRadius = 0.065f, BottomRadius = 0.075f, Height = 2.25f }, new Vector3(0.0f, 1.52f, 0.05f), new Vector3(0.0f, 0.0f, 90.0f), Vector3.One, _matWood);

		AddMesh(actor, "StrawTorso", new CapsuleMesh { Radius = 0.38f, Height = 1.12f }, new Vector3(0.0f, 1.20f, 0.0f), Vector3.Zero, new Vector3(1.10f, 1.0f, 0.78f), straw);
		AddMesh(actor, "TunicFront", BoxMeshFor(new Vector3(0.70f, 0.78f, 0.07f)), new Vector3(0.0f, 1.18f, -0.32f), Vector3.Zero, Vector3.One, cloth);
		AddMesh(actor, "Patch", BoxMeshFor(new Vector3(0.24f, 0.20f, 0.075f)), new Vector3(0.18f, 1.30f, -0.37f), new Vector3(0.0f, 0.0f, 8.0f), Vector3.One, sack);
		AddMesh(actor, "RopeBelt", new CylinderMesh { TopRadius = 0.36f, BottomRadius = 0.36f, Height = 0.10f }, new Vector3(0.0f, 0.88f, 0.0f), Vector3.Zero, new Vector3(1.06f, 1.0f, 0.82f), rope);

		AddMesh(actor, "SackHead", new SphereMesh { Radius = 0.39f, Height = 0.72f }, new Vector3(0.0f, 1.95f, -0.03f), Vector3.Zero, new Vector3(0.96f, 1.05f, 0.86f), sack);
		AddMesh(actor, "ButtonEyeL", CylinderMeshFor(0.065f, 0.065f, 0.035f), new Vector3(-0.14f, 2.02f, -0.36f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, _matActorDark);
		AddMesh(actor, "ButtonEyeR", CylinderMeshFor(0.065f, 0.065f, 0.035f), new Vector3(0.14f, 2.02f, -0.36f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, _matActorDark);
		AddMesh(actor, "StitchedMouthL", BoxMeshFor(new Vector3(0.20f, 0.035f, 0.035f)), new Vector3(-0.09f, 1.84f, -0.37f), new Vector3(0.0f, 0.0f, -9.0f), Vector3.One, _matActorDark);
		AddMesh(actor, "StitchedMouthR", BoxMeshFor(new Vector3(0.20f, 0.035f, 0.035f)), new Vector3(0.09f, 1.84f, -0.37f), new Vector3(0.0f, 0.0f, 9.0f), Vector3.One, _matActorDark);

		AddMesh(actor, "LeftSleeve", new CapsuleMesh { Radius = 0.14f, Height = 0.78f }, new Vector3(-0.68f, 1.49f, 0.0f), new Vector3(0.0f, 0.0f, 90.0f), Vector3.One, cloth);
		AddMesh(actor, "RightSleeve", new CapsuleMesh { Radius = 0.14f, Height = 0.78f }, new Vector3(0.68f, 1.49f, 0.0f), new Vector3(0.0f, 0.0f, 90.0f), Vector3.One, cloth);
		for (int side = -1; side <= 1; side += 2)
		{
			for (int strand = -1; strand <= 1; strand++)
			{
				AddMesh(actor, $"WristStraw{side}_{strand}", new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.025f, Height = 0.34f },
					new Vector3(side * 1.04f, 1.48f + strand * 0.055f, strand * 0.055f),
					new Vector3(0.0f, 0.0f, side * (78.0f + strand * 7.0f)), Vector3.One, strawLight);
			}
		}
		for (int strand = -2; strand <= 2; strand++)
		{
			AddMesh(actor, $"HemStraw{strand}", new CylinderMesh { TopRadius = 0.0f, BottomRadius = 0.025f, Height = 0.42f },
				new Vector3(strand * 0.12f, 0.58f, 0.02f), new Vector3(0.0f, 0.0f, strand * 5.0f), Vector3.One, strawLight);
		}

		AddMesh(actor, "HatBrim", CylinderMeshFor(0.58f, 0.64f, 0.08f), new Vector3(0.0f, 2.27f, -0.01f), new Vector3(0.0f, 0.0f, -4.0f), Vector3.One, straw);
		AddMesh(actor, "HatCrown", new CylinderMesh { TopRadius = 0.24f, BottomRadius = 0.39f, Height = 0.38f }, new Vector3(0.0f, 2.47f, 0.0f), new Vector3(0.0f, 0.0f, -4.0f), Vector3.One, strawLight);
		AddMesh(actor, "HatBand", new CylinderMesh { TopRadius = 0.395f, BottomRadius = 0.395f, Height = 0.08f }, new Vector3(0.0f, 2.31f, -0.01f), new Vector3(0.0f, 0.0f, -4.0f), Vector3.One, cloth);
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
		// Tier 1 is the newbie band: "幼年" monsters are passive and only fight back
		// when attacked, so new players can explore the starter zone safely.
		if (isMonster)
		{
			actor.SetPassive(tier <= WorldTierCatalog.MinTier);
		}
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

	// Portals moved to scripts/World/World.Portals.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

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
			EnsureWildInstancePopulated(wildMap.Id, GetSelectedTier(wildMap.Id), LocalGroupId());
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
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (!IsInstanceValid(actor) || actor.IsCaptured)
			{
				continue;
			}

			if (actor.ActorKind == "monster")
			{
				ApplyActorInstanceState(actor);
			}
			else
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
		foreach (KeyValuePair<string, (string MapId, int Tier, int GroupId)> entry in _spawnedWildInstancesByKey)
		{
			RespawnMonstersIfNeeded(entry.Value.MapId, entry.Value.Tier, entry.Value.GroupId);
		}
	}

	private void UpdateWildBosses(float step)
	{
		if (_actorsRoot == null || WildBosses.Length == 0 || IsNetworkClientWorld)
		{
			return;
		}

		// One boss per populated (map, tier, group) instance.
		foreach (KeyValuePair<string, (string MapId, int Tier, int GroupId)> instanceEntry in _spawnedWildInstancesByKey)
		{
			string instanceKey = instanceEntry.Key;
			(string mapId, int tier, int groupId) = instanceEntry.Value;
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
				if (mapId == _activeMapId && tier == GetSelectedTier(mapId) && groupId == LocalGroupId())
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

			SpawnBossForMap(definition.Value, tier, groupId, true);
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

	private void RespawnMonstersIfNeeded(string mapId, int tier, int groupId)
	{
		int targetCount = GetWildMonsterTargetCount(mapId);
		int livingCount = CountLivingMonstersInInstance(mapId, tier, groupId, false);
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
			SpawnMonsterForMap(mapId, tier, groupId);
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
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (IsInstanceValid(actor)
				&& actor.ActorKind == "monster"
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

	private int CountLivingMonstersInInstance(string mapId, int tier, int groupId, bool includeBosses = true)
	{
		int count = 0;
		foreach (SimpleActor actor in SimpleActor.ActiveActors)
		{
			if (IsInstanceValid(actor)
				&& actor.ActorKind == "monster"
				&& actor.MapId == mapId
				&& actor.WorldTier == tier
				&& actor.GroupId == groupId
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

	private static string WildInstanceKey(string mapId, int tier, int groupId)
	{
		return $"{mapId}#t{tier}#g{groupId}";
	}

	// The local player's instance group: their party (leader) or solo id online,
	// 0 in single-player. Different groups never share a hunting-ground instance.
	private int LocalGroupId()
	{
		return NetworkManager.Instance is { IsOnline: true } net ? net.LocalGroupId : 0;
	}

	// The instance key of THIS player's view of a map (their selected tier + group).
	private string GetLocalWildInstanceKey(string mapId)
	{
		return WildInstanceKey(mapId, GetSelectedTier(mapId), LocalGroupId());
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

		EnsureWildInstancePopulated(mapId, tier, LocalGroupId());
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
	public void EnsureWildInstancePopulated(string mapId, int tier, int groupId)
	{
		if (IsNetworkClientWorld || !_worldActorsGenerated || !IsWildMapId(mapId))
		{
			return;
		}

		tier = WorldTierCatalog.ClampTier(tier);
		string instanceKey = WildInstanceKey(mapId, tier, groupId);
		if (_spawnedWildInstancesByKey.ContainsKey(instanceKey))
		{
			return;
		}

		_spawnedWildInstancesByKey[instanceKey] = (mapId, tier, groupId);
		UseWildMapObstacleContext(mapId);
		int targetCount = GetWildMonsterTargetCount(mapId);
		for (int index = 0; index < targetCount; index++)
		{
			SpawnMonsterForMap(mapId, tier, groupId);
		}

		BossDefinition? definition = FindBossDefinition(mapId);
		if (definition != null)
		{
			SpawnBossForMap(definition.Value, tier, groupId, false);
		}
	}

	// Frees populations no player is using. An instance stays alive while it is
	// some player's current selection for that map (local player) or a remote
	// player is standing in it — per (map, tier, group).
	private void DespawnInactiveWildInstances()
	{
		if (IsNetworkClientWorld)
		{
			return;
		}

		_instanceCleanupScratch.Clear();
		foreach (KeyValuePair<string, (string MapId, int Tier, int GroupId)> entry in _spawnedWildInstancesByKey)
		{
			if (!IsWildInstanceInUse(entry.Value.MapId, entry.Value.Tier, entry.Value.GroupId))
			{
				_instanceCleanupScratch.Add(entry.Key);
			}
		}

		foreach (string instanceKey in _instanceCleanupScratch)
		{
			(string mapId, int tier, int groupId) = _spawnedWildInstancesByKey[instanceKey];
			_spawnedWildInstancesByKey.Remove(instanceKey);
			_wildBossesByInstance.Remove(instanceKey);
			_wildBossRespawnRemainingByInstance.Remove(instanceKey);
			foreach (SimpleActor actor in SimpleActor.ActiveActors)
			{
				if (IsInstanceValid(actor)
					&& actor.ActorKind == "monster"
					&& actor.MapId == mapId
					&& actor.WorldTier == tier
					&& actor.GroupId == groupId
					&& !actor.IsCaptured)
				{
					// Tell clients to drop their puppet for this monster too, so a player
					// who left the group doesn't keep a stale (hidden) copy around.
					if (NetworkManager.Instance is { IsHost: true } net && actor.NetworkMonsterId >= 0)
					{
						net.BroadcastMonsterRemoved(actor.NetworkMonsterId, false);
						_netMonstersById.Remove(actor.NetworkMonsterId);
					}
					actor.QueueFree();
				}
			}
		}
	}

	private bool IsWildInstanceInUse(string mapId, int tier, int groupId)
	{
		// Kept alive (not despawned) whenever it's the local player's selected tier
		// instance — so monsters persist across trips back to the city — or a remote
		// player is in it.
		if (GetSelectedTier(mapId) == tier && LocalGroupId() == groupId)
		{
			return true;
		}

		return NetworkManager.Instance is { IsHost: true } net && net.IsRemoteInstanceInUse(mapId, tier, groupId);
	}

	// Whether a player is PHYSICALLY present in this instance right now (as opposed
	// to merely alive). Drives host simulation so idle instances stop running.
	private bool IsWildInstanceOccupied(string mapId, int tier, int groupId)
	{
		if (_activeMapId == mapId && GetSelectedTier(mapId) == tier && LocalGroupId() == groupId)
		{
			return true;
		}

		return NetworkManager.Instance is { IsHost: true } net && net.IsRemoteInstanceInUse(mapId, tier, groupId);
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
