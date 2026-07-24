using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// Multi-world save storage (Minecraft-style): each world is its own file under
// user://saves/<id>.json. Any world can be played single-player or hosted for
// multiplayer; the file records which mode it was last played in plus the seed
// so the world regenerates identically.
public static class SaveGameManager
{
	public const string SaveDirectory = "user://saves/";
	// Pre-multi-world single save; migrated into a slot on first access.
	private const string LegacySavePath = "user://savegame.json";

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true,
	};

	// Lightweight metadata for the world-list screen.
	public sealed class WorldSaveInfo
	{
		public string Id = string.Empty;
		public string Name = string.Empty;
		public int Seed;
		public string LastMode = "single";
		public string SavedAt = string.Empty;
		public int Level = 1;
		public string PlayerName = string.Empty;
	}

	private static string PathForId(string worldId)
	{
		return SaveDirectory + worldId + ".json";
	}

	public static string NewWorldId()
	{
		return Guid.NewGuid().ToString("N")[..12];
	}

	public static bool HasWorld(string worldId)
	{
		return !string.IsNullOrEmpty(worldId) && Godot.FileAccess.FileExists(PathForId(worldId));
	}

	public static bool HasAnyWorld()
	{
		MigrateLegacyIfNeeded();
		return ListWorlds().Count > 0;
	}

	public static bool TrySave(string worldId, SaveGameData data, out string error)
	{
		error = string.Empty;
		if (string.IsNullOrEmpty(worldId))
		{
			error = "No active world.";
			return false;
		}

		try
		{
			data.WorldId = worldId;
			data.SavedAt = DateTimeOffset.Now.ToString("O");
			string json = JsonSerializer.Serialize(data, SerializerOptions);
			string path = ProjectSettings.GlobalizePath(PathForId(worldId));
			Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
			File.WriteAllText(path, json);
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}

	public static bool TryLoad(string worldId, out SaveGameData data, out string error)
	{
		data = new SaveGameData();
		error = string.Empty;
		try
		{
			string path = ProjectSettings.GlobalizePath(PathForId(worldId));
			if (!File.Exists(path))
			{
				error = "Save file does not exist.";
				return false;
			}

			string json = File.ReadAllText(path);
			SaveGameData? loaded = JsonSerializer.Deserialize<SaveGameData>(json, SerializerOptions);
			if (loaded == null)
			{
				error = "Save file is empty.";
				return false;
			}

			data = loaded;
			return true;
		}
		catch (Exception exception)
		{
			error = exception.Message;
			return false;
		}
	}

	public static void DeleteWorld(string worldId)
	{
		try
		{
			string path = ProjectSettings.GlobalizePath(PathForId(worldId));
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Failed to delete world {worldId}: {exception.Message}");
		}
	}

	// All worlds on disk, newest-played first.
	public static List<WorldSaveInfo> ListWorlds()
	{
		MigrateLegacyIfNeeded();
		var worlds = new List<WorldSaveInfo>();
		string dir = ProjectSettings.GlobalizePath(SaveDirectory);
		if (!Directory.Exists(dir))
		{
			return worlds;
		}

		foreach (string file in Directory.GetFiles(dir, "*.json"))
		{
			try
			{
				SaveGameData? data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(file), SerializerOptions);
				if (data == null)
				{
					continue;
				}

				string id = string.IsNullOrEmpty(data.WorldId) ? Path.GetFileNameWithoutExtension(file) : data.WorldId;
				worlds.Add(new WorldSaveInfo
				{
					Id = id,
					Name = string.IsNullOrWhiteSpace(data.WorldName) ? id : data.WorldName,
					Seed = data.WorldSeed,
					LastMode = string.IsNullOrEmpty(data.LastMode) ? "single" : data.LastMode,
					SavedAt = data.SavedAt,
					Level = data.Player?.Level ?? 1,
					PlayerName = data.Player?.PlayerName ?? string.Empty,
				});
			}
			catch (Exception exception)
			{
				GD.PushWarning($"Skipping unreadable save {file}: {exception.Message}");
			}
		}

		worlds.Sort((a, b) => string.CompareOrdinal(b.SavedAt, a.SavedAt));
		return worlds;
	}

	// One-time import of the old single savegame.json into a slot.
	private static void MigrateLegacyIfNeeded()
	{
		try
		{
			string legacy = ProjectSettings.GlobalizePath(LegacySavePath);
			if (!File.Exists(legacy))
			{
				return;
			}

			SaveGameData? data = JsonSerializer.Deserialize<SaveGameData>(File.ReadAllText(legacy), SerializerOptions);
			if (data != null)
			{
				string id = NewWorldId();
				data.WorldId = id;
				if (string.IsNullOrWhiteSpace(data.WorldName))
				{
					data.WorldName = LocaleText.T("world.migrated_name");
				}

				string path = ProjectSettings.GlobalizePath(PathForId(id));
				Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
				File.WriteAllText(path, JsonSerializer.Serialize(data, SerializerOptions));
			}

			File.Delete(legacy);
		}
		catch (Exception exception)
		{
			GD.PushWarning($"Legacy save migration failed: {exception.Message}");
		}
	}
}
