using Godot;

public static class ExternalModelLibrary
{
	private static readonly string[] NpcMelee =
	{
		"res://assets/models/characters/knight.glb",
		"res://assets/models/characters/barbarian.glb",
		"res://assets/models/characters/rogue.glb",
		"res://assets/models/characters/warrior.glb",
		"res://assets/models/characters/guard.glb",
		"res://assets/models/characters/adventurer.glb",
		"res://assets/models/characters/warrior.gltf",
		"res://assets/models/characters/guard.gltf",
		"res://assets/models/characters/adventurer.gltf",
	};

	private static readonly string[] NpcRanged =
	{
		"res://assets/models/characters/archer.glb",
		"res://assets/models/characters/rogue.glb",
		"res://assets/models/characters/hunter.glb",
		"res://assets/models/characters/ranger.glb",
		"res://assets/models/characters/bowman.glb",
		"res://assets/models/characters/guard.gltf",
		"res://assets/models/characters/adventurer.gltf",
	};

	private static readonly string[] NpcSupport =
	{
		"res://assets/models/characters/mage.glb",
		"res://assets/models/characters/healer.glb",
		"res://assets/models/characters/wizard.glb",
		"res://assets/models/characters/apprentice.glb",
		"res://assets/models/characters/adventurer.gltf",
		"res://assets/models/characters/guard.gltf",
	};

	private static readonly string[] MonsterMelee =
	{
		"res://assets/models/monsters/orc.gltf",
		"res://assets/models/monsters/golem.gltf",
		"res://assets/models/monsters/beast.gltf",
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
		"res://assets/models/monsters/imp.gltf",
		"res://assets/models/monsters/spitter.gltf",
		"res://assets/models/monsters/blue_demon.gltf",
		"res://assets/models/monsters/demon.gltf",
		"res://assets/models/monsters/imp.glb",
		"res://assets/models/monsters/spitter.glb",
		"res://assets/models/monsters/dragon.glb",
		"res://assets/models/monsters/ghost.glb",
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
			? actor.IsRangedCombatant ? MonsterRanged : MonsterMelee
			: actor.CombatRole == "Support" ? NpcSupport : actor.IsRangedCombatant ? NpcRanged : NpcMelee;
		Vector3 scale = actor.ActorKind == "monster" ? new Vector3(1.05f, 1.05f, 1.05f) : new Vector3(1.05f, 1.05f, 1.05f);
		return TryAddFirstExisting(actor, paths, "ExternalModel", Vector3.Zero, new Vector3(0.0f, 180.0f, 0.0f), scale, (int)actor.GetInstanceId());
	}

	public static bool TryAddPropModel(Node3D parent, string propKind, int variantSeed, Vector3 position, Vector3 scale)
	{
		string[] paths = propKind == "tree" ? TreeModels : RockModels;
		return TryAddFirstExisting(parent, paths, "ExternalModel", position, Vector3.Zero, scale, variantSeed);
	}

	public static bool TryAddModel(Node3D parent, string path, string nodeName, Vector3 position, Vector3 rotationDegrees, Vector3 scale)
	{
		if (!ResourceLoader.Exists(path))
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
		return true;
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

		if (player.CurrentAnimation != animationName || !player.IsPlaying())
		{
			player.Play(animationName);
		}

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
			model.Scale = scale;
			parent.AddChild(model);
			TryPlayActorAnimation(model, "idle");
			return true;
		}

		return false;
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
}
