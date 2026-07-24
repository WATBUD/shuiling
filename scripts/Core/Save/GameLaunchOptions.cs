public static class GameLaunchOptions
{
	public static bool LoadSaveOnWorldReady { get; set; }

	// The world slot the running game saves to / loads from.
	public static string ActiveWorldId { get; set; } = string.Empty;

	// World seed used for generation when offline (online uses Net.WorldSeed).
	public static int ActiveSeed { get; set; }

	// Chosen on the character-select screen; consumed by PlayerController on a
	// new game. Empty = use defaults.
	public static string NewGamePlayerModelPath { get; set; } = string.Empty;
	public static string NewGamePlayerName { get; set; } = string.Empty;
	public static string NewWorldName { get; set; } = string.Empty;

	// Chosen on the "new world" mode window BEFORE character select: the created
	// world will be entered single-player or hosted for multiplayer.
	public static bool NewWorldIsMultiplayer { get; set; }

	// Set when leaving character-select via Cancel, so the main menu reopens the
	// world list + new-world mode window (returns to the previous screen).
	public static bool ReturnToNewWorldMode { get; set; }

	// Begin a brand-new world (single-player entry point).
	public static void StartNewWorld(string worldId, string worldName, int seed, string modelPath, string playerName)
	{
		ActiveWorldId = worldId;
		NewWorldName = worldName;
		ActiveSeed = seed;
		NewGamePlayerModelPath = modelPath;
		NewGamePlayerName = playerName;
		LoadSaveOnWorldReady = false;
	}

	// Continue / host an existing world.
	public static void LoadWorld(string worldId, int seed)
	{
		ActiveWorldId = worldId;
		ActiveSeed = seed;
		LoadSaveOnWorldReady = true;
	}

	public static void StartNewGame()
	{
		LoadSaveOnWorldReady = false;
	}

	public static void LoadSavedGame()
	{
		LoadSaveOnWorldReady = true;
	}
}
