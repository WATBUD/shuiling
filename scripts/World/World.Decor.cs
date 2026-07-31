using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private void CreateRuinSite()
	{
		Vector3 center = new(-45.0f, 0.0f, -34.0f);
		// Structural floor, walls, columns and stairs are supplied by the Kenney
		// Prototype Kit in CreatePrototypeArchitecture.
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
}
