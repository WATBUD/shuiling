public static class GameLaunchOptions
{
	public static bool LoadSaveOnWorldReady { get; set; }

	// Chosen on the character-select screen; consumed by PlayerController on a
	// new game. Empty = use defaults.
	public static string NewGamePlayerModelPath { get; set; } = string.Empty;
	public static string NewGamePlayerName { get; set; } = string.Empty;

	public static void StartNewGame()
	{
		LoadSaveOnWorldReady = false;
	}

	public static void LoadSavedGame()
	{
		LoadSaveOnWorldReady = true;
	}
}
