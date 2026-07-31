using Godot;

public static partial class ExternalModelLibrary
{
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

	private static void EnsureKayKitSkeletonAnimations(Node3D model, string sourcePath)
	{
		if (!sourcePath.Contains(KayKitSkeletonModelFolder, System.StringComparison.OrdinalIgnoreCase)
			|| sourcePath.Contains("/animations/", System.StringComparison.OrdinalIgnoreCase)
			|| FindAnimationPlayer(model) != null)
		{
			return;
		}

		AnimationLibrary? animations = GetKayKitSkeletonAnimationLibrary();
		if (animations == null)
		{
			return;
		}

		var player = new AnimationPlayer { Name = "KayKitAnimationPlayer" };
		model.AddChild(player);
		player.AddAnimationLibrary("", animations);
	}

	private static AnimationLibrary? GetKayKitSkeletonAnimationLibrary()
	{
		if (_kayKitSkeletonAnimations != null)
		{
			return _kayKitSkeletonAnimations;
		}

		var merged = new AnimationLibrary();
		foreach (string scenePath in KayKitSkeletonAnimationScenes)
		{
			if (!ResourceLoader.Exists(scenePath)
				|| ResourceLoader.Load<PackedScene>(scenePath) is not PackedScene packedScene
				|| packedScene.Instantiate() is not Node animationRoot)
			{
				continue;
			}

			AnimationPlayer? sourcePlayer = FindAnimationPlayer(animationRoot);
			if (sourcePlayer != null)
			{
				foreach (StringName animationName in sourcePlayer.GetAnimationList())
				{
					if (animationName == "RESET" || merged.HasAnimation(animationName))
					{
						continue;
					}

					Animation? animation = sourcePlayer.GetAnimation(animationName);
					if (animation != null)
					{
						merged.AddAnimation(animationName, animation);
					}
				}
			}

			animationRoot.Free();
		}

		if (merged.GetAnimationList().Count == 0)
		{
			return null;
		}

		_kayKitSkeletonAnimations = merged;
		return _kayKitSkeletonAnimations;
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
}
