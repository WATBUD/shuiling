using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private static readonly string[] NpcNames =
	{
		"name.npc.guard",
		"name.npc.hunter",
		"name.npc.merchant",
		"name.npc.gatherer",
		"name.npc.apprentice",
	};

	private static readonly string[] MonsterNames =
	{
		"name.monster.slime",
		"name.monster.water_spirit",
		"name.monster.redhorn",
		"name.monster.hunter",
		"name.monster.wolf",
		"name.monster.imp",
		"name.monster.dragon",
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
		"Tank",
		"Gatherer",
		"Builder",
		"DPS",
	};

	private static readonly string[] MonsterRoles =
	{
		"DPS",
		"DPS",
		"Tank",
		"Ranged",
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
	[Export] public int ActorCount { get; set; } = 36;
	[Export(PropertyHint.Range, "0,1,0.05")] public float MonsterRatio { get; set; } = 0.45f;
	[Export] public int SeedValue { get; set; }

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Vector3> _obstaclePositions = new();

	private Node3D _mapRoot = null!;
	private Node3D _propsRoot = null!;
	private Node3D _actorsRoot = null!;

	private StandardMaterial3D _matGround = null!;
	private StandardMaterial3D _matPath = null!;
	private StandardMaterial3D _matWall = null!;
	private StandardMaterial3D _matTrunk = null!;
	private StandardMaterial3D _matLeaf = null!;
	private StandardMaterial3D _matRock = null!;
	private StandardMaterial3D _matWater = null!;
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
		if (SeedValue == 0)
		{
			_rng.Randomize();
		}
		else
		{
			_rng.Seed = (ulong)SeedValue;
		}

		CreateMaterials();
		BuildEnvironment();
		BuildMap();
		CreatePlayer();
		SpawnActors();
		AddCrosshair();
	}

	private void CreateMaterials()
	{
		_matGround = MakeMaterial(new Color(0.24f, 0.48f, 0.30f));
		_matPath = MakeMaterial(new Color(0.44f, 0.36f, 0.25f));
		_matWall = MakeMaterial(new Color(0.36f, 0.38f, 0.40f));
		_matTrunk = MakeMaterial(new Color(0.33f, 0.21f, 0.12f));
		_matLeaf = MakeMaterial(new Color(0.12f, 0.44f, 0.22f));
		_matRock = MakeMaterial(new Color(0.43f, 0.44f, 0.43f));
		_matWater = MakeMaterial(new Color(0.16f, 0.43f, 0.70f, 0.72f), 0.08f);
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
	}

	private void BuildEnvironment()
	{
		var environment = new Environment
		{
			BackgroundMode = Environment.BGMode.Color,
			BackgroundColor = new Color(0.58f, 0.72f, 0.86f),
			AmbientLightSource = Environment.AmbientSource.Color,
			AmbientLightColor = new Color(0.82f, 0.88f, 0.92f),
			AmbientLightEnergy = 0.55f,
			FogEnabled = true,
			FogDensity = 0.012f,
		};

		var worldEnvironment = new WorldEnvironment
		{
			Name = "WorldEnvironment",
			Environment = environment,
		};
		AddChild(worldEnvironment);

		var sun = new DirectionalLight3D
		{
			Name = "Sun",
			LightEnergy = 2.4f,
			RotationDegrees = new Vector3(-50.0f, -35.0f, 0.0f),
		};
		AddChild(sun);
	}

	private void BuildMap()
	{
		_mapRoot = new Node3D { Name = "Map" };
		AddChild(_mapRoot);

		_propsRoot = new Node3D { Name = "Props" };
		_mapRoot.AddChild(_propsRoot);

		_actorsRoot = new Node3D { Name = "Actors" };
		AddChild(_actorsRoot);

		CreateStaticBox(_mapRoot, "Ground", new Vector3(0.0f, -0.5f, 0.0f), new Vector3(MapSize, 1.0f, MapSize), _matGround);
		CreateBoundaries();
		CreateLandmarks();
		CreateSpawnCamp();
		CreateMainCity();
		CreateRuinSite();
		CreateMonsterDen();
		ScatterProps();
		ScatterDetailProps();
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
		CreateMesh(_mapRoot, "MainPathNS", BoxMeshFor(new Vector3(8.0f, 0.08f, MapSize - 12.0f)), new Vector3(0.0f, 0.04f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "MainPathEW", BoxMeshFor(new Vector3(MapSize - 12.0f, 0.08f, 8.0f)), new Vector3(0.0f, 0.05f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "SpawnPlaza", CylinderMeshFor(12.0f, 12.0f, 0.12f), new Vector3(0.0f, 0.09f, 0.0f), _matPath);
		CreateMesh(_mapRoot, "PondBank", CylinderMeshFor(16.0f, 16.0f, 0.08f), new Vector3(-34.0f, 0.10f, 28.0f), _matPondBank);
		CreateMesh(_mapRoot, "Pond", CylinderMeshFor(13.0f, 13.0f, 0.08f), new Vector3(-34.0f, 0.13f, 28.0f), _matWater);
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
		Vector3 center = new(0.0f, 0.0f, -20.0f);
		CreateMesh(_mapRoot, "MainCityPlaza", CylinderMeshFor(15.5f, 15.5f, 0.12f), center + new Vector3(0.0f, 0.11f, 0.0f), _matPath);
		CreateStaticBox(_propsRoot, "CityNorthGate", center + new Vector3(0.0f, 1.85f, -10.2f), new Vector3(8.5f, 3.7f, 1.1f), _matWall);
		CreateStaticBox(_propsRoot, "CityLeftHouse", center + new Vector3(-9.4f, 1.35f, -1.6f), new Vector3(5.0f, 2.7f, 5.6f), _matWood);
		CreateStaticBox(_propsRoot, "CityRightHouse", center + new Vector3(9.4f, 1.35f, -1.6f), new Vector3(5.0f, 2.7f, 5.6f), _matWood);
		AddMesh(_propsRoot, "CityLeftRoof", CylinderMeshFor(0.0f, 3.8f, 2.1f), center + new Vector3(-9.4f, 3.45f, -1.6f), Vector3.Zero, new Vector3(1.0f, 0.72f, 1.0f), _matTentCloth);
		AddMesh(_propsRoot, "CityRightRoof", CylinderMeshFor(0.0f, 3.8f, 2.1f), center + new Vector3(9.4f, 3.45f, -1.6f), Vector3.Zero, new Vector3(1.0f, 0.72f, 1.0f), _matTentCloth);
		CreateStaticBox(_propsRoot, "CityClinic", center + new Vector3(0.0f, 1.25f, 7.0f), new Vector3(6.4f, 2.5f, 4.6f), _matWall);
		AddMesh(_propsRoot, "ClinicRoof", CylinderMeshFor(0.0f, 4.0f, 1.8f), center + new Vector3(0.0f, 3.15f, 7.0f), Vector3.Zero, new Vector3(1.0f, 0.68f, 0.78f), _matNpcAccent);
		AddMesh(_propsRoot, "ClinicCrossVertical", BoxMeshFor(new Vector3(0.18f, 0.72f, 0.06f)), center + new Vector3(0.0f, 2.0f, 4.66f), Vector3.Zero, Vector3.One, _matCrystal);
		AddMesh(_propsRoot, "ClinicCrossHorizontal", BoxMeshFor(new Vector3(0.58f, 0.18f, 0.065f)), center + new Vector3(0.0f, 2.0f, 4.62f), Vector3.Zero, Vector3.One, _matCrystal);
		CreateRevivalNpc(center + new Vector3(0.0f, 0.0f, 2.7f));
		CreateBanner(center + new Vector3(-5.2f, 0.0f, 2.6f), 18.0f, _matCrystal);
		CreateBanner(center + new Vector3(5.2f, 0.0f, 2.6f), -18.0f, _matCrystal);
		CreateTorch(center + new Vector3(-6.9f, 0.0f, 6.0f));
		CreateTorch(center + new Vector3(6.9f, 0.0f, 6.0f));

		_obstaclePositions.Add(center);
		_obstaclePositions.Add(center + new Vector3(-9.4f, 0.0f, -1.6f));
		_obstaclePositions.Add(center + new Vector3(9.4f, 0.0f, -1.6f));
		_obstaclePositions.Add(center + new Vector3(0.0f, 0.0f, 7.0f));
	}

	private void CreateRevivalNpc(Vector3 position)
	{
		var npc = new StaticBody3D
		{
			Name = "PetRevivalNpc",
			Position = position,
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
			Text = "Pet Revival",
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

			if (_rng.Randf() < 0.68f)
			{
				CreateTree(position);
			}
			else
			{
				CreateRock(position);
			}

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

			float roll = _rng.Randf();
			if (roll < 0.48f)
			{
				CreateGrassPatch(position);
			}
			else if (roll < 0.72f)
			{
				CreateFlowerPatch(position);
			}
			else if (roll < 0.88f)
			{
				CreateMushroom(position);
			}
			else
			{
				CreateCrystalCluster(position, (float)_rng.RandfRange(0.42f, 0.72f), _matCrystal);
			}
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
			Position = new Vector3(0.0f, 0.0f, 8.0f),
		};

		var collisionShape = new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Position = new Vector3(0.0f, 0.9f, 0.0f),
			Shape = new CapsuleShape3D { Radius = 0.38f, Height = 1.8f },
		};
		player.AddChild(collisionShape);

		var cameraPivot = new Node3D
		{
			Name = "CameraPivot",
			Position = new Vector3(0.0f, 1.58f, 0.0f),
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
	}

	private void SpawnActors()
	{
		for (int index = 0; index < ActorCount; index++)
		{
			bool isMonster = _rng.Randf() < MonsterRatio;
			SimpleActor actor = CreateActor(isMonster);
			Vector3 spawnPosition = FindOpenSpawnPosition();
			actor.Position = spawnPosition;
			actor.HomePosition = spawnPosition;
			_actorsRoot.AddChild(actor);
		}
	}

	private SimpleActor CreateActor(bool isMonster)
	{
		var actor = new SimpleActor
		{
			Name = isMonster ? "Monster" : "NPC",
			ActorKind = isMonster ? "monster" : "npc",
			MoveSpeed = isMonster ? (float)_rng.RandfRange(2.0f, 3.1f) : (float)_rng.RandfRange(1.1f, 1.8f),
			WanderRadius = (float)_rng.RandfRange(8.0f, 17.0f),
		};
		ConfigureActorStats(actor, isMonster);

		var collisionShape = new CollisionShape3D
		{
			Name = "CollisionShape3D",
			Position = new Vector3(0.0f, 0.9f, 0.0f),
			Shape = new CapsuleShape3D
			{
				Radius = isMonster ? 0.52f : 0.34f,
				Height = isMonster ? 1.72f : 1.78f,
			},
		};
		actor.AddChild(collisionShape);

		if (isMonster)
		{
			BuildMonsterVisual(actor);
		}
		else
		{
			BuildNpcVisual(actor);
		}

		return actor;
	}

	private void BuildNpcVisual(Node3D actor)
	{
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

	private void ConfigureActorStats(SimpleActor actor, bool isMonster)
	{
		int level = isMonster ? _rng.RandiRange(2, 10) : _rng.RandiRange(1, 7);
		int maxHealth = isMonster
			? 95 + level * 22 + _rng.RandiRange(0, 35)
			: 70 + level * 14 + _rng.RandiRange(0, 24);
		int attack = isMonster
			? 9 + level * 4 + _rng.RandiRange(0, 5)
			: 5 + level * 2 + _rng.RandiRange(0, 3);
		int defense = isMonster
			? 5 + level * 3 + _rng.RandiRange(0, 4)
			: 4 + level * 2 + _rng.RandiRange(0, 3);
		int experience = isMonster ? level * 9 + _rng.RandiRange(3, 12) : level * 4 + _rng.RandiRange(1, 5);
		int gold = isMonster ? level * 3 + _rng.RandiRange(0, 8) : level + _rng.RandiRange(0, 4);
		string[] namePool = isMonster ? MonsterNames : NpcNames;
		string displayName = namePool[_rng.RandiRange(0, namePool.Length - 1)];
		string[] abilityPool = isMonster ? MonsterAbilities : NpcAbilities;
		string specialAbility = abilityPool[_rng.RandiRange(0, abilityPool.Length - 1)];
		string[] rolePool = isMonster ? MonsterRoles : NpcRoles;
		string combatRole = rolePool[_rng.RandiRange(0, rolePool.Length - 1)];
		string personality = Personalities[_rng.RandiRange(0, Personalities.Length - 1)];
		string passiveAbility = PassiveAbilities[_rng.RandiRange(0, PassiveAbilities.Length - 1)];
		int affinity = _rng.RandiRange(isMonster ? 24 : 42, isMonster ? 72 : 88);

		actor.ConfigureStats(displayName, level, maxHealth, attack, defense, experience, gold);
		actor.ConfigureGrowth(specialAbility, _rng.RandiRange(1, 2));
		actor.ConfigureCombatProfile(combatRole, personality, passiveAbility, affinity);
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

	private Vector3 FindOpenSpawnPosition()
	{
		float half = MapSize * 0.5f - 9.0f;

		for (int attempt = 0; attempt < 90; attempt++)
		{
			var position = new Vector3(
				(float)_rng.RandfRange(-half, half),
				0.0f,
				(float)_rng.RandfRange(-half, half)
			);

			if (position.DistanceTo(new Vector3(0.0f, 0.0f, 8.0f)) < 18.0f)
			{
				continue;
			}

			if (IsPositionClear(position, 3.4f))
			{
				return position;
			}
		}

		return new Vector3((float)_rng.RandfRange(-half, half), 0.0f, (float)_rng.RandfRange(-half, half));
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

	private void CreateTree(Vector3 position)
	{
		var tree = new StaticBody3D
		{
			Name = "Tree",
			Position = position,
		};
		_propsRoot.AddChild(tree);

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
}
