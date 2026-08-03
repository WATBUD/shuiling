using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
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
		// Chain portal to the next biome, so the 5 maps form a walkable sequence
		// (reaching a biome marks it visited for later city fast-travel).
		string nextBiomeId = GetNextWildMapId(wildMap.Id);
		if (!string.IsNullOrEmpty(nextBiomeId))
		{
			CreateMapPortal("NextBiomePortal", _wildSpawnPosition + new Vector3(6.0f, 0.0f, 5.0f), nextBiomeId, "portal.travel_next");
		}
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

	// Per-biome theme dressing lives in World.Biomes.cs (CreateWildMapThemeDressing).

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
}
