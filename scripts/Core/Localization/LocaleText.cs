using Godot;
using System.Collections.Generic;
using System.Text.Json;

public static class LocaleText
{
	public const string ZhTw = "zh_TW";
	public const string En = "en";
	private const string LocaleDirectory = "res://scripts/Core/Localization/locales";

	public static readonly string[] LanguageCodes = { ZhTw, En };

	private static readonly Dictionary<string, Dictionary<string, string>> Translations = new();
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	private static string _language = ZhTw;
	private static bool _loaded;

	public static event System.Action? LanguageChanged;

	public static string CurrentLanguage => _language;

	static LocaleText()
	{
		EnsureLoaded();
	}

	public static void SetLanguage(string language)
	{
		EnsureLoaded();
		if (!Translations.ContainsKey(language))
		{
			language = ZhTw;
		}

		if (_language == language)
		{
			return;
		}

		_language = language;
		LanguageChanged?.Invoke();
	}

	public static string T(string key)
	{
		EnsureLoaded();
		if (Translations.TryGetValue(_language, out Dictionary<string, string>? languageTable)
			&& languageTable.TryGetValue(key, out string? localized))
		{
			return localized;
		}

		if (Translations.TryGetValue(ZhTw, out Dictionary<string, string>? fallbackTable)
			&& fallbackTable.TryGetValue(key, out string? fallback))
		{
			return fallback;
		}

		return key;
	}

	public static string F(string key, params object[] args)
	{
		return string.Format(T(key), args);
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		Translations.Clear();
		foreach (string languageCode in LanguageCodes)
		{
			Translations[languageCode] = LoadLanguage(languageCode);
		}

		_loaded = true;
	}

	private static Dictionary<string, string> LoadLanguage(string languageCode)
	{
		string path = $"{LocaleDirectory}/{languageCode}.json";
		if (!FileAccess.FileExists(path))
		{
			GD.PushWarning($"Locale file not found: {path}");
			return new Dictionary<string, string>();
		}

		try
		{
			string json = FileAccess.GetFileAsString(path);
			Dictionary<string, string>? loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);
			return loaded ?? new Dictionary<string, string>();
		}
		catch (System.Exception exception)
		{
			GD.PushError($"Failed to load locale file {path}: {exception.Message}");
			return new Dictionary<string, string>();
		}
	}
}
