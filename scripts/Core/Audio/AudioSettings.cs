using Godot;

// Global audio settings (music + sound-effect volume), shared by the main menu
// and the in-game settings panel. Volumes drive dedicated "Music"/"SFX" audio
// buses and persist to user://audio.cfg across sessions.
public static class AudioSettings
{
	public const string MusicBus = "Music";
	public const string SfxBus = "SFX";
	private const string ConfigPath = "user://audio.cfg";

	public static float MusicVolume { get; private set; } = 0.8f;
	public static float SfxVolume { get; private set; } = 0.8f;

	private static bool _initialized;

	public static void Initialize()
	{
		if (_initialized)
		{
			return;
		}

		_initialized = true;
		EnsureBus(MusicBus);
		EnsureBus(SfxBus);
		Load();
		Apply();
	}

	public static void SetMusicVolume(float value)
	{
		MusicVolume = Mathf.Clamp(value, 0.0f, 1.0f);
		Apply();
		Save();
	}

	public static void SetSfxVolume(float value)
	{
		SfxVolume = Mathf.Clamp(value, 0.0f, 1.0f);
		Apply();
		Save();
	}

	private static void EnsureBus(string name)
	{
		if (AudioServer.GetBusIndex(name) >= 0)
		{
			return;
		}

		int index = AudioServer.BusCount;
		AudioServer.AddBus(index);
		AudioServer.SetBusName(index, name);
		AudioServer.SetBusSend(index, "Master");
	}

	private static void Apply()
	{
		ApplyBus(MusicBus, MusicVolume);
		ApplyBus(SfxBus, SfxVolume);
	}

	private static void ApplyBus(string name, float linear)
	{
		int index = AudioServer.GetBusIndex(name);
		if (index < 0)
		{
			return;
		}

		AudioServer.SetBusMute(index, linear <= 0.001f);
		AudioServer.SetBusVolumeDb(index, Mathf.LinearToDb(Mathf.Clamp(linear, 0.0001f, 1.0f)));
	}

	private static void Load()
	{
		var config = new ConfigFile();
		if (config.Load(ConfigPath) != Error.Ok)
		{
			return;
		}

		MusicVolume = Mathf.Clamp((float)config.GetValue("audio", "music", 0.8f).AsSingle(), 0.0f, 1.0f);
		SfxVolume = Mathf.Clamp((float)config.GetValue("audio", "sfx", 0.8f).AsSingle(), 0.0f, 1.0f);
	}

	private static void Save()
	{
		var config = new ConfigFile();
		config.SetValue("audio", "music", MusicVolume);
		config.SetValue("audio", "sfx", SfxVolume);
		config.Save(ConfigPath);
	}
}
