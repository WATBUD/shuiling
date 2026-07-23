using Godot;
using System.Collections.Generic;

public static class ExternalModelLibrary
{
	private static readonly Dictionary<ulong, AnimationPlayer?> AnimationPlayerCache = new();
	private static readonly Dictionary<ulong, Node3D?> RootMotionNodeCache = new();

	private static readonly string[] PlayerModels =
	{
		"res://assets/models/player/player_rogue_hooded.glb",
		"res://assets/models/player/player_knight.glb",
		"res://assets/models/player/player_mage.glb",
		"res://assets/models/player/player_barbarian.glb",
	};

	// Player-selectable characters for the creation screen (path + name locale
	// key). Includes the dedicated player models plus the humanoid character
	// models. The character-select screen filters out any that don't exist.
	public static readonly (string Path, string NameKey)[] SelectablePlayerModels =
	{
		("res://assets/models/player/player_rogue_hooded.glb", "character.rogue"),
		("res://assets/models/player/player_knight.glb", "character.knight"),
		("res://assets/models/player/player_mage.glb", "character.mage"),
		("res://assets/models/player/player_barbarian.glb", "character.barbarian"),
		("res://assets/models/characters/adventurer.gltf", "character.adventurer"),
		("res://assets/models/characters/archer.glb", "character.archer"),
		("res://assets/models/characters/knight.glb", "character.knight"),
		("res://assets/models/characters/barbarian.glb", "character.barbarian"),
		("res://assets/models/characters/mage.glb", "character.mage"),
		("res://assets/models/characters/rogue.glb", "character.rogue"),
		("res://assets/models/characters/guard.gltf", "character.guard"),
	};

	// Every model the player can pick on the character-select screen: humanoids
	// AND monsters/pets, so you can play as a human or a creature. Deduplicated
	// by display name (so the same character/model never appears twice) and
	// filtered to models that exist. Returns (path, display name).
	public static List<(string Path, string Display)> GetAvailableCharacterModels()
	{
		var result = new List<(string Path, string Display)>();
		var seenKeys = new HashSet<string>();

		void TryAdd(string path, string display)
		{
			if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path) || HasInvalidImportRemap(path))
			{
				return;
			}

			// Dedup by a filename-derived identity so the SAME creature never
			// appears twice even across folders (e.g. player_barbarian.glb and
			// characters/barbarian.glb both map to "barbarian").
			if (!seenKeys.Add(CanonicalModelKey(path)))
			{
				return;
			}

			result.Add((path, display));
		}

		// Humanoids first, with localized names.
		foreach ((string path, string nameKey) in SelectablePlayerModels)
		{
			TryAdd(path, LocaleText.T(nameKey));
		}

		// Then every monster / pet model, with a localized name where known.
		foreach (string path in MonsterMelee)
		{
			TryAdd(path, MonsterModelDisplay(path));
		}
		foreach (string path in MonsterRanged)
		{
			TryAdd(path, MonsterModelDisplay(path));
		}

		// Scan the whole cube-pets folder so EVERY pet model is selectable
		// (not just the handful referenced by the combat pools).
		foreach (string path in ListModelFiles("res://assets/models/pets/cube_pets/"))
		{
			TryAdd(path, MonsterModelDisplay(path));
		}

		return result;
	}

	private static List<string> ListModelFiles(string directory)
	{
		var files = new List<string>();
		using DirAccess dir = DirAccess.Open(directory);
		if (dir == null)
		{
			return files;
		}

		dir.ListDirBegin();
		for (string name = dir.GetNext(); !string.IsNullOrEmpty(name); name = dir.GetNext())
		{
			if (dir.CurrentIsDir())
			{
				continue;
			}

			if (name.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase)
				|| name.EndsWith(".gltf", System.StringComparison.OrdinalIgnoreCase))
			{
				files.Add(directory + name);
			}
		}

		dir.ListDirEnd();
		return files;
	}

	// Localized monster/pet names keyed by canonical model id, so a Chinese
	// locale shows Chinese names instead of the English filename.
	private static readonly Dictionary<string, string> MonsterModelNameKeys = new()
	{
		["rat street"] = "character.mob.rat",
		["lion"] = "character.mob.lion",
		["tiger"] = "character.mob.tiger",
		["polar"] = "character.mob.polar_bear",
		["hog"] = "character.mob.hog",
		["fox"] = "character.mob.fox",
		["orc"] = "character.mob.orc",
		["golem"] = "character.mob.golem",
		["beast"] = "character.mob.beast",
		["slime"] = "character.mob.slime",
		["demon"] = "character.mob.demon",
		["wolf"] = "character.mob.wolf",
		["bee"] = "character.mob.bee",
		["parrot"] = "character.mob.parrot",
		["crab"] = "character.mob.crab",
		["fish"] = "character.mob.fish",
		["imp"] = "character.mob.imp",
		["spitter"] = "character.mob.spitter",
		["blue demon"] = "character.mob.blue_demon",
		["dragon"] = "character.mob.dragon",
		["ghost"] = "character.mob.ghost",
		["beaver"] = "character.mob.beaver",
		["bunny"] = "character.mob.bunny",
		["cat"] = "character.mob.cat",
		["caterpillar"] = "character.mob.caterpillar",
		["chick"] = "character.mob.chick",
		["cow"] = "character.mob.cow",
		["deer"] = "character.mob.deer",
		["dog"] = "character.mob.dog",
		["elephant"] = "character.mob.elephant",
		["giraffe"] = "character.mob.giraffe",
		["koala"] = "character.mob.koala",
		["monkey"] = "character.mob.monkey",
		["panda"] = "character.mob.panda",
		["penguin"] = "character.mob.penguin",
		["pig"] = "character.mob.pig",
	};

	private static string MonsterModelDisplay(string path)
	{
		return MonsterModelNameKeys.TryGetValue(CanonicalModelKey(path), out string? nameKey)
			? LocaleText.T(nameKey)
			: PrettifyModelName(path);
	}

	// --- monster card identity (卡片系統) ------------------------------------
	// One card per visually-distinct model: the canonical key collapses cosmetic
	// variants so the same creature never yields two different cards.
	public static string CardKeyFromModelPath(string path)
	{
		return string.IsNullOrWhiteSpace(path) ? string.Empty : CanonicalModelKey(path);
	}

	private static Dictionary<string, string>? _cardKeyToModelPath;

	// Reverse lookup: canonical card key → a concrete model path, so the album can
	// instantiate a 3D preview from a stored card key. Cached after first build.
	public static string GetModelPathForCardKey(string cardKey)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return string.Empty;
		}

		if (_cardKeyToModelPath == null)
		{
			_cardKeyToModelPath = new Dictionary<string, string>();
			foreach ((string path, string _) in GetAvailableCharacterModels())
			{
				string key = CanonicalModelKey(path);
				if (!_cardKeyToModelPath.ContainsKey(key))
				{
					_cardKeyToModelPath[key] = path;
				}
			}
		}

		return _cardKeyToModelPath.TryGetValue(cardKey, out string? modelPath) ? modelPath : string.Empty;
	}

	// Fixed, stable list of the named monster-card keys (used to assign each NPC a
	// specific card it will accept for its quest). Ordered for determinism.
	public static IReadOnlyList<string> KnownCardKeys
	{
		get
		{
			var keys = new List<string>(MonsterModelNameKeys.Keys);
			keys.Sort(System.StringComparer.Ordinal);
			return keys;
		}
	}

	// Localized card name. Accepts a canonical model key; falls back to a raw
	// locale key (species DisplayName) or a prettified token.
	public static string LocalizedCardName(string cardKey)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return LocaleText.T("card.unknown");
		}

		if (MonsterModelNameKeys.TryGetValue(cardKey, out string? nameKey))
		{
			return LocaleText.T(nameKey);
		}

		return cardKey.Contains('.') ? LocaleText.T(cardKey) : PrettifyModelName(cardKey);
	}

	// Canonical identity of a model, ignoring folder, extension, the "player_"
	// prefix and cosmetic/size tokens — so visually-identical models collapse.
	private static string CanonicalModelKey(string path)
	{
		string file = path;
		int slash = file.LastIndexOf('/');
		if (slash >= 0)
		{
			file = file[(slash + 1)..];
		}
		int dot = file.IndexOf('.');
		if (dot >= 0)
		{
			file = file[..dot];
		}

		file = file.Replace('-', ' ').Replace('_', ' ').ToLowerInvariant();
		var kept = new List<string>();
		foreach (string token in file.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
		{
			if (token is "player" or "hooded" or "1k" or "poly" or "pizza" or "enemy" or "animal")
			{
				continue;
			}

			kept.Add(token);
		}

		kept.Sort(System.StringComparer.Ordinal);
		return string.Join(" ", kept);
	}

	private static string PrettifyModelName(string path)
	{
		string file = path;
		int slash = file.LastIndexOf('/');
		if (slash >= 0)
		{
			file = file[(slash + 1)..];
		}
		int dot = file.IndexOf('.');
		if (dot >= 0)
		{
			file = file[..dot];
		}

		file = file.Replace('-', ' ').Replace('_', ' ');
		var words = new List<string>();
		foreach (string token in file.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
		{
			string lower = token.ToLowerInvariant();
			if (lower is "1k" or "poly" or "pizza" or "enemy" or "animal")
			{
				continue;
			}

			words.Add(char.ToUpperInvariant(token[0]) + (token.Length > 1 ? token[1..] : string.Empty));
		}

		return words.Count > 0 ? string.Join(" ", words) : file;
	}

	// Instantiate a model for a UI preview (character select). Applies fallback
	// materials + idle animation; caller positions/scales it.
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

		ApplyFallbackMaterials(model, path);
		TryPlayActorAnimation(model, "idle");
		return model;
	}

	private static readonly string[] NpcMelee =
	{
		"res://assets/models/characters/knight.glb",
		"res://assets/models/characters/barbarian.glb",
		"res://assets/models/characters/rogue.glb",
	};

	private static readonly string[] NpcRanged =
	{
		"res://assets/models/characters/archer.glb",
		"res://assets/models/characters/rogue.glb",
	};

	private static readonly string[] NpcSupport =
	{
		"res://assets/models/characters/mage.glb",
	};

	private static readonly string[] MonsterMelee =
	{
		"res://assets/models/monsters/street_rat/street_rat_1k.gltf",
		"res://assets/models/pets/cube_pets/animal-lion.glb",
		"res://assets/models/pets/cube_pets/animal-tiger.glb",
		"res://assets/models/pets/cube_pets/animal-polar.glb",
		"res://assets/models/pets/cube_pets/animal-hog.glb",
		"res://assets/models/pets/cube_pets/animal-fox.glb",
		"res://assets/models/monsters/orc.gltf",
		"res://assets/models/monsters/golem.gltf",
		"res://assets/models/monsters/beast.gltf",
		"res://assets/models/monsters/slime_enemy_poly_pizza.glb",
		"res://assets/models/monsters/slime.gltf",
		"res://assets/models/monsters/demon.gltf",
		"res://assets/models/monsters/orc.glb",
		"res://assets/models/monsters/wolf.glb",
		"res://assets/models/monsters/golem.glb",
		"res://assets/models/monsters/beast.glb",
		"res://assets/models/monsters/slime.glb",
	};

	private static readonly string[] MonsterRanged =
	{
		"res://assets/models/pets/cube_pets/animal-bee.glb",
		"res://assets/models/pets/cube_pets/animal-parrot.glb",
		"res://assets/models/pets/cube_pets/animal-crab.glb",
		"res://assets/models/pets/cube_pets/animal-fish.glb",
		"res://assets/models/monsters/imp.gltf",
		"res://assets/models/monsters/spitter.gltf",
		"res://assets/models/monsters/blue_demon.gltf",
		"res://assets/models/monsters/demon.gltf",
		"res://assets/models/monsters/imp.glb",
		"res://assets/models/monsters/spitter.glb",
		"res://assets/models/monsters/dragon.glb",
		"res://assets/models/monsters/ghost.glb",
	};

	private static readonly string[] SlimeMonsterModels =
	{
		"res://assets/models/monsters/slime_enemy_poly_pizza.glb",
		"res://assets/models/monsters/slime.gltf",
	};

	private static readonly string[] TreeModels =
	{
		"res://assets/models/environment/tree.glb",
		"res://assets/models/environment/tree_01.glb",
		"res://assets/models/environment/oak_tree.glb",
		"res://assets/models/environment/pine_tree.glb",
	};

	private static readonly string[] RockModels =
	{
		"res://assets/models/environment/rock.glb",
		"res://assets/models/environment/rock_01.glb",
		"res://assets/models/environment/boulder.glb",
		"res://assets/models/environment/stone.glb",
	};

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
		ApplyFallbackMaterials(model, path);
		TryPlayActorAnimation(model, "idle");
		return model;
	}

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
		ApplyFallbackMaterials(model, path);
		return true;
	}

	private static bool HasInvalidImportRemap(string path)
	{
		string importPath = $"{path}.import";
		if (!FileAccess.FileExists(importPath))
		{
			return false;
		}

		string importText = FileAccess.GetFileAsString(importPath);
		return importText.Contains("valid=false");
	}

	public static bool TryPlayActorAnimation(Node root, string state)
	{
		if (GetCachedAnimationPlayer(root) is not AnimationPlayer player)
		{
			return false;
		}

		string? animationName = FindAnimationName(player, state);
		if (string.IsNullOrEmpty(animationName))
		{
			return false;
		}

		ConfigureAnimationLoop(player, animationName, state);
		if (player.CurrentAnimation != animationName || !player.IsPlaying())
		{
			player.Play(animationName);
		}

		return true;
	}

	public static void StabilizeRootMotion(Node3D model, Vector3 localPosition, Vector3 localRotationDegrees)
	{
		model.Position = localPosition;
		model.RotationDegrees = localRotationDegrees;

		if (GetCachedRootMotionNode(model) is Node3D rootMotionNode)
		{
			rootMotionNode.Position = Vector3.Zero;
			rootMotionNode.Rotation = Vector3.Zero;
		}
	}

	private static void ConfigureAnimationLoop(AnimationPlayer player, string animationName, string state)
	{
		Animation? animation = player.GetAnimation(animationName);
		if (animation == null)
		{
			return;
		}

		animation.LoopMode = state is "walk" or "run" or "idle"
			? Animation.LoopModeEnum.Linear
			: Animation.LoopModeEnum.None;
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

	private static void ApplyFallbackMaterials(Node root, string sourcePath)
	{
		ApplyFallbackMaterialsRecursive(root, sourcePath.ToLowerInvariant(), root.Name.ToString().ToLowerInvariant());
	}

	private static void ApplyFallbackMaterialsRecursive(Node node, string sourcePath, string inheritedName)
	{
		string nodeName = node.Name.ToString().ToLowerInvariant();
		string materialKey = $"{inheritedName} {nodeName}";
		if (node is MeshInstance3D meshInstance && meshInstance.Mesh != null)
		{
			int surfaceCount = meshInstance.Mesh.GetSurfaceCount();
			for (int surfaceIndex = 0; surfaceIndex < surfaceCount; surfaceIndex++)
			{
				Material? material = meshInstance.GetSurfaceOverrideMaterial(surfaceIndex) ?? meshInstance.Mesh.SurfaceGetMaterial(surfaceIndex);
				if (HasUsefulMaterialColor(material))
				{
					continue;
				}

				meshInstance.SetSurfaceOverrideMaterial(surfaceIndex, PickFallbackMaterial(sourcePath, materialKey, surfaceIndex));
			}
		}

		foreach (Node child in node.GetChildren())
		{
			ApplyFallbackMaterialsRecursive(child, sourcePath, materialKey);
		}
	}

	private static bool HasUsefulMaterialColor(Material? material)
	{
		if (material == null)
		{
			return false;
		}

		if (material is not StandardMaterial3D standardMaterial)
		{
			return true;
		}

		if (standardMaterial.AlbedoTexture != null)
		{
			return true;
		}

		Color color = standardMaterial.AlbedoColor;
		float max = Mathf.Max(color.R, Mathf.Max(color.G, color.B));
		float min = Mathf.Min(color.R, Mathf.Min(color.G, color.B));
		float saturation = max <= 0.001f ? 0.0f : (max - min) / max;
		bool nearlyWhite = max > 0.78f && saturation < 0.12f;
		bool nearlyFlatGray = max > 0.28f && saturation < 0.08f;
		return !nearlyWhite && !nearlyFlatGray;
	}

	private static StandardMaterial3D PickFallbackMaterial(string sourcePath, string meshName, int surfaceIndex)
	{
		string key = $"{sourcePath} {meshName}";

		if (key.Contains("leaf") || key.Contains("leaves") || key.Contains("tree") || key.Contains("hedge") || key.Contains("grass"))
		{
			return ModelMaterial(new Color(0.18f, 0.50f, 0.18f));
		}

		if (key.Contains("trunk") || key.Contains("wood") || key.Contains("fence") || key.Contains("cart") || key.Contains("door") || key.Contains("staff") || key.Contains("bow"))
		{
			return ModelMaterial(new Color(0.42f, 0.26f, 0.13f));
		}

		if (key.Contains("roof") || key.Contains("stall-red") || key.Contains("banner-red"))
		{
			return ModelMaterial(new Color(0.66f, 0.16f, 0.14f));
		}

		if (key.Contains("stall-green") || key.Contains("banner-green"))
		{
			return ModelMaterial(new Color(0.16f, 0.48f, 0.22f));
		}

		if (key.Contains("window") || key.Contains("glass") || key.Contains("crystal"))
		{
			return ModelMaterial(new Color(0.36f, 0.78f, 0.96f, 0.82f), 0.18f, true);
		}

		if (key.Contains("water") || key.Contains("fountain"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.34f, 0.62f, 0.78f, 0.62f) : new Color(0.52f, 0.52f, 0.47f), 0.16f, surfaceIndex % 2 == 0);
		}

		if (key.Contains("rock") || key.Contains("stone") || key.Contains("wall") || key.Contains("chimney") || key.Contains("stairs"))
		{
			return ModelMaterial(new Color(0.48f, 0.47f, 0.42f));
		}

		if (key.Contains("sword") || key.Contains("shield") || key.Contains("armor") || key.Contains("metal") || key.Contains("blade"))
		{
			return ModelMaterial(new Color(0.68f, 0.72f, 0.74f), 0.34f);
		}

		if (key.Contains("skin") || key.Contains("head") || key.Contains("face") || key.Contains("hand"))
		{
			return ModelMaterial(new Color(0.82f, 0.58f, 0.40f));
		}

		if (key.Contains("mage"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.32f, 0.22f, 0.66f) : new Color(0.86f, 0.66f, 0.26f));
		}

		if (key.Contains("rogue"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.12f, 0.18f, 0.22f) : new Color(0.44f, 0.24f, 0.14f));
		}

		if (key.Contains("knight") || key.Contains("guard"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.20f, 0.34f, 0.62f) : new Color(0.70f, 0.72f, 0.68f), surfaceIndex % 2 == 1 ? 0.36f : 0.78f);
		}

		if (key.Contains("barbarian") || key.Contains("warrior"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.48f, 0.22f, 0.10f) : new Color(0.24f, 0.13f, 0.08f));
		}

		if (key.Contains("street_rat"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.38f, 0.28f, 0.22f) : new Color(0.72f, 0.58f, 0.48f));
		}

		if (key.Contains("animal-fox"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.92f, 0.34f, 0.12f) : new Color(0.96f, 0.84f, 0.70f));
		}

		if (key.Contains("animal-deer") || key.Contains("animal-beaver") || key.Contains("animal-hog"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.54f, 0.32f, 0.16f) : new Color(0.78f, 0.58f, 0.36f));
		}

		if (key.Contains("animal-bunny"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.90f, 0.80f, 0.72f) : new Color(1.0f, 0.74f, 0.82f));
		}

		if (key.Contains("animal-crab"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.86f, 0.18f, 0.12f) : new Color(1.0f, 0.42f, 0.24f));
		}

		if (key.Contains("animal-fish"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.22f, 0.66f, 0.92f) : new Color(0.76f, 0.92f, 1.0f));
		}

		if (key.Contains("animal-caterpillar"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.32f, 0.70f, 0.18f) : new Color(0.78f, 0.94f, 0.34f));
		}

		if (key.Contains("animal-bee"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(1.0f, 0.78f, 0.12f) : new Color(0.10f, 0.10f, 0.08f));
		}

		if (key.Contains("animal-lion") || key.Contains("animal-tiger"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.90f, 0.46f, 0.12f) : new Color(0.18f, 0.12f, 0.08f));
		}

		if (key.Contains("animal-polar"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.86f, 0.92f, 1.0f) : new Color(0.56f, 0.64f, 0.72f));
		}

		if (key.Contains("animal-elephant"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.50f, 0.56f, 0.60f) : new Color(0.74f, 0.76f, 0.74f));
		}

		if (key.Contains("monster") || key.Contains("orc") || key.Contains("demon") || key.Contains("imp") || key.Contains("beast") || key.Contains("wolf"))
		{
			return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.64f, 0.15f, 0.12f) : new Color(0.25f, 0.08f, 0.07f));
		}

		if (key.Contains("slime") || key.Contains("ghost") || key.Contains("spitter"))
		{
			return ModelMaterial(new Color(0.24f, 0.78f, 0.74f, 0.76f), 0.16f, true);
		}

		return ModelMaterial(surfaceIndex % 2 == 0 ? new Color(0.54f, 0.42f, 0.28f) : new Color(0.76f, 0.58f, 0.32f));
	}

	private static StandardMaterial3D ModelMaterial(Color color, float roughness = 0.78f, bool transparent = false)
	{
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			Roughness = roughness,
		};

		if (transparent || color.A < 1.0f)
		{
			material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		}

		return material;
	}

	private static AnimationPlayer? FindAnimationPlayer(Node root)
	{
		if (root is AnimationPlayer player)
		{
			return player;
		}

		foreach (Node child in root.GetChildren())
		{
			AnimationPlayer? found = FindAnimationPlayer(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static AnimationPlayer? GetCachedAnimationPlayer(Node root)
	{
		ulong instanceId = root.GetInstanceId();
		if (AnimationPlayerCache.TryGetValue(instanceId, out AnimationPlayer? cachedPlayer))
		{
			if (cachedPlayer == null || GodotObject.IsInstanceValid(cachedPlayer))
			{
				return cachedPlayer;
			}

			AnimationPlayerCache.Remove(instanceId);
		}

		AnimationPlayer? player = FindAnimationPlayer(root);
		AnimationPlayerCache[instanceId] = player;
		return player;
	}

	private static Node3D? FindRootMotionNode(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is Node3D childNode3D && IsRootMotionNodeName(childNode3D.Name.ToString()))
			{
				return childNode3D;
			}

			Node3D? found = FindRootMotionNode(child);
			if (found != null)
			{
				return found;
			}
		}

		return null;
	}

	private static Node3D? GetCachedRootMotionNode(Node3D model)
	{
		ulong instanceId = model.GetInstanceId();
		if (RootMotionNodeCache.TryGetValue(instanceId, out Node3D? cachedNode))
		{
			if (cachedNode == null || GodotObject.IsInstanceValid(cachedNode))
			{
				return cachedNode;
			}

			RootMotionNodeCache.Remove(instanceId);
		}

		Node3D? rootMotionNode = FindRootMotionNode(model);
		RootMotionNodeCache[instanceId] = rootMotionNode;
		return rootMotionNode;
	}

	private static bool IsRootMotionNodeName(string name)
	{
		string lowerName = name.ToLowerInvariant();
		return lowerName is "root" or "armature" or "skeleton3d" or "scene root"
			|| lowerName.Contains("root")
			|| lowerName.Contains("armature")
			|| lowerName.Contains("mixamorig");
	}

	private static string? FindAnimationName(AnimationPlayer player, string state)
	{
		string[] preferredNames = state switch
		{
			"shoot" => new[] { "2H_Ranged_Shoot", "1H_Ranged_Shoot", "2H_Ranged_Shooting", "1H_Ranged_Shooting", "Bow_Shoot", "Crossbow_Shoot", "Shoot", "shoot", "Ranged_Attack", "Attack_Ranged" },
			"cast" => new[] { "Spellcast_Raise", "Spellcast_Shoot", "Cast", "cast", "Magic", "magic" },
			"attack" => new[] { "1H_Melee_Attack_Chop", "2H_Melee_Attack_Chop", "Unarmed_Melee_Attack_Punch", "Attack", "attack", "Melee", "Punch" },
			"death" => new[] { "Death_A", "Death", "death", "Die", "die", "Dead", "defeat" },
			"run" => new[] { "Running_A", "Run", "run", "Running", "running", "Sprint", "sprint" },
			"walk" => new[] { "Walking_A", "Walk", "walk", "Walking", "walking" },
			_ => new[] { "Idle_A", "Idle", "idle", "Standing", "stand", "Rest", "rest" },
		};

		foreach (string preferredName in preferredNames)
		{
			if (player.HasAnimation(preferredName))
			{
				return preferredName;
			}
		}

		string stateToken = state switch
		{
			"death" => "die",
			"shoot" => "shoot",
			"cast" => "cast",
			_ => state,
		};
		foreach (StringName animation in player.GetAnimationList())
		{
			string animationName = animation.ToString();
			string lowerName = animationName.ToLowerInvariant();
			if (lowerName.Contains(stateToken)
				|| (state == "attack" && (lowerName.Contains("melee") || lowerName.Contains("punch") || lowerName.Contains("chop")))
				|| (state == "shoot" && (lowerName.Contains("bow") || lowerName.Contains("ranged")))
				|| (state == "cast" && (lowerName.Contains("spell") || lowerName.Contains("magic")))
				|| (state == "idle" && lowerName.Contains("stand")))
			{
				return animationName;
			}
		}

		if (state == "run")
		{
			return FindAnimationName(player, "walk");
		}

		return state == "walk" ? FindAnimationName(player, "idle") : null;
	}

	private static int PositiveModulo(int value, int divisor)
	{
		int result = value % divisor;
		return result < 0 ? result + divisor : result;
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
