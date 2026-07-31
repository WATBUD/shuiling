using Godot;
using System.Collections.Generic;

public static partial class ExternalModelLibrary
{
	public const string KenneyBlockyRoot = "res://assets/models/characters/kenney_blocky/";
	private static readonly Dictionary<ulong, AnimationPlayer?> AnimationPlayerCache = new();
	private static readonly Dictionary<ulong, Node3D?> RootMotionNodeCache = new();
	private static AnimationLibrary? _kayKitSkeletonAnimations;
	private const string KayKitSkeletonModelFolder = "/kaykit_skeletons/";
	private static readonly string[] KayKitSkeletonAnimationScenes =
	{
		"res://assets/models/monsters/kaykit_skeletons/animations/Rig_Medium_General.glb",
		"res://assets/models/monsters/kaykit_skeletons/animations/Rig_Medium_MovementBasic.glb",
	};

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
		(KenneyBlockyRoot + "character-m.glb", "character.archer"),
		("res://assets/models/characters/knight.glb", "character.knight"),
		("res://assets/models/characters/barbarian.glb", "character.barbarian"),
		("res://assets/models/characters/mage.glb", "character.mage"),
		("res://assets/models/characters/rogue.glb", "character.rogue"),
		("res://assets/models/characters/guard.gltf", "character.guard"),
		(KenneyBlockyRoot + "character-a.glb", "character.blocky.craftsman"),
		(KenneyBlockyRoot + "character-b.glb", "character.blocky.adventurer"),
		(KenneyBlockyRoot + "character-c.glb", "character.blocky.elder"),
		(KenneyBlockyRoot + "character-d.glb", "character.blocky.android"),
		(KenneyBlockyRoot + "character-e.glb", "character.blocky.scholar"),
		(KenneyBlockyRoot + "character-f.glb", "character.blocky.villager"),
		(KenneyBlockyRoot + "character-g.glb", "character.blocky.crimson_knight"),
		(KenneyBlockyRoot + "character-h.glb", "character.blocky.arcane_knight"),
		(KenneyBlockyRoot + "character-i.glb", "character.blocky.alchemist"),
		(KenneyBlockyRoot + "character-j.glb", "character.blocky.guard"),
		(KenneyBlockyRoot + "character-k.glb", "character.blocky.traveler"),
		(KenneyBlockyRoot + "character-l.glb", "character.blocky.goblin"),
		(KenneyBlockyRoot + "character-n.glb", "character.blocky.mystic"),
		(KenneyBlockyRoot + "character-o.glb", "character.blocky.orc"),
		(KenneyBlockyRoot + "character-p.glb", "character.blocky.merchant"),
		(KenneyBlockyRoot + "character-q.glb", "character.blocky.gentleman"),
		(KenneyBlockyRoot + "character-r.glb", "character.blocky.ninja"),
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
		foreach (MonsterSpeciesDefinition species in MonsterSpeciesCatalog.Current.All)
		{
			foreach (string path in species.ModelPaths)
			{
				TryAdd(path, MonsterModelDisplay(path));
			}
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
		["skeleton warrior"] = "character.mob.skeleton_warrior",
		["rogue skeleton"] = "character.mob.skeleton_rogue",
		["mage skeleton"] = "character.mob.skeleton_mage",
		["minion skeleton"] = "character.mob.skeleton_minion",
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

	private static void EnsureCardModelRegistry()
	{
		if (_cardKeyToModelPath != null)
		{
			return;
		}

		var registry = new Dictionary<string, string>();
		_cardKeyToModelPath = registry;

		void Register(string path)
		{
			if (string.IsNullOrWhiteSpace(path)
				|| !ResourceLoader.Exists(path)
				|| HasInvalidImportRemap(path))
			{
				return;
			}

			string key = CanonicalModelKey(path);
			// Every card must have one explicit monster name. This prevents
			// player/NPC models and untranslated filenames entering the album.
			if (string.IsNullOrWhiteSpace(key)
				|| !MonsterModelNameKeys.ContainsKey(key)
				|| registry.ContainsKey(key))
			{
				return;
			}

			registry[key] = path;
		}

		foreach (string path in MonsterMelee)
		{
			Register(path);
		}
		foreach (string path in MonsterRanged)
		{
			Register(path);
		}
		foreach (MonsterSpeciesDefinition species in MonsterSpeciesCatalog.Current.All)
		{
			foreach (string path in species.ModelPaths)
			{
				Register(path);
			}
		}
		foreach (string path in ListModelFiles("res://assets/models/pets/cube_pets/"))
		{
			Register(path);
		}
	}

	// Reverse lookup: canonical card key → a concrete model path, so the album can
	// instantiate a 3D preview from a stored card key. Cached after first build.
	public static string GetModelPathForCardKey(string cardKey)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return string.Empty;
		}

		EnsureCardModelRegistry();

		return _cardKeyToModelPath!.TryGetValue(cardKey, out string? modelPath) ? modelPath : string.Empty;
	}

	public static bool IsValidCardKey(string cardKey)
	{
		if (string.IsNullOrWhiteSpace(cardKey))
		{
			return false;
		}

		EnsureCardModelRegistry();
		return _cardKeyToModelPath!.ContainsKey(cardKey);
	}

	// Fixed, stable list of the named monster-card keys (used to assign each NPC a
	// specific card it will accept for its quest). Ordered for determinism.
	public static IReadOnlyList<string> KnownCardKeys
	{
		get
		{
			EnsureCardModelRegistry();
			var keys = new List<string>(_cardKeyToModelPath!.Keys);
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
	// Preview model instantiation moved to ExternalModelLibrary.ModelResolution.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	private static readonly string[] NpcMelee =
	{
		"res://assets/models/characters/knight.glb",
		"res://assets/models/characters/barbarian.glb",
		"res://assets/models/characters/rogue.glb",
	};

	private static readonly string[] NpcRanged =
	{
		KenneyBlockyRoot + "character-m.glb",
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

	// Actor and player model resolution moved to ExternalModelLibrary.ModelResolution.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Prop and static model placement moved to ExternalModelLibrary.Placement.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Import remap validation moved to ExternalModelLibrary.ImportRemap.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Actor animation playback moved to ExternalModelLibrary.Animation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// First-existing model placement moved to ExternalModelLibrary.Placement.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// KayKit skeleton animation setup moved to ExternalModelLibrary.Animation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Blocked actor path check moved to ExternalModelLibrary.Placement.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Fallback materials moved to ExternalModelLibrary.FallbackMaterials.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// Animation player and root-motion lookup moved to ExternalModelLibrary.Animation.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// PositiveModulo moved to ExternalModelLibrary.Placement.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).

	// StableStringHash moved to ExternalModelLibrary.ModelResolution.cs (Stage-0 — docs/ARCHITECTURE_REVIEW.md).
}
