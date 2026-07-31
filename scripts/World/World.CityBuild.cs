using Godot;
using System.Collections.Generic;

public partial class World : Node3D
{
	private void CreateMainCity()
	{
		Vector3 center = _mainCityCenter;
		const float shopRadius = 31.0f;
		// Keep the city center visually clean: one large plaza only. The former
		// nested garden/edge/walk discs overlapped and produced noisy circular
		// bands. Fountain/water and portal circles are authored separately below.
		CreateMesh(_mapRoot, "MainCityPlaza", CylinderMeshFor(33.0f, 33.0f, 0.10f), center + new Vector3(0.0f, FlatWalkableCenterY(0.10f, 0.006f), 0.0f), _matCobblestone);
		CreateCityRoad("CitySouthSpoke", center + new Vector3(0.0f, 0.0f, 29.0f), new Vector2(12.4f, 22.0f));
		CreateMesh(_mapRoot, "CityPortalPlazaEdge", CylinderMeshFor(9.4f, 9.4f, 0.09f), center + new Vector3(0.0f, FlatWalkableCenterY(0.09f, 0.018f), -28.0f), _matRoadEdge);
		CreateMesh(_mapRoot, "CityPortalPlaza", CylinderMeshFor(7.8f, 7.8f, 0.10f), center + new Vector3(0.0f, FlatWalkableCenterY(0.10f, 0.020f), -28.0f), _matCobblestone);
		CreateBanner(center + new Vector3(-5.2f, 0.0f, -27.6f), 8.0f, _matCrystal);
		CreateBanner(center + new Vector3(5.2f, 0.0f, -27.6f), -8.0f, _matCrystal);
		CreateTorch(center + new Vector3(-7.1f, 0.0f, -24.4f));
		CreateTorch(center + new Vector3(7.1f, 0.0f, -24.4f));

		// 主城改為六屋圍繞：六棟建築等距（每 60°）環繞中央，並整體偏移 30° 讓正北（180°）
		// 的傳送廣場走廊保持淨空。順序沿環：30 道具、90 強化屋、150 傭兵、210 寵物、270 倉庫、330 鐵匠。
		// 傭兵公會已移除；其清單併入夥伴招募所（pet shop）。150° 留給日後擴充。
		Vector3 itemShopOffset = RingOffset(30.0f, shopRadius);
		Vector3 refinementOffset = RingOffset(90.0f, shopRadius);
		Vector3 petShopOffset = RingOffset(210.0f, shopRadius);
		Vector3 warehouseOffset = RingOffset(270.0f, shopRadius);
		Vector3 blacksmithOffset = RingOffset(330.0f, shopRadius);
		CreateItemShop(center + itemShopOffset, YawFacingCenter(itemShopOffset));
		CreateRefinementHouse(center + refinementOffset, YawFacingCenter(refinementOffset));
		CreatePetShop(center + petShopOffset, YawFacingCenter(petShopOffset));
		CreateWarehouseBuilding(center + warehouseOffset, YawFacingCenter(warehouseOffset));
		CreateBlacksmithShop(center + blacksmithOffset, YawFacingCenter(blacksmithOffset));

		for (int index = 0; index < 8; index++)
		{
			Vector3 offset = RingOffset(index * 45.0f + 22.5f, 23.5f);
			CreateTorch(center + offset);
		}

		for (int index = 0; index < 8; index++)
		{
			// The south ring position overlaps the city portal at z = -28.
			// Leave that slot empty so a small street lamp never grows out of
			// the center of the teleport effect.
			if (index == 4)
			{
				continue;
			}

			Vector3 offset = RingOffset(index * 45.0f, 29.0f);
			CreateExternalProp($"CityRingLantern{index}", "res://assets/models/environment/lantern.glb", center + offset, Vector3.Zero, new Vector3(1.18f, 1.18f, 1.18f), new Vector3(0.6f, 2.2f, 0.6f), new Vector3(0.0f, 1.1f, 0.0f));
		}

		CreateCityFountain(center);
		CreateCityMarket(center);
		CreateCityGardens(center);

		// 訓練場稻草人放在城鎮外圈，避免夥伴經過中央廣場時誤觸戰鬥。
		Vector3 dummyOffset = RingOffset(150.0f, 35.0f);
		CreateTrainingDummy(center + dummyOffset, YawFacingCenter(dummyOffset));

		_obstaclePositions.Add(center);
	}

	private void CreateTrainingDummy(Vector3 position, float yawDegrees)
	{
		// 用一般怪物 actor 當受擊本體（沿用近戰／投射物的命中判定與傷害數字），
		// 但標記為訓練稻草人：被動、不主動攻擊、受擊只顯示數字不扣血。
		SimpleActor dummy = CreateActor(true, "city", "name.training_dummy", "Tank", 1, 1);
		dummy.Name = "TrainingDummy";
		dummy.IsTrainingDummy = true;
		dummy.SetPassive(true);
		dummy.WanderRadius = 0.0f;
		dummy.MoveSpeed = 0.0f;
		// 防禦 0 → 顯示接近原始傷害；血量很高但反正永不扣血。
		dummy.ConfigureStats("name.training_dummy", 1, 999999, 0, 0, 0, 0);
		dummy.ConfigureCombatProfile("Tank", "personality.calm", "ability.none", 0);
		if (dummy.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") is CollisionShape3D dummyCollision)
		{
			dummyCollision.Position = new Vector3(0.0f, 1.25f, 0.0f);
			dummyCollision.Shape = new CapsuleShape3D { Radius = 0.62f, Height = 2.5f };
		}
		dummy.Position = position;
		dummy.HomePosition = position;
		dummy.RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f);
		_actorsRoot.AddChild(dummy);

		// 簡單的木架裝飾，讓它看起來像訓練場的標靶。
		var frame = new Node3D { Name = "TrainingDummyPlatform", Position = position, RotationDegrees = new Vector3(0.0f, yawDegrees, 0.0f) };
		_propsRoot.AddChild(frame);
		AddMesh(frame, "DummyBase", CylinderMeshFor(1.05f, 1.16f, 0.22f), new Vector3(0.0f, 0.11f, 0.0f), Vector3.Zero, Vector3.One, _matCobblestone);
	}

	private void CreateCityRoad(string name, Vector3 center, Vector2 size)
	{
		CreateMesh(_mapRoot, $"{name}Edge", BoxMeshFor(new Vector3(size.X + 2.0f, 0.075f, size.Y + 2.0f)), center + new Vector3(0.0f, FlatWalkableCenterY(0.075f, 0.014f), 0.0f), _matRoadEdge);
		CreateMesh(_mapRoot, name, BoxMeshFor(new Vector3(size.X, 0.08f, size.Y)), center + new Vector3(0.0f, FlatWalkableCenterY(0.08f, 0.016f), 0.0f), _matCobblestone);
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
		// 主城中央水池：站在旁邊按 E 可復活已死亡的夥伴（PlayerController 依此群組偵測）。
		fountain.AddToGroup("revival_fountain");

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

	private void CreateRefinementHouse(Vector3 position, float yawDegrees)
	{
		StaticBody3D shop = CreateCityShopShell(
			"CityRefinementHouse",
			position,
			yawDegrees,
			new Vector3(7.8f, 3.1f, 6.0f),
			_matWall,
			_matCrystal,
			"shop.refinement",
			new Color(0.62f, 0.82f, 1.0f)
		);

		// 鐵砧 + 懸浮的發光強化水晶，象徵精煉裝備。
		AddMesh(shop, "RefineAnvilBase", BoxMeshFor(new Vector3(0.95f, 0.5f, 0.6f)), new Vector3(0.0f, 0.25f, -3.2f), Vector3.Zero, Vector3.One, _matMetal);
		AddMesh(shop, "RefineAnvilTop", BoxMeshFor(new Vector3(1.3f, 0.28f, 0.66f)), new Vector3(0.0f, 0.62f, -3.2f), Vector3.Zero, Vector3.One, _matMetal);
		AddMesh(shop, "RefineCrystal", new SphereMesh { Radius = 0.34f, Height = 0.9f }, new Vector3(0.0f, 1.35f, -3.2f), Vector3.Zero, new Vector3(1.0f, 1.7f, 1.0f), _matCrystal);
		AddMesh(shop, "RefineShardLeft", CylinderMeshFor(0.0f, 0.16f, 0.72f), new Vector3(-2.7f, 1.08f, -3.3f), new Vector3(0.0f, 0.0f, -12.0f), Vector3.One, _matCrystal);
		AddMesh(shop, "RefineShardRight", CylinderMeshFor(0.0f, 0.16f, 0.72f), new Vector3(2.7f, 1.08f, -3.3f), new Vector3(0.0f, 0.0f, 12.0f), Vector3.One, _matCrystal);
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

}
