using Godot;
using System.Collections.Generic;

public partial class World
{
	private const string PrototypeKitRoot =
		"res://assets/models/environment/kenney_prototype/";
	private const float WalkableVisualSurfaceY = 0.002f;

	private static readonly Dictionary<string, Mesh?> PrototypeMeshCache = new();

	// The original collision ground remains authoritative. These two checker
	// batches are visual-only and turn the flat colour slab into readable terrain
	// tiles without creating hundreds of individual scene nodes.
	private void CreatePrototypeGround(string mapId)
	{
		(Color first, Color second, float tileSize) = mapId switch
		{
			"city" => (
				new Color(0.29f, 0.33f, 0.39f),
				new Color(0.38f, 0.42f, 0.48f),
				4.0f),
			"wild_marsh" => (
				new Color(0.12f, 0.25f, 0.23f),
				new Color(0.16f, 0.31f, 0.27f),
				7.5f),
			"wild_badlands" => (
				new Color(0.34f, 0.22f, 0.15f),
				new Color(0.43f, 0.29f, 0.18f),
				7.5f),
			"wild_snow" => (
				new Color(0.69f, 0.77f, 0.84f),
				new Color(0.83f, 0.88f, 0.92f),
				7.5f),
			"wild_skeleton" => (
				new Color(0.16f, 0.15f, 0.20f),
				new Color(0.25f, 0.22f, 0.30f),
				7.5f),
			_ => (
				new Color(0.17f, 0.32f, 0.20f),
				new Color(0.23f, 0.39f, 0.24f),
				7.5f),
		};

		Mesh? floorMesh = LoadPrototypeMesh("floor-square.glb");
		if (floorMesh == null)
		{
			return;
		}

		int tilesPerSide = Mathf.CeilToInt(MapSize / tileSize);
		float start = -tilesPerSide * tileSize * 0.5f + tileSize * 0.5f;
		var lightTiles = new List<Transform3D>();
		var darkTiles = new List<Transform3D>();
		for (int x = 0; x < tilesPerSide; x++)
		{
			for (int z = 0; z < tilesPerSide; z++)
			{
				Vector3 position = new(start + x * tileSize, WalkableVisualSurfaceY * 0.5f, start + z * tileSize);
				var transform = new Transform3D(
					Basis.Identity.Scaled(new Vector3(tileSize * 0.992f, 1.0f, tileSize * 0.992f)),
					position);
				((x + z) % 2 == 0 ? lightTiles : darkTiles).Add(transform);
			}
		}

		AddPrototypeFloorBatch($"{mapId}PrototypeFloorA", floorMesh, lightTiles, MakeMaterial(first, 0.96f));
		AddPrototypeFloorBatch($"{mapId}PrototypeFloorB", floorMesh, darkTiles, MakeMaterial(second, 0.96f));
	}

	private static float FlatWalkableCenterY(float meshHeight, float surfaceY = WalkableVisualSurfaceY)
	{
		return surfaceY - meshHeight * 0.5f;
	}

	private void AddPrototypeFloorBatch(string name, Mesh mesh, List<Transform3D> transforms, Material material)
	{
		var multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = mesh,
			InstanceCount = transforms.Count,
		};
		for (int index = 0; index < transforms.Count; index++)
		{
			multiMesh.SetInstanceTransform(index, transforms[index]);
		}

		var instance = new MultiMeshInstance3D
		{
			Name = name,
			Multimesh = multiMesh,
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
		};
		_mapRoot.AddChild(instance);
	}

	private void CreatePrototypeArchitecture(string mapId)
	{
		if (mapId == "city")
		{
			CreatePrototypeCityArchitecture();
		}
	}

	private void CreatePrototypeCityArchitecture()
	{
		Vector3 center = _mainCityCenter;
		for (int index = 0; index < 8; index++)
		{
			float angle = index / 8.0f * Mathf.Tau;
			Vector3 offset = new(Mathf.Cos(angle) * 38.0f, 0.0f, Mathf.Sin(angle) * 38.0f);
			CreatePrototypeStatic(
				$"PrototypeCityColumn{index}",
				index % 2 == 0 ? "column-rounded.glb" : "column.glb",
				center + offset,
				Vector3.Zero,
				new Vector3(2.3f, 4.2f, 2.3f),
				new Vector3(1.25f, 4.2f, 1.25f));
		}

		CreatePrototypeModel("PrototypeCityCrates", "crate-color.glb", center + new Vector3(27.0f, 0.0f, 19.0f), new Vector3(0.0f, 18.0f, 0.0f), new Vector3(2.0f, 2.0f, 2.0f));
		CreatePrototypeModel("PrototypeCityFlagWest", "flag.glb", CityPortalPosition + new Vector3(-5.2f, 0.0f, 0.0f), Vector3.Zero, new Vector3(2.2f, 2.2f, 2.2f));
		CreatePrototypeModel("PrototypeCityFlagEast", "flag.glb", CityPortalPosition + new Vector3(5.2f, 0.0f, 0.0f), Vector3.Zero, new Vector3(2.2f, 2.2f, 2.2f));
	}

	private void CreatePrototypeStatic(string name, string file, Vector3 position, Vector3 rotation, Vector3 scale, Vector3 collisionSize)
	{
		CreateExternalProp(
			name,
			PrototypeKitRoot + file,
			position,
			rotation,
			scale,
			collisionSize,
			new Vector3(0.0f, collisionSize.Y * 0.5f, 0.0f));
		_obstaclePositions.Add(position);
	}

	private void CreatePrototypeModel(string name, string file, Vector3 position, Vector3 rotation, Vector3 scale)
	{
		ExternalModelLibrary.TryAddModel(_propsRoot, PrototypeKitRoot + file, name, position, rotation, scale);
	}

	private static Mesh? LoadPrototypeMesh(string file)
	{
		if (PrototypeMeshCache.TryGetValue(file, out Mesh? cached))
		{
			return cached;
		}

		string path = PrototypeKitRoot + file;
		if (!ResourceLoader.Exists(path) || ResourceLoader.Load<PackedScene>(path) is not PackedScene scene)
		{
			PrototypeMeshCache[file] = null;
			return null;
		}

		Node instance = scene.Instantiate();
		Mesh? mesh = FindFirstMesh(instance);
		instance.Free();
		PrototypeMeshCache[file] = mesh;
		return mesh;
	}

	private static Mesh? FindFirstMesh(Node node)
	{
		if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
		{
			return meshInstance.Mesh;
		}

		foreach (Node child in node.GetChildren())
		{
			Mesh? mesh = FindFirstMesh(child);
			if (mesh != null)
			{
				return mesh;
			}
		}

		return null;
	}
}
