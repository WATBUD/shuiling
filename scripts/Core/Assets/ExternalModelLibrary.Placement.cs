using Godot;

public static partial class ExternalModelLibrary
{
	public static bool TryAddPropModel(Node3D parent, string propKind, int variantSeed, Vector3 position, Vector3 scale)
	{
		string[] paths = propKind == "tree" ? TreeModels : RockModels;
		return TryAddFirstExisting(parent, paths, "ExternalModel", position, Vector3.Zero, scale, variantSeed);
	}

	public static bool TryAddModel(Node3D parent, string path, string nodeName, Vector3 position, Vector3 rotationDegrees, Vector3 scale)
	{
		if (!ResourceLoader.Exists(path) || HasInvalidImportRemap(path))
		{
			return false;
		}

		var packedScene = ResourceLoader.Load<PackedScene>(path);
		if (packedScene == null)
		{
			return false;
		}

		Node instance = packedScene.Instantiate();
		if (instance is not Node3D model)
		{
			instance.QueueFree();
			return false;
		}

		model.Name = nodeName;
		model.Position = position;
		model.RotationDegrees = rotationDegrees;
		model.Scale = scale;
		parent.AddChild(model);
		EnsureKayKitSkeletonAnimations(model, path);
		ApplyFallbackMaterials(model, path);
		TryPlayActorAnimation(model, "idle");
		return true;
	}

	private static bool TryAddFirstExisting(Node3D parent, string[] paths, string nodeName, Vector3 position, Vector3 rotationDegrees, Vector3 scale, int variantSeed)
	{
		if (parent.GetNodeOrNull<Node3D>(nodeName) != null)
		{
			return true;
		}

		int startIndex = paths.Length == 0 ? 0 : PositiveModulo(variantSeed, paths.Length);
		for (int offset = 0; offset < paths.Length; offset++)
		{
			string path = paths[(startIndex + offset) % paths.Length];
			if (IsBlockedActorPath(parent, path))
			{
				continue;
			}

			if (!ResourceLoader.Exists(path))
			{
				continue;
			}

			var packedScene = ResourceLoader.Load<PackedScene>(path);
			if (packedScene == null)
			{
				continue;
			}

			Node instance = packedScene.Instantiate();
			if (instance is not Node3D model)
			{
				instance.QueueFree();
				continue;
			}

			model.Name = nodeName;
			model.Position = position;
			model.RotationDegrees = rotationDegrees;
			model.Scale = GetModelSpecificScale(path, scale);
			parent.AddChild(model);
			EnsureKayKitSkeletonAnimations(model, path);
			ApplyFallbackMaterials(model, path);
			TryPlayActorAnimation(model, "idle");
			return true;
		}

		return false;
	}

	private static Vector3 GetModelSpecificScale(string path, Vector3 requestedScale)
	{
		if (path.Contains("/street_rat/", System.StringComparison.OrdinalIgnoreCase))
		{
			return requestedScale * 10.5f;
		}

		return requestedScale;
	}

	private static bool IsBlockedActorPath(Node3D parent, string path)
	{
		if (parent is not SimpleActor actor || actor.ActorKind != "npc")
		{
			return false;
		}

		return path.Contains("/monsters/") || path.Contains("Atlas_Monsters") || path.EndsWith(".gltf");
	}

	private static int PositiveModulo(int value, int divisor)
	{
		int result = value % divisor;
		return result < 0 ? result + divisor : result;
	}
}
