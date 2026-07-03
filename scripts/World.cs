using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private static readonly string[] NpcNames =
	{
		"村莊守衛",
		"巡邏獵人",
		"旅途商人",
		"森林採集者",
		"修練學徒",
	};

	private static readonly string[] MonsterNames =
	{
		"野生史萊姆",
		"紅角獸",
		"荒野獵手",
		"洞穴狼",
		"毒牙小鬼",
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
		ScatterProps();
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
		CreateMesh(_mapRoot, "Pond", CylinderMeshFor(13.0f, 13.0f, 0.08f), new Vector3(-34.0f, 0.13f, 28.0f), _matWater);
		CreateStaticBox(_mapRoot, "WatchTowerBase", new Vector3(34.0f, 1.0f, -31.0f), new Vector3(7.0f, 2.0f, 7.0f), _matWall);

		Vector3 towerPosition = new(34.0f, 0.0f, -31.0f);
		CreateStaticBox(_mapRoot, "WatchTowerLevel", towerPosition + new Vector3(0.0f, 2.6f, 0.0f), new Vector3(5.0f, 0.8f, 5.0f), _matWall);
		CreateStaticBox(_mapRoot, "WatchTowerLevel", towerPosition + new Vector3(0.0f, 5.0f, 0.0f), new Vector3(5.0f, 0.8f, 5.0f), _matWall);

		_obstaclePositions.Add(new Vector3(-34.0f, 0.0f, 28.0f));
		_obstaclePositions.Add(new Vector3(34.0f, 0.0f, -31.0f));
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
			Shape = new CapsuleShape3D { Radius = 0.36f, Height = 1.75f },
		};
		actor.AddChild(collisionShape);

		var body = new MeshInstance3D
		{
			Name = "Body",
			Mesh = new CapsuleMesh { Radius = 0.38f, Height = 1.35f },
			Position = new Vector3(0.0f, 0.82f, 0.0f),
		};
		body.SetSurfaceOverrideMaterial(0, isMonster ? _matMonster : _matNpc);
		actor.AddChild(body);

		var head = new MeshInstance3D
		{
			Name = "Head",
			Mesh = new SphereMesh { Radius = 0.32f, Height = 0.64f },
			Position = new Vector3(0.0f, 1.63f, 0.0f),
		};
		head.SetSurfaceOverrideMaterial(0, isMonster ? _matMonster : _matNpc);
		actor.AddChild(head);

		var eyeBand = new MeshInstance3D
		{
			Name = "EyeBand",
			Mesh = BoxMeshFor(new Vector3(0.44f, 0.08f, 0.04f)),
			Position = new Vector3(0.0f, 1.67f, -0.29f),
		};
		eyeBand.SetSurfaceOverrideMaterial(0, _matActorDark);
		actor.AddChild(eyeBand);

		if (isMonster)
		{
			AddHorn(actor, new Vector3(-0.18f, 1.98f, -0.02f), new Vector3(20.0f, 0.0f, -20.0f));
			AddHorn(actor, new Vector3(0.18f, 1.98f, -0.02f), new Vector3(20.0f, 0.0f, 20.0f));
		}

		return actor;
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

		actor.ConfigureStats(displayName, level, maxHealth, attack, defense, experience, gold);
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

		var crosshair = new Control
		{
			Name = "Crosshair",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		crosshair.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		layer.AddChild(crosshair);

		var horizontal = new ColorRect
		{
			Color = new Color(1.0f, 1.0f, 1.0f, 0.82f),
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -9.0f,
			OffsetRight = 9.0f,
			OffsetTop = -1.0f,
			OffsetBottom = 1.0f,
		};
		crosshair.AddChild(horizontal);

		var vertical = new ColorRect
		{
			Color = horizontal.Color,
			AnchorLeft = 0.5f,
			AnchorRight = 0.5f,
			AnchorTop = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -1.0f,
			OffsetRight = 1.0f,
			OffsetTop = -9.0f,
			OffsetBottom = 9.0f,
		};
		crosshair.AddChild(vertical);
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
