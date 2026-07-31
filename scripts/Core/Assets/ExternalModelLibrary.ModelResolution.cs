using Godot;
using System.Collections.Generic;

public static partial class ExternalModelLibrary
{
	public static Node3D? InstantiatePreviewModel(string path)
	{
		if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path) || HasInvalidImportRemap(path))
		{
			return null;
		}

		var packedScene = ResourceLoader.Load<PackedScene>(path);
		if (packedScene == null || packedScene.Instantiate() is not Node3D model)
		{
			return null;
		}

		EnsureKayKitSkeletonAnimations(model, path);
		ApplyFallbackMaterials(model, path);
		TryPlayActorAnimation(model, "idle");
		return model;
	}

	public static bool TryAddActorModel(SimpleActor actor)
	{
		string[] paths = actor.ActorKind == "monster"
			? GetMonsterModelPool(actor)
			: actor.CombatRole == "Support" ? NpcSupport : actor.IsRangedCombatant ? NpcRanged : NpcMelee;
		Vector3 scale = GetActorModelScale(actor);
		return TryAddFirstExisting(actor, paths, "ExternalModel", Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f), scale, GetActorVariantSeed(actor));
	}

	// Force a specific model (used to give each city NPC a unique model).
	public static bool TryAddActorModel(SimpleActor actor, string forcedPath)
	{
		if (string.IsNullOrEmpty(forcedPath))
		{
			return TryAddActorModel(actor);
		}

		Vector3 scale = GetActorModelScale(actor);
		return TryAddFirstExisting(actor, new[] { forcedPath }, "ExternalModel", Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f), scale, 0);
	}

	// Distinct humanoid NPC models that exist on disk (deduped across pools).
	public static List<string> GetDistinctNpcModels()
	{
		var seen = new HashSet<string>();
		var result = new List<string>();
		foreach (string path in NpcMelee)
		{
			AddIfNew(path);
		}
		foreach (string path in NpcRanged)
		{
			AddIfNew(path);
		}
		foreach (string path in NpcSupport)
		{
			AddIfNew(path);
		}

		return result;

		void AddIfNew(string path)
		{
			if (seen.Add(path) && ResourceLoader.Exists(path) && !HasInvalidImportRemap(path))
			{
				result.Add(path);
			}
		}
	}

	private static string[] GetMonsterModelPool(SimpleActor actor)
	{
		string displayName = actor.DisplayName ?? string.Empty;
		string[]? matchedModels = MonsterSpeciesCatalog.Current.GetModelPaths(displayName);
		if (matchedModels != null)
		{
			return matchedModels;
		}

		if (displayName.Contains("slime", System.StringComparison.OrdinalIgnoreCase) || displayName.Contains("史萊姆"))
		{
			return SlimeMonsterModels;
		}

		return actor.IsRangedCombatant ? MonsterRanged : MonsterMelee;
	}

	private static Vector3 GetActorModelScale(SimpleActor actor)
	{
		if (actor.ActorKind != "monster")
		{
			return new Vector3(1.05f, 1.05f, 1.05f);
		}

		return actor.MapId switch
		{
			"wild_marsh" => new Vector3(0.96f, 0.96f, 0.96f),
			"wild_badlands" => new Vector3(1.12f, 1.12f, 1.12f),
			"wild_snow" => new Vector3(1.08f, 1.08f, 1.08f),
			"wild_skeleton" => new Vector3(1.10f, 1.10f, 1.10f),
			_ => new Vector3(1.04f, 1.04f, 1.04f),
		};
	}

	private static int GetActorVariantSeed(SimpleActor actor)
	{
		unchecked
		{
			int seed = (int)actor.GetInstanceId();
			seed = seed * 397 ^ StableStringHash(actor.MapId);
			seed = seed * 397 ^ StableStringHash(actor.DisplayName);
			return seed;
		}
	}

	public static Node3D? TryAddPlayerModel(Node3D player, string preferredPath = "")
	{
		if (player.GetNodeOrNull<Node3D>("PlayerExternalModel") != null)
		{
			return player.GetNode<Node3D>("PlayerExternalModel");
		}

		// Try the player's chosen model first (from character select / save),
		// then fall back to the default player model list.
		if (!string.IsNullOrEmpty(preferredPath) && TryBuildPlayerModel(player, preferredPath) is Node3D chosen)
		{
			return chosen;
		}

		foreach (string path in PlayerModels)
		{
			if (TryBuildPlayerModel(player, path) is Node3D model)
			{
				return model;
			}
		}

		return null;
	}

	private static Node3D? TryBuildPlayerModel(Node3D player, string path)
	{
		if (!ResourceLoader.Exists(path) || HasInvalidImportRemap(path))
		{
			return null;
		}

		var packedScene = ResourceLoader.Load<PackedScene>(path);
		if (packedScene == null || packedScene.Instantiate() is not Node3D model)
		{
			return null;
		}

		model.Name = "PlayerExternalModel";
		model.Position = Vector3.Zero;
		model.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
		model.Scale = new Vector3(0.88f, 0.88f, 0.88f);
		player.AddChild(model);
		EnsureKayKitSkeletonAnimations(model, path);
		ApplyFallbackMaterials(model, path);
		TryPlayActorAnimation(model, "idle");
		return model;
	}

	private static int StableStringHash(string value)
	{
		unchecked
		{
			int hash = 23;
			for (int index = 0; index < value.Length; index++)
			{
				hash = hash * 31 + value[index];
			}

			return hash;
		}
	}
}
