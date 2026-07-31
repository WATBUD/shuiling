using Godot;
using System.Collections.Generic;

public static partial class ExternalModelLibrary
{
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
}
