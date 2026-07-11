using Godot;
using System;
using System.IO;
using System.Text.Json;

public static class SaveGameManager
{
	public const string SavePath = "user://savegame.json";

	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true,
	};

	public static bool HasSave()
	{
		return Godot.FileAccess.FileExists(SavePath);
	}

	public static string GetSavePath()
	{
		return ProjectSettings.GlobalizePath(SavePath);
	}

	public static bool TrySave(SaveGameData data, out string error)
	{
		error = string.Empty;
		try
		{
			data.SavedAt = DateTimeOffset.Now.ToString("O");
			string json = JsonSerializer.Serialize(data, SerializerOptions);
			string path = GetSavePath();
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

	public static bool TryLoad(out SaveGameData data, out string error)
	{
		data = new SaveGameData();
		error = string.Empty;
		try
		{
			string path = GetSavePath();
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
}
