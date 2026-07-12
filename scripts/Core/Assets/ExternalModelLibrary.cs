using Godot;

public static class ExternalModelLibrary
{
	private static readonly string[] PlayerModels =
	{
		"res://assets/models/player/player_rogue_hooded.glb",
		"res://assets/models/player/player_knight.glb",
		"res://assets/models/player/player_mage.glb",
		"res://assets/models/player/player_barbarian.glb",
	};

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

	private static readonly string[] RatMonsterModels = { "res://assets/models/monsters/street_rat/street_rat_1k.gltf" };
	private static readonly string[] FoxMonsterModels = { "res://assets/models/pets/cube_pets/animal-fox.glb" };
	private static readonly string[] DeerMonsterModels = { "res://assets/models/pets/cube_pets/animal-deer.glb" };
	private static readonly string[] BunnyMonsterModels = { "res://assets/models/pets/cube_pets/animal-bunny.glb" };
	private static readonly string[] BeaverMonsterModels = { "res://assets/models/pets/cube_pets/animal-beaver.glb" };
	private static readonly string[] BoarMonsterModels = { "res://assets/models/pets/cube_pets/animal-hog.glb" };
	private static readonly string[] CrabMonsterModels = { "res://assets/models/pets/cube_pets/animal-crab.glb" };
	private static readonly string[] FishMonsterModels = { "res://assets/models/pets/cube_pets/animal-fish.glb" };
	private static readonly string[] CaterpillarMonsterModels = { "res://assets/models/pets/cube_pets/animal-caterpillar.glb" };
	private static readonly string[] BeeMonsterModels = { "res://assets/models/pets/cube_pets/animal-bee.glb" };
	private static readonly string[] LionMonsterModels = { "res://assets/models/pets/cube_pets/animal-lion.glb" };
	private static readonly string[] TigerMonsterModels = { "res://assets/models/pets/cube_pets/animal-tiger.glb" };
	private static readonly string[] BearMonsterModels = { "res://assets/models/pets/cube_pets/animal-polar.glb" };
	private static readonly string[] ElephantMonsterModels = { "res://assets/models/pets/cube_pets/animal-elephant.glb" };

	private static readonly string[] ForestMonsterModels =
	{
		"res://assets/models/monsters/street_rat/street_rat_1k.gltf",
		"res://assets/models/pets/cube_pets/animal-fox.glb",
		"res://assets/models/pets/cube_pets/animal-deer.glb",
		"res://assets/models/pets/cube_pets/animal-bunny.glb",
		"res://assets/models/pets/cube_pets/animal-beaver.glb",
		"res://assets/models/pets/cube_pets/animal-hog.glb",
		"res://assets/models/monsters/beast.gltf",
		"res://assets/models/monsters/orc.gltf",
	};

	private static readonly string[] MarshMonsterModels =
	{
		"res://assets/models/monsters/street_rat/street_rat_1k.gltf",
		"res://assets/models/pets/cube_pets/animal-crab.glb",
		"res://assets/models/pets/cube_pets/animal-fish.glb",
		"res://assets/models/pets/cube_pets/animal-caterpillar.glb",
		"res://assets/models/pets/cube_pets/animal-bee.glb",
		"res://assets/models/monsters/slime_enemy_poly_pizza.glb",
		"res://assets/models/monsters/slime.gltf",
		"res://assets/models/monsters/spitter.gltf",
		"res://assets/models/monsters/imp.gltf",
	};

	private static readonly string[] BadlandsMonsterModels =
	{
		"res://assets/models/pets/cube_pets/animal-lion.glb",
		"res://assets/models/pets/cube_pets/animal-tiger.glb",
		"res://assets/models/pets/cube_pets/animal-polar.glb",
		"res://assets/models/pets/cube_pets/animal-elephant.glb",
		"res://assets/models/monsters/golem.gltf",
		"res://assets/models/monsters/demon.gltf",
		"res://assets/models/monsters/blue_demon.gltf",
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

	private static string[] GetMonsterModelPool(SimpleActor actor)
	{
		string displayName = actor.DisplayName ?? string.Empty;
		string[]? matchedModels = GetMonsterModelsForName(displayName);
		if (matchedModels != null)
		{
			return matchedModels;
		}

		if (displayName.Contains("slime", System.StringComparison.OrdinalIgnoreCase) || displayName.Contains("史萊姆"))
		{
			return SlimeMonsterModels;
		}

		return actor.MapId switch
		{
			"wild_forest" => ForestMonsterModels,
			"wild_marsh" => MarshMonsterModels,
			"wild_badlands" => BadlandsMonsterModels,
			_ => actor.IsRangedCombatant ? MonsterRanged : MonsterMelee,
		};
	}

	private static string[]? GetMonsterModelsForName(string displayName)
	{
		return displayName switch
		{
			"name.monster.slime" => SlimeMonsterModels,
			"name.monster.rat" => RatMonsterModels,
			"name.monster.fox" => FoxMonsterModels,
			"name.monster.deer" => DeerMonsterModels,
			"name.monster.bunny" => BunnyMonsterModels,
			"name.monster.beaver" => BeaverMonsterModels,
			"name.monster.boar" => BoarMonsterModels,
			"name.monster.crab" => CrabMonsterModels,
			"name.monster.fish" => FishMonsterModels,
			"name.monster.caterpillar" => CaterpillarMonsterModels,
			"name.monster.bee" => BeeMonsterModels,
			"name.monster.lion" => LionMonsterModels,
			"name.monster.tiger" => TigerMonsterModels,
			"name.monster.bear" => BearMonsterModels,
			"name.monster.elephant" => ElephantMonsterModels,
			_ => null,
		};
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

	public static Node3D? TryAddPlayerModel(Node3D player)
	{
		if (player.GetNodeOrNull<Node3D>("PlayerExternalModel") != null)
		{
			return player.GetNode<Node3D>("PlayerExternalModel");
		}

		foreach (string path in PlayerModels)
		{
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

			model.Name = "PlayerExternalModel";
			model.Position = Vector3.Zero;
			model.RotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);
			model.Scale = new Vector3(0.88f, 0.88f, 0.88f);
			player.AddChild(model);
			ApplyFallbackMaterials(model, path);
			TryPlayActorAnimation(model, "idle");
			return model;
		}

		return null;
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
		if (FindAnimationPlayer(root) is not AnimationPlayer player)
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

		if (FindRootMotionNode(model) is Node3D rootMotionNode)
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

		return state is "run" or "walk" ? FindAnimationName(player, "idle") : null;
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
