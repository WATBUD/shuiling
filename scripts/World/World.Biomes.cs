using Godot;

// Biome dressing for the wild maps: per-map atmosphere (sky/fog/sun), biome
// specific procedural props and landmark set-pieces. Goal: each wild map reads
// as its own ecosystem at a glance (docs/world_progression.md — map = biome).
public partial class World
{
	// Which map is currently being BUILT (not the active map) — set by
	// BuildWildMapScene/BuildCityMapScene so scatter helpers pick biome props.
	private string _currentThemeMapId = "wild_forest";

	// Environment handles captured in BuildEnvironment for atmosphere swaps.
	private WorldEnvironment _worldEnvironment = null!;
	private DirectionalLight3D _sunLight = null!;
	private ProceduralSkyMaterial _skyMaterial = null!;

	// Biome materials.
	private StandardMaterial3D _matSwampTrunk = null!;
	private StandardMaterial3D _matSwampLeaf = null!;
	private StandardMaterial3D _matMoss = null!;
	private StandardMaterial3D _matReed = null!;
	private StandardMaterial3D _matReedHead = null!;
	private StandardMaterial3D _matLilyPad = null!;
	private StandardMaterial3D _matLilyBloom = null!;
	private StandardMaterial3D _matDeadWood = null!;
	private StandardMaterial3D _matRedRock = null!;
	private StandardMaterial3D _matRedRockDark = null!;
	private StandardMaterial3D _matObsidian = null!;
	private StandardMaterial3D _matDryGrass = null!;
	private StandardMaterial3D _matEmber = null!;
	private StandardMaterial3D _matSnowCover = null!;
	private StandardMaterial3D _matIceShard = null!;
	private StandardMaterial3D _matPineLeaf = null!;
	private StandardMaterial3D _matFirefly = null!;

	private void CreateBiomeMaterials()
	{
		_matSwampTrunk = MakeMaterial(new Color(0.20f, 0.16f, 0.11f));
		_matSwampLeaf = MakeMaterial(new Color(0.15f, 0.33f, 0.20f));
		_matMoss = MakeMaterial(new Color(0.26f, 0.46f, 0.22f));
		_matReed = MakeMaterial(new Color(0.32f, 0.48f, 0.21f));
		_matReedHead = MakeMaterial(new Color(0.45f, 0.31f, 0.16f));
		_matLilyPad = MakeMaterial(new Color(0.17f, 0.47f, 0.26f));
		_matLilyBloom = MakeMaterial(new Color(0.98f, 0.74f, 0.86f));
		_matDeadWood = MakeMaterial(new Color(0.32f, 0.26f, 0.19f));
		_matRedRock = MakeMaterial(new Color(0.60f, 0.31f, 0.18f));
		_matRedRockDark = MakeMaterial(new Color(0.43f, 0.21f, 0.13f));
		_matObsidian = MakeMaterial(new Color(0.10f, 0.09f, 0.12f), 0.28f);
		_matDryGrass = MakeMaterial(new Color(0.63f, 0.51f, 0.26f));
		_matEmber = MakeEmissiveMaterial(new Color(1.0f, 0.42f, 0.12f), 2.2f);
		_matSnowCover = MakeMaterial(new Color(0.90f, 0.94f, 0.99f));
		_matIceShard = MakeMaterial(new Color(0.58f, 0.82f, 0.96f, 0.86f), 0.14f);
		_matPineLeaf = MakeMaterial(new Color(0.10f, 0.32f, 0.22f));
		_matFirefly = MakeEmissiveMaterial(new Color(0.85f, 1.0f, 0.45f), 2.6f);
	}

	// ---------------------------------------------------------------- ground palette

	// Ground tones for the generic terrain overlays (meadow/field/bank/water/
	// paths). Shared across all wild maps before, so snow showed green meadows
	// and brown fields; now each biome recolours the whole floor to match.
	private readonly record struct BiomeGroundPalette(
		Material Base, Material Meadow, Material Field, Material Path,
		Material Bank, Material Water, Material Shallow, Material Ash);

	// Set in BuildWildMapScene before the terrain overlays are laid down.
	private BiomeGroundPalette _wildGroundPalette;

	private BiomeGroundPalette BuildWildGroundPalette(string mapId)
	{
		switch (mapId)
		{
			case "wild_snow":
				return new BiomeGroundPalette(
					Base: MakeMaterial(new Color(0.88f, 0.92f, 0.97f)),
					Meadow: MakeMaterial(new Color(0.90f, 0.94f, 0.99f)),
					Field: MakeMaterial(new Color(0.82f, 0.88f, 0.95f)),
					Path: MakeMaterial(new Color(0.80f, 0.85f, 0.91f)),
					Bank: MakeMaterial(new Color(0.74f, 0.83f, 0.91f)),
					Water: MakeMaterial(new Color(0.58f, 0.82f, 0.96f, 0.88f), 0.14f),
					Shallow: MakeMaterial(new Color(0.72f, 0.88f, 0.98f, 0.80f), 0.12f),
					Ash: MakeMaterial(new Color(0.85f, 0.90f, 0.96f)));
			case "wild_badlands":
				return new BiomeGroundPalette(
					Base: MakeMaterial(new Color(0.52f, 0.32f, 0.20f)),
					Meadow: MakeMaterial(new Color(0.56f, 0.45f, 0.26f)),
					Field: MakeMaterial(new Color(0.53f, 0.29f, 0.17f)),
					Path: MakeMaterial(new Color(0.44f, 0.31f, 0.20f)),
					Bank: MakeMaterial(new Color(0.47f, 0.34f, 0.21f)),
					Water: MakeMaterial(new Color(0.34f, 0.30f, 0.18f, 0.78f), 0.10f),
					Shallow: MakeMaterial(new Color(0.46f, 0.40f, 0.26f, 0.70f), 0.10f),
					Ash: _matNest);
			case "wild_marsh":
				return new BiomeGroundPalette(
					Base: MakeMaterial(new Color(0.22f, 0.32f, 0.24f)),
					Meadow: MakeMaterial(new Color(0.26f, 0.40f, 0.24f)),
					Field: MakeMaterial(new Color(0.34f, 0.31f, 0.19f)),
					Path: MakeMaterial(new Color(0.30f, 0.27f, 0.18f)),
					Bank: _matPondBank,
					Water: _matWater,
					Shallow: _matShallowWater,
					Ash: MakeMaterial(new Color(0.24f, 0.26f, 0.18f)));
			default: // wild_forest and any fallback keep the lush green set.
				return new BiomeGroundPalette(
					Base: MakeMaterial(new Color(0.24f, 0.46f, 0.29f)),
					Meadow: _matMeadow,
					Field: _matField,
					Path: _matPath,
					Bank: _matPondBank,
					Water: _matWater,
					Shallow: _matShallowWater,
					Ash: _matNest);
		}
	}

	// ---------------------------------------------------------------- atmosphere

	private readonly record struct BiomeAtmosphere(
		Color SkyTop, Color SkyHorizon,
		Color Ambient, float AmbientEnergy,
		Color Fog, float FogDensity,
		Color Sun, float SunEnergy);

	private static readonly BiomeAtmosphere DefaultAtmosphere = new(
		new Color(0.20f, 0.45f, 0.86f), new Color(0.82f, 0.90f, 1.0f),
		new Color(0.72f, 0.80f, 0.90f), 0.72f,
		new Color(0.74f, 0.82f, 0.88f), 0.008f,
		new Color(1.0f, 0.91f, 0.76f), 2.4f);

	private static BiomeAtmosphere GetBiomeAtmosphere(string mapId)
	{
		return mapId switch
		{
			// Lush late-morning warmth under a clear sky.
			"wild_forest" => new BiomeAtmosphere(
				new Color(0.18f, 0.44f, 0.80f), new Color(0.80f, 0.92f, 0.90f),
				new Color(0.68f, 0.80f, 0.72f), 0.74f,
				new Color(0.70f, 0.82f, 0.74f), 0.009f,
				new Color(1.0f, 0.93f, 0.72f), 2.5f),
			// Thick teal mist, weak diffused sun.
			"wild_marsh" => new BiomeAtmosphere(
				new Color(0.30f, 0.44f, 0.46f), new Color(0.62f, 0.72f, 0.64f),
				new Color(0.55f, 0.66f, 0.58f), 0.62f,
				new Color(0.48f, 0.60f, 0.52f), 0.024f,
				new Color(0.85f, 0.95f, 0.78f), 1.7f),
			// Dusty amber haze, low burning sun.
			"wild_badlands" => new BiomeAtmosphere(
				new Color(0.36f, 0.30f, 0.48f), new Color(0.98f, 0.70f, 0.46f),
				new Color(0.86f, 0.66f, 0.50f), 0.68f,
				new Color(0.74f, 0.52f, 0.36f), 0.012f,
				new Color(1.0f, 0.74f, 0.48f), 2.7f),
			// Bright, cold, high-albedo glare with white haze.
			"wild_snow" => new BiomeAtmosphere(
				new Color(0.28f, 0.50f, 0.82f), new Color(0.90f, 0.95f, 1.0f),
				new Color(0.82f, 0.88f, 1.0f), 0.92f,
				new Color(0.86f, 0.91f, 0.98f), 0.015f,
				new Color(0.96f, 0.98f, 1.0f), 2.3f),
			_ => DefaultAtmosphere,
		};
	}

	private static readonly BiomeAtmosphere CaveAtmosphere = new(
		new Color(0.06f, 0.07f, 0.10f), new Color(0.12f, 0.13f, 0.18f),
		new Color(0.42f, 0.44f, 0.55f), 0.40f,
		new Color(0.10f, 0.11f, 0.16f), 0.035f,
		new Color(0.55f, 0.60f, 0.75f), 0.7f);

	// Called from SetMapVisibility so travelling re-lights the world to match
	// the destination biome.
	private void ApplyMapAtmosphere(string mapId)
	{
		if (_worldEnvironment == null || _sunLight == null || _skyMaterial == null)
		{
			return;
		}

		BiomeAtmosphere atmosphere = IsCaveMapId(mapId) ? CaveAtmosphere : GetBiomeAtmosphere(mapId);
		_skyMaterial.SkyTopColor = atmosphere.SkyTop;
		_skyMaterial.SkyHorizonColor = atmosphere.SkyHorizon;
		Environment environment = _worldEnvironment.Environment;
		environment.AmbientLightColor = atmosphere.Ambient;
		environment.AmbientLightEnergy = atmosphere.AmbientEnergy;
		environment.FogLightColor = atmosphere.Fog;
		environment.FogDensity = atmosphere.FogDensity;
		_sunLight.LightColor = atmosphere.Sun;
		_sunLight.LightEnergy = atmosphere.SunEnergy;
	}

	// ---------------------------------------------------------------- scatter

	// Large obstacle-forming prop for the map being built (caller registers the
	// obstacle position).
	private void CreateBiomePrimaryProp(Vector3 position)
	{
		float roll = _rng.Randf();
		switch (_currentThemeMapId)
		{
			case "wild_marsh":
				if (roll < 0.52f) CreateSwampTree(position);
				else if (roll < 0.76f) CreateDeadTree(position);
				else CreateRock(position);
				return;
			case "wild_badlands":
				if (roll < 0.30f) CreateDeadTree(position);
				else if (roll < 0.70f) CreateRockSpire(position);
				else CreateRock(position);
				return;
			case "wild_snow":
				if (roll < 0.62f) CreatePineTree(position);
				else CreateSnowRock(position);
				return;
			default:
				if (roll < 0.70f) CreateTree(position);
				else CreateRock(position);
				return;
		}
	}

	// Small non-blocking ground detail for the map being built.
	private void CreateBiomeDetailProp(Vector3 position)
	{
		float roll = _rng.Randf();
		switch (_currentThemeMapId)
		{
			case "wild_marsh":
				if (roll < 0.32f) CreateGrassPatch(position);
				else if (roll < 0.62f) CreateReedCluster(position);
				else if (roll < 0.86f) CreateMushroom(position);
				else CreateFireflySwarm(position);
				return;
			case "wild_badlands":
				if (roll < 0.46f) CreateDryShrub(position);
				else if (roll < 0.64f) CreateCrystalCluster(position, (float)_rng.RandfRange(0.35f, 0.6f), _matEmber);
				else if (roll < 0.88f) CreatePebbleCluster(position, _matRedRockDark);
				else CreateObsidianSpike(position);
				return;
			case "wild_snow":
				if (roll < 0.34f) CreateSnowLump(position);
				else if (roll < 0.66f) CreateCrystalCluster(position, (float)_rng.RandfRange(0.4f, 0.72f), _matIceShard);
				else if (roll < 0.9f) CreateGrassPatch(position);
				else CreateMushroom(position);
				return;
			default:
				if (roll < 0.48f) CreateGrassPatch(position);
				else if (roll < 0.76f) CreateFlowerPatch(position);
				else if (roll < 0.9f) CreateMushroom(position);
				else CreateCrystalCluster(position, (float)_rng.RandfRange(0.42f, 0.72f), _matCrystal);
				return;
		}
	}

	// ---------------------------------------------------------------- theme dressing

	// Per-biome terrain overlays and landmark set-pieces. Moved out of World.cs
	// and expanded so each biome has a distinct centrepiece.
	private void CreateWildMapThemeDressing(string mapId)
	{
		switch (mapId)
		{
			case "wild_forest":
				DressForest();
				return;
			case "wild_marsh":
				DressMarsh();
				return;
			case "wild_badlands":
				DressBadlands();
				return;
			case "wild_snow":
				DressSnow();
				return;
		}
	}

	private void DressForest()
	{
		// Sun-dappled clearings and a lily pond.
		CreateTerrainPatch("ForestGladeA", new Vector3(20.0f, 0.0f, -46.0f), 11.0f, new Vector3(1.3f, 1.0f, 0.8f), 24.0f, _matGrassBright, 0.037f);
		CreateTerrainPatch("ForestGladeB", new Vector3(-38.0f, 0.0f, 40.0f), 10.0f, new Vector3(1.2f, 1.0f, 0.76f), -30.0f, _matGrassBright, 0.037f);
		CreateTerrainPatch("ForestPondBank", new Vector3(12.0f, 0.0f, 54.0f), 11.0f, new Vector3(1.3f, 1.0f, 0.72f), 14.0f, _matPondBank, 0.05f);
		CreateTerrainPatch("ForestPond", new Vector3(12.0f, 0.0f, 54.0f), 8.5f, new Vector3(1.22f, 1.0f, 0.62f), 14.0f, _matWater, 0.064f);
		for (int index = 0; index < 6; index++)
		{
			float angle = index / 6.0f * Mathf.Tau;
			CreateLilyPad(new Vector3(12.0f, 0.0f, 54.0f) + new Vector3(Mathf.Cos(angle) * 4.2f, 0.0f, Mathf.Sin(angle) * 2.6f));
		}
		for (int index = 0; index < 7; index++)
		{
			float angle = 0.3f + index / 7.0f * Mathf.Tau;
			CreateReedCluster(new Vector3(12.0f, 0.0f, 54.0f) + new Vector3(Mathf.Cos(angle) * 9.0f, 0.0f, Mathf.Sin(angle) * 6.0f));
		}

		// Ancient guardian tree.
		var ancientTreePosition = new Vector3(48.0f, 0.0f, -52.0f);
		CreateAncientTree(ancientTreePosition);
		_obstaclePositions.Add(ancientTreePosition);
		CreateFireflySwarm(ancientTreePosition + new Vector3(2.6f, 0.0f, 1.4f));
		CreateFireflySwarm(ancientTreePosition + new Vector3(-2.2f, 0.0f, -1.8f));
		for (int index = 0; index < 5; index++)
		{
			float angle = index / 5.0f * Mathf.Tau;
			CreateFlowerPatch(ancientTreePosition + new Vector3(Mathf.Cos(angle) * 4.6f, 0.0f, Mathf.Sin(angle) * 4.6f));
		}
	}

	private void DressMarsh()
	{
		Vector3 poolA = new(28.0f, 0.0f, -28.0f);
		Vector3 poolB = new(-22.0f, 0.0f, 44.0f);
		CreateTerrainPatch("MarshMudA", poolA, 19.0f, new Vector3(1.3f, 1.0f, 0.68f), -18.0f, _matPondBank, 0.05f);
		CreateTerrainPatch("MarshMudB", poolB, 16.0f, new Vector3(1.26f, 1.0f, 0.64f), 22.0f, _matPondBank, 0.05f);
		CreateTerrainPatch("MarshMirrorWaterA", poolA, 16.0f, new Vector3(1.25f, 1.0f, 0.62f), -18.0f, _matShallowWater, 0.062f);
		CreateTerrainPatch("MarshMirrorWaterB", poolB, 13.0f, new Vector3(1.2f, 1.0f, 0.58f), 22.0f, _matShallowWater, 0.062f);

		// Drowned trees, lilies and reed banks make the pools feel alive.
		foreach ((Vector3 pool, float radius) in new[] { (poolA, 13.0f), (poolB, 10.0f) })
		{
			CreateDeadTree(pool + new Vector3(radius * 0.35f, 0.0f, -radius * 0.2f));
			CreateDeadTree(pool + new Vector3(-radius * 0.4f, 0.0f, radius * 0.3f));
			_obstaclePositions.Add(pool + new Vector3(radius * 0.35f, 0.0f, -radius * 0.2f));
			_obstaclePositions.Add(pool + new Vector3(-radius * 0.4f, 0.0f, radius * 0.3f));
			for (int index = 0; index < 6; index++)
			{
				float angle = index / 6.0f * Mathf.Tau;
				CreateLilyPad(pool + new Vector3(Mathf.Cos(angle) * radius * 0.45f, 0.0f, Mathf.Sin(angle) * radius * 0.32f));
			}
			for (int index = 0; index < 9; index++)
			{
				float angle = 0.2f + index / 9.0f * Mathf.Tau;
				CreateReedCluster(pool + new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius * 0.75f));
			}
			CreateFireflySwarm(pool + new Vector3(0.0f, 0.0f, radius * 0.6f));
		}

		for (int index = 0; index < 16; index++)
		{
			Vector3 position = new((float)_rng.RandfRange(-62.0f, 62.0f), 0.0f, (float)_rng.RandfRange(-62.0f, 62.0f));
			if (position.DistanceTo(Vector3.Zero) > 12.0f)
			{
				CreateMushroom(position);
				CreateGrassPatch(position + new Vector3(0.6f, 0.0f, -0.4f));
			}
		}
	}

	private void DressBadlands()
	{
		CreateTerrainPatch("BadlandsAshFieldA", new Vector3(-30.0f, 0.0f, -28.0f), 18.0f, new Vector3(1.35f, 1.0f, 0.66f), 18.0f, _matNest, 0.04f);
		CreateTerrainPatch("BadlandsAshFieldB", new Vector3(38.0f, 0.0f, 32.0f), 16.0f, new Vector3(1.25f, 1.0f, 0.62f), -28.0f, _matNest, 0.04f);
		CreateTerrainPatch("BadlandsClayA", new Vector3(20.0f, 0.0f, -52.0f), 14.0f, new Vector3(1.4f, 1.0f, 0.6f), 30.0f, _matRedRockDark, 0.036f);
		CreateTerrainPatch("BadlandsClayB", new Vector3(-52.0f, 0.0f, 26.0f), 13.0f, new Vector3(1.3f, 1.0f, 0.62f), -12.0f, _matRedRockDark, 0.036f);

		// Mesa formations anchor the horizon.
		var mesaA = new Vector3(-48.0f, 0.0f, 42.0f);
		var mesaB = new Vector3(54.0f, 0.0f, -46.0f);
		CreateMesa(mesaA, 1.2f);
		CreateMesa(mesaB, 0.85f);
		_obstaclePositions.Add(mesaA);
		_obstaclePositions.Add(mesaB);

		// Ember vents hiss between the rocks.
		CreateEmberVent(new Vector3(12.0f, 0.0f, -38.0f), true);
		CreateEmberVent(new Vector3(-26.0f, 0.0f, 18.0f), false);
		CreateEmberVent(new Vector3(40.0f, 0.0f, 20.0f), false);

		for (int index = 0; index < 14; index++)
		{
			Vector3 position = new((float)_rng.RandfRange(-64.0f, 64.0f), 0.0f, (float)_rng.RandfRange(-64.0f, 64.0f));
			if (position.DistanceTo(Vector3.Zero) > 14.0f && IsPositionClear(position, 3.0f))
			{
				CreateRockSpire(position);
				_obstaclePositions.Add(position);
			}
		}
	}

	private void DressSnow()
	{
		CreateTerrainPatch("SnowFieldNorth", new Vector3(-24.0f, 0.0f, -32.0f), 34.0f, new Vector3(1.55f, 1.0f, 0.92f), 12.0f, _matSnowCover, 0.066f);
		CreateTerrainPatch("SnowFieldSouth", new Vector3(28.0f, 0.0f, 34.0f), 35.0f, new Vector3(1.48f, 1.0f, 0.90f), -16.0f, _matSnowCover, 0.066f);
		var lakeCenter = new Vector3(30.0f, 0.0f, -28.0f);
		CreateTerrainPatch("FrozenLake", lakeCenter, 17.0f, new Vector3(1.25f, 1.0f, 0.62f), -18.0f, _matIceShard, 0.072f);

		// Great ice shards erupt from the frozen lake.
		CreateIceShardMonolith(lakeCenter);
		_obstaclePositions.Add(lakeCenter);
		for (int index = 0; index < 6; index++)
		{
			float angle = index / 6.0f * Mathf.Tau;
			CreateCrystalCluster(lakeCenter + new Vector3(Mathf.Cos(angle) * 8.5f, 0.0f, Mathf.Sin(angle) * 5.5f), (float)_rng.RandfRange(0.6f, 1.1f), _matIceShard);
		}

		CreateSnowman(new Vector3(-10.0f, 0.0f, 22.0f));

		for (int index = 0; index < 16; index++)
		{
			Vector3 position = new((float)_rng.RandfRange(-64.0f, 64.0f), 0.0f, (float)_rng.RandfRange(-64.0f, 64.0f));
			if (position.DistanceTo(Vector3.Zero) > 14.0f && IsPositionClear(position, 3.0f))
			{
				CreateSnowRock(position);
				CreateTerrainPatch($"SnowDrift{index}", position, (float)_rng.RandfRange(3.5f, 7.0f), new Vector3(1.3f, 1.0f, 0.62f), index * 19.0f, _matSnowCover, 0.07f);
				_obstaclePositions.Add(position);
			}
		}
	}

	// ---------------------------------------------------------------- marsh props

	private void CreateSwampTree(Vector3 position)
	{
		var tree = new StaticBody3D
		{
			Name = "SwampTree",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(tree);

		float lean = (float)_rng.RandfRange(4.0f, 14.0f);
		AddMesh(tree, "Trunk", CylinderMeshFor(0.22f, 0.40f, 2.9f), new Vector3(0.0f, 1.45f, 0.0f), new Vector3(0.0f, 0.0f, lean), Vector3.One, _matSwampTrunk);
		AddMesh(tree, "RootA", CylinderMeshFor(0.05f, 0.16f, 1.1f), new Vector3(0.45f, 0.4f, 0.1f), new Vector3(0.0f, 0.0f, -38.0f), Vector3.One, _matSwampTrunk);
		AddMesh(tree, "RootB", CylinderMeshFor(0.05f, 0.14f, 1.0f), new Vector3(-0.4f, 0.36f, -0.15f), new Vector3(0.0f, 0.0f, 40.0f), Vector3.One, _matSwampTrunk);
		AddMesh(tree, "Moss", BoxMeshFor(new Vector3(0.5f, 0.08f, 0.4f)), new Vector3(0.1f, 2.2f, 0.0f), new Vector3(0.0f, 20.0f, lean), Vector3.One, _matMoss);

		// Drooping, flattened crowns.
		float crownY = 3.1f + Mathf.Sin(Mathf.DegToRad(lean)) * 0.4f;
		AddMesh(tree, "CrownCore", new SphereMesh { Radius = 1.7f, Height = 1.7f }, new Vector3(0.2f, crownY, 0.0f), Vector3.Zero, new Vector3(1.25f, 0.55f, 1.25f), _matSwampLeaf);
		AddMesh(tree, "CrownDroopA", new SphereMesh { Radius = 1.1f, Height = 1.1f }, new Vector3(1.5f, crownY - 0.55f, 0.4f), Vector3.Zero, new Vector3(1.0f, 0.45f, 1.0f), _matSwampLeaf);
		AddMesh(tree, "CrownDroopB", new SphereMesh { Radius = 1.0f, Height = 1.0f }, new Vector3(-1.3f, crownY - 0.6f, -0.5f), Vector3.Zero, new Vector3(1.0f, 0.42f, 1.0f), _matSwampLeaf);
		AddMesh(tree, "VineA", CylinderMeshFor(0.03f, 0.03f, 1.3f), new Vector3(1.6f, crownY - 1.15f, 0.4f), Vector3.Zero, Vector3.One, _matMoss);
		AddMesh(tree, "VineB", CylinderMeshFor(0.03f, 0.03f, 1.0f), new Vector3(-1.35f, crownY - 1.05f, -0.5f), Vector3.Zero, Vector3.One, _matMoss);

		tree.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, 1.4f, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(0.9f, 2.8f, 0.9f) },
		});
	}

	private void CreateReedCluster(Vector3 position)
	{
		var cluster = new Node3D
		{
			Name = "ReedCluster",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(cluster);

		int reedCount = _rng.RandiRange(5, 9);
		for (int index = 0; index < reedCount; index++)
		{
			float height = (float)_rng.RandfRange(0.9f, 1.7f);
			float offsetX = (float)_rng.RandfRange(-0.5f, 0.5f);
			float offsetZ = (float)_rng.RandfRange(-0.5f, 0.5f);
			float tilt = (float)_rng.RandfRange(-7.0f, 7.0f);
			AddMesh(cluster, "Reed", CylinderMeshFor(0.02f, 0.035f, height), new Vector3(offsetX, height * 0.5f, offsetZ), new Vector3(tilt, 0.0f, -tilt), Vector3.One, _matReed);
			if (_rng.Randf() < 0.6f)
			{
				AddMesh(cluster, "ReedHead", new CapsuleMesh { Radius = 0.05f, Height = 0.28f }, new Vector3(offsetX, height + 0.1f, offsetZ), new Vector3(tilt, 0.0f, -tilt), Vector3.One, _matReedHead);
			}
		}
	}

	private void CreateLilyPad(Vector3 position)
	{
		var pad = new Node3D
		{
			Name = "LilyPad",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(pad);

		float radius = (float)_rng.RandfRange(0.35f, 0.62f);
		AddMesh(pad, "Pad", CylinderMeshFor(radius, radius, 0.03f), new Vector3(0.0f, 0.085f, 0.0f), Vector3.Zero, Vector3.One, _matLilyPad);
		if (_rng.Randf() < 0.4f)
		{
			AddMesh(pad, "Bloom", new SphereMesh { Radius = 0.12f, Height = 0.16f }, new Vector3(radius * 0.3f, 0.16f, 0.0f), Vector3.Zero, Vector3.One, _matLilyBloom);
		}
	}

	private void CreateFireflySwarm(Vector3 position)
	{
		var swarm = new Node3D { Name = "FireflySwarm", Position = position };
		_propsRoot.AddChild(swarm);

		int fireflyCount = _rng.RandiRange(4, 7);
		for (int index = 0; index < fireflyCount; index++)
		{
			AddMesh(
				swarm,
				"Firefly",
				new SphereMesh { Radius = 0.045f, Height = 0.09f },
				new Vector3((float)_rng.RandfRange(-1.4f, 1.4f), (float)_rng.RandfRange(0.5f, 1.9f), (float)_rng.RandfRange(-1.4f, 1.4f)),
				Vector3.Zero,
				Vector3.One,
				_matFirefly);
		}
	}

	// ---------------------------------------------------------------- badlands props

	private void CreateDeadTree(Vector3 position)
	{
		var tree = new StaticBody3D
		{
			Name = "DeadTree",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(tree);

		float height = (float)_rng.RandfRange(2.4f, 3.6f);
		AddMesh(tree, "Trunk", CylinderMeshFor(0.10f, 0.30f, height), new Vector3(0.0f, height * 0.5f, 0.0f), new Vector3(0.0f, 0.0f, (float)_rng.RandfRange(-6.0f, 6.0f)), Vector3.One, _matDeadWood);
		AddMesh(tree, "BranchA", CylinderMeshFor(0.03f, 0.08f, 1.3f), new Vector3(0.45f, height * 0.72f, 0.0f), new Vector3(0.0f, 0.0f, -52.0f), Vector3.One, _matDeadWood);
		AddMesh(tree, "BranchB", CylinderMeshFor(0.03f, 0.07f, 1.1f), new Vector3(-0.4f, height * 0.62f, 0.1f), new Vector3(8.0f, 0.0f, 55.0f), Vector3.One, _matDeadWood);
		AddMesh(tree, "BranchC", CylinderMeshFor(0.02f, 0.05f, 0.8f), new Vector3(0.1f, height * 0.9f, -0.3f), new Vector3(-48.0f, 0.0f, 6.0f), Vector3.One, _matDeadWood);

		tree.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, height * 0.5f, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(0.6f, height, 0.6f) },
		});
	}

	private void CreateRockSpire(Vector3 position)
	{
		var spire = new StaticBody3D
		{
			Name = "RockSpire",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(spire);

		float baseRadius = (float)_rng.RandfRange(0.9f, 1.5f);
		float totalHeight = (float)_rng.RandfRange(2.6f, 5.2f);
		int tiers = _rng.RandiRange(3, 5);
		float y = 0.0f;
		for (int index = 0; index < tiers; index++)
		{
			float t = index / (float)tiers;
			float tierHeight = totalHeight / tiers;
			float bottom = Mathf.Lerp(baseRadius, baseRadius * 0.25f, t);
			float top = Mathf.Lerp(baseRadius, baseRadius * 0.25f, (index + 1) / (float)tiers);
			AddMesh(
				spire,
				$"SpireTier{index}",
				CylinderMeshFor(top, bottom, tierHeight),
				new Vector3((float)_rng.RandfRange(-0.12f, 0.12f), y + tierHeight * 0.5f, (float)_rng.RandfRange(-0.12f, 0.12f)),
				new Vector3(0.0f, index * 25.0f, 0.0f),
				Vector3.One,
				index % 2 == 0 ? _matRedRock : _matRedRockDark);
			y += tierHeight;
		}

		spire.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, totalHeight * 0.5f, 0.0f),
			Shape = new CylinderShape3D { Radius = baseRadius * 0.8f, Height = totalHeight },
		});
	}

	private void CreateMesa(Vector3 position, float scale)
	{
		var mesa = new StaticBody3D
		{
			Name = "Mesa",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(mesa);

		float[] widths = { 9.0f, 7.6f, 6.0f, 4.2f };
		float[] heights = { 1.6f, 1.5f, 1.4f, 1.2f };
		float y = 0.0f;
		for (int index = 0; index < widths.Length; index++)
		{
			float width = widths[index] * scale;
			float height = heights[index] * scale;
			AddMesh(
				mesa,
				$"MesaTier{index}",
				BoxMeshFor(new Vector3(width, height, width * 0.82f)),
				new Vector3((float)_rng.RandfRange(-0.3f, 0.3f) * scale, y + height * 0.5f, (float)_rng.RandfRange(-0.3f, 0.3f) * scale),
				new Vector3(0.0f, index * 9.0f, 0.0f),
				Vector3.One,
				index % 2 == 0 ? _matRedRock : _matRedRockDark);
			y += height;
		}
		AddMesh(mesa, "MesaCap", BoxMeshFor(new Vector3(4.6f * scale, 0.22f, 3.9f * scale)), new Vector3(0.0f, y + 0.11f, 0.0f), Vector3.Zero, Vector3.One, _matDryGrass);

		mesa.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, y * 0.5f, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(8.6f * scale, y, 7.2f * scale) },
		});
	}

	private void CreateEmberVent(Vector3 position, bool withLight)
	{
		var vent = new Node3D { Name = "EmberVent", Position = position };
		_propsRoot.AddChild(vent);

		AddMesh(vent, "VentRim", CylinderMeshFor(1.15f, 1.35f, 0.30f), new Vector3(0.0f, 0.15f, 0.0f), Vector3.Zero, Vector3.One, _matObsidian);
		AddMesh(vent, "VentGlow", CylinderMeshFor(0.85f, 0.85f, 0.10f), new Vector3(0.0f, 0.30f, 0.0f), Vector3.Zero, Vector3.One, _matEmber);
		for (int index = 0; index < 4; index++)
		{
			float angle = index / 4.0f * Mathf.Tau + 0.4f;
			AddMesh(vent, $"EmberPebble{index}", new SphereMesh { Radius = 0.14f, Height = 0.2f }, new Vector3(Mathf.Cos(angle) * 1.5f, 0.08f, Mathf.Sin(angle) * 1.5f), Vector3.Zero, Vector3.One, index % 2 == 0 ? _matEmber : _matObsidian);
		}

		if (withLight)
		{
			vent.AddChild(new OmniLight3D
			{
				Name = "EmberLight",
				LightColor = new Color(1.0f, 0.42f, 0.12f),
				LightEnergy = 1.1f,
				OmniRange = 7.0f,
				Position = new Vector3(0.0f, 0.9f, 0.0f),
			});
		}
	}

	private void CreateDryShrub(Vector3 position)
	{
		var shrub = new Node3D
		{
			Name = "DryShrub",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(shrub);

		int bladeCount = _rng.RandiRange(4, 7);
		for (int index = 0; index < bladeCount; index++)
		{
			float height = (float)_rng.RandfRange(0.28f, 0.6f);
			AddMesh(
				shrub,
				"DryBlade",
				BoxMeshFor(new Vector3(0.04f, height, 0.016f)),
				new Vector3((float)_rng.RandfRange(-0.4f, 0.4f), height * 0.5f, (float)_rng.RandfRange(-0.4f, 0.4f)),
				new Vector3((float)_rng.RandfRange(-16.0f, 16.0f), (float)_rng.RandfRange(0.0f, 360.0f), (float)_rng.RandfRange(-24.0f, 24.0f)),
				Vector3.One,
				_matDryGrass);
		}
		AddMesh(shrub, "Twig", CylinderMeshFor(0.015f, 0.03f, 0.5f), new Vector3(0.0f, 0.25f, 0.0f), new Vector3(0.0f, 0.0f, (float)_rng.RandfRange(-30.0f, 30.0f)), Vector3.One, _matDeadWood);
	}

	private void CreatePebbleCluster(Vector3 position, Material material)
	{
		var cluster = new Node3D { Name = "PebbleCluster", Position = position };
		_propsRoot.AddChild(cluster);
		int pebbleCount = _rng.RandiRange(3, 5);
		for (int index = 0; index < pebbleCount; index++)
		{
			float radius = (float)_rng.RandfRange(0.09f, 0.22f);
			AddMesh(
				cluster,
				"Pebble",
				new SphereMesh { Radius = radius, Height = radius * 1.3f },
				new Vector3((float)_rng.RandfRange(-0.5f, 0.5f), radius * 0.4f, (float)_rng.RandfRange(-0.5f, 0.5f)),
				Vector3.Zero,
				new Vector3(1.2f, 0.55f, 1.0f),
				material);
		}
	}

	private void CreateObsidianSpike(Vector3 position)
	{
		var spike = new Node3D
		{
			Name = "ObsidianSpike",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(spike);

		int spikeCount = _rng.RandiRange(2, 3);
		for (int index = 0; index < spikeCount; index++)
		{
			float height = (float)_rng.RandfRange(0.6f, 1.3f);
			AddMesh(
				spike,
				"Spike",
				CylinderMeshFor(0.0f, 0.16f, height),
				new Vector3((float)_rng.RandfRange(-0.35f, 0.35f), height * 0.5f, (float)_rng.RandfRange(-0.35f, 0.35f)),
				new Vector3((float)_rng.RandfRange(-14.0f, 14.0f), 0.0f, (float)_rng.RandfRange(-14.0f, 14.0f)),
				Vector3.One,
				_matObsidian);
		}
	}

	// ---------------------------------------------------------------- snow props

	private void CreatePineTree(Vector3 position)
	{
		var pine = new StaticBody3D
		{
			Name = "PineTree",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(pine);

		float scale = (float)_rng.RandfRange(0.85f, 1.3f);
		AddMesh(pine, "Trunk", CylinderMeshFor(0.16f * scale, 0.24f * scale, 1.2f * scale), new Vector3(0.0f, 0.6f * scale, 0.0f), Vector3.Zero, Vector3.One, _matTrunk);
		float[] tierRadii = { 1.35f, 1.05f, 0.72f };
		float[] tierHeights = { 1.15f, 1.0f, 0.95f };
		float y = 1.0f * scale;
		for (int index = 0; index < tierRadii.Length; index++)
		{
			float radius = tierRadii[index] * scale;
			float height = tierHeights[index] * scale;
			AddMesh(pine, $"PineTier{index}", CylinderMeshFor(0.02f, radius, height), new Vector3(0.0f, y + height * 0.5f, 0.0f), Vector3.Zero, Vector3.One, _matPineLeaf);
			// Snow rim resting on each tier.
			AddMesh(pine, $"SnowRim{index}", CylinderMeshFor(radius * 0.6f, radius * 0.94f, 0.09f), new Vector3(0.0f, y + 0.10f, 0.0f), Vector3.Zero, Vector3.One, _matSnowCover);
			y += height * 0.72f;
		}
		AddMesh(pine, "SnowTip", CylinderMeshFor(0.0f, 0.16f * scale, 0.3f * scale), new Vector3(0.0f, y + 0.5f * scale, 0.0f), Vector3.Zero, Vector3.One, _matSnowCover);

		pine.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, 1.3f * scale, 0.0f),
			Shape = new BoxShape3D { Size = new Vector3(0.85f * scale, 2.6f * scale, 0.85f * scale) },
		});
	}

	private void CreateSnowRock(Vector3 position)
	{
		var rock = new StaticBody3D { Name = "SnowRock", Position = position };
		_propsRoot.AddChild(rock);

		var size = new Vector3(
			(float)_rng.RandfRange(1.0f, 2.4f),
			(float)_rng.RandfRange(0.6f, 1.3f),
			(float)_rng.RandfRange(1.0f, 2.2f));
		float yaw = (float)_rng.RandfRange(0.0f, 360.0f);
		AddMesh(rock, "RockBody", BoxMeshFor(size), new Vector3(0.0f, size.Y * 0.5f, 0.0f), new Vector3(0.0f, yaw, 0.0f), Vector3.One, _matRock);
		AddMesh(rock, "SnowCap", BoxMeshFor(new Vector3(size.X * 0.96f, 0.14f, size.Z * 0.96f)), new Vector3(0.0f, size.Y + 0.07f, 0.0f), new Vector3(0.0f, yaw, 0.0f), Vector3.One, _matSnowCover);

		rock.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, size.Y * 0.5f, 0.0f),
			Shape = new BoxShape3D { Size = size },
		});
	}

	private void CreateSnowLump(Vector3 position)
	{
		var lump = new Node3D { Name = "SnowLump", Position = position };
		_propsRoot.AddChild(lump);
		int lumpCount = _rng.RandiRange(1, 3);
		for (int index = 0; index < lumpCount; index++)
		{
			float radius = (float)_rng.RandfRange(0.3f, 0.7f);
			AddMesh(
				lump,
				"Lump",
				new SphereMesh { Radius = radius, Height = radius * 1.2f },
				new Vector3((float)_rng.RandfRange(-0.6f, 0.6f), radius * 0.32f, (float)_rng.RandfRange(-0.6f, 0.6f)),
				Vector3.Zero,
				new Vector3(1.25f, 0.5f, 1.1f),
				_matSnowCover);
		}
	}

	private void CreateIceShardMonolith(Vector3 position)
	{
		var monolith = new StaticBody3D { Name = "IceShardMonolith", Position = position };
		_propsRoot.AddChild(monolith);

		float[] heights = { 5.2f, 3.8f, 3.0f, 2.2f };
		float[] radii = { 0.85f, 0.62f, 0.5f, 0.4f };
		for (int index = 0; index < heights.Length; index++)
		{
			float angle = index / (float)heights.Length * Mathf.Tau + 0.5f;
			var offset = new Vector3(Mathf.Cos(angle) * (0.6f + index * 0.5f), 0.0f, Mathf.Sin(angle) * (0.6f + index * 0.5f));
			AddMesh(
				monolith,
				$"Shard{index}",
				CylinderMeshFor(0.0f, radii[index], heights[index]),
				offset + new Vector3(0.0f, heights[index] * 0.5f, 0.0f),
				new Vector3((float)_rng.RandfRange(-8.0f, 8.0f), index * 40.0f, (float)_rng.RandfRange(-8.0f, 8.0f)),
				Vector3.One,
				_matIceShard);
		}

		monolith.AddChild(new OmniLight3D
		{
			Name = "IceGlow",
			LightColor = new Color(0.55f, 0.80f, 1.0f),
			LightEnergy = 0.9f,
			OmniRange = 9.0f,
			Position = new Vector3(0.0f, 2.4f, 0.0f),
		});
		monolith.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, 2.4f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.9f, Height = 4.8f },
		});
	}

	private void CreateSnowman(Vector3 position)
	{
		var snowman = new StaticBody3D
		{
			Name = "Snowman",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(snowman);

		AddMesh(snowman, "Base", new SphereMesh { Radius = 0.62f, Height = 1.24f }, new Vector3(0.0f, 0.55f, 0.0f), Vector3.Zero, Vector3.One, _matSnowCover);
		AddMesh(snowman, "Body", new SphereMesh { Radius = 0.45f, Height = 0.9f }, new Vector3(0.0f, 1.35f, 0.0f), Vector3.Zero, Vector3.One, _matSnowCover);
		AddMesh(snowman, "Head", new SphereMesh { Radius = 0.3f, Height = 0.6f }, new Vector3(0.0f, 1.95f, 0.0f), Vector3.Zero, Vector3.One, _matSnowCover);
		AddMesh(snowman, "Nose", CylinderMeshFor(0.0f, 0.06f, 0.3f), new Vector3(0.0f, 1.95f, -0.38f), new Vector3(90.0f, 0.0f, 0.0f), Vector3.One, _matFlowerWarm);
		AddMesh(snowman, "EyeL", new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(-0.1f, 2.05f, -0.26f), Vector3.Zero, Vector3.One, _matActorDark);
		AddMesh(snowman, "EyeR", new SphereMesh { Radius = 0.04f, Height = 0.08f }, new Vector3(0.1f, 2.05f, -0.26f), Vector3.Zero, Vector3.One, _matActorDark);
		AddMesh(snowman, "ArmL", CylinderMeshFor(0.025f, 0.04f, 0.8f), new Vector3(-0.62f, 1.42f, 0.0f), new Vector3(0.0f, 0.0f, 62.0f), Vector3.One, _matDeadWood);
		AddMesh(snowman, "ArmR", CylinderMeshFor(0.025f, 0.04f, 0.8f), new Vector3(0.62f, 1.42f, 0.0f), new Vector3(0.0f, 0.0f, -62.0f), Vector3.One, _matDeadWood);

		snowman.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, 1.0f, 0.0f),
			Shape = new CylinderShape3D { Radius = 0.65f, Height = 2.0f },
		});
	}

	// ---------------------------------------------------------------- forest props

	private void CreateAncientTree(Vector3 position)
	{
		var tree = new StaticBody3D
		{
			Name = "AncientTree",
			Position = position,
			RotationDegrees = new Vector3(0.0f, (float)_rng.RandfRange(0.0f, 360.0f), 0.0f),
		};
		_propsRoot.AddChild(tree);

		AddMesh(tree, "Trunk", CylinderMeshFor(0.65f, 1.05f, 5.6f), new Vector3(0.0f, 2.8f, 0.0f), Vector3.Zero, Vector3.One, _matTrunk);
		for (int index = 0; index < 5; index++)
		{
			float angle = index / 5.0f * Mathf.Tau;
			AddMesh(tree, $"Root{index}", CylinderMeshFor(0.10f, 0.42f, 1.7f), new Vector3(Mathf.Cos(angle) * 1.05f, 0.6f, Mathf.Sin(angle) * 1.05f), new Vector3(Mathf.Sin(angle) * 42.0f, 0.0f, Mathf.Cos(angle) * 42.0f), Vector3.One, _matTrunk);
		}

		AddMesh(tree, "CrownCore", new SphereMesh { Radius = 3.4f, Height = 3.4f }, new Vector3(0.0f, 6.6f, 0.0f), Vector3.Zero, new Vector3(1.15f, 0.82f, 1.15f), _matLeaf);
		AddMesh(tree, "CrownEast", new SphereMesh { Radius = 2.2f, Height = 2.2f }, new Vector3(2.6f, 5.9f, 0.6f), Vector3.Zero, new Vector3(1.05f, 0.75f, 1.05f), _matGrassBright);
		AddMesh(tree, "CrownWest", new SphereMesh { Radius = 2.0f, Height = 2.0f }, new Vector3(-2.5f, 6.1f, -0.7f), Vector3.Zero, new Vector3(1.0f, 0.72f, 1.0f), _matLeaf);
		AddMesh(tree, "CrownTop", new SphereMesh { Radius = 1.7f, Height = 1.7f }, new Vector3(0.3f, 8.2f, 0.2f), Vector3.Zero, new Vector3(1.0f, 0.8f, 1.0f), _matGrassBright);
		AddMesh(tree, "VineA", CylinderMeshFor(0.04f, 0.04f, 2.4f), new Vector3(2.7f, 4.4f, 0.6f), Vector3.Zero, Vector3.One, _matMoss);
		AddMesh(tree, "VineB", CylinderMeshFor(0.04f, 0.04f, 2.0f), new Vector3(-2.4f, 4.6f, -0.7f), Vector3.Zero, Vector3.One, _matMoss);
		AddMesh(tree, "VineC", CylinderMeshFor(0.03f, 0.03f, 1.7f), new Vector3(0.4f, 4.2f, 2.4f), Vector3.Zero, Vector3.One, _matMoss);

		tree.AddChild(new OmniLight3D
		{
			Name = "AncientGlow",
			LightColor = new Color(0.75f, 1.0f, 0.55f),
			LightEnergy = 0.7f,
			OmniRange = 10.0f,
			Position = new Vector3(0.0f, 4.6f, 0.0f),
		});
		tree.AddChild(new CollisionShape3D
		{
			Position = new Vector3(0.0f, 2.8f, 0.0f),
			Shape = new CylinderShape3D { Radius = 1.5f, Height = 5.6f },
		});
	}
}
