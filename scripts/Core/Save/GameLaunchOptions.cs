public static class GameLaunchOptions
{
	public const int MaxWorldNameLength = 20;

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

	// Chosen on the "new world" character screen: auto-save when leaving the world.
	public static bool NewWorldAutoSave { get; set; } = true;

	// Set when leaving character-select via Cancel, so the main menu reopens the
	// world list + new-world mode window (returns to the previous screen).
	public static bool ReturnToNewWorldMode { get; set; }

	// True while character-select is being shown for a player JOINING someone
	// else's server (a transient guest), rather than creating a new world. In
	// this mode character-select hides world-name/auto-save and does not create
	// or reset a session — the connection is already established.
	public static bool IsJoiningGuest { get; set; }

	// Enter a host's world as a transient guest with the chosen character. No
	// local world slot is created (the guest keeps no save).
	public static void StartJoinGuest(string modelPath, string playerName)
	{
		ActiveWorldId = string.Empty;
		NewWorldName = string.Empty;
		ActiveSeed = 0;
		NewGamePlayerModelPath = modelPath;
		NewGamePlayerName = playerName;
		LoadSaveOnWorldReady = false;
	}


	// Begin a brand-new world (single-player entry point).
	public static void StartNewWorld(string worldId, string worldName, int seed, string modelPath, string playerName)
	{
		ActiveWorldId = worldId;
		string normalizedWorldName = worldName.Trim();
		NewWorldName = normalizedWorldName.Length > MaxWorldNameLength
			? normalizedWorldName[..MaxWorldNameLength]
			: normalizedWorldName;
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
