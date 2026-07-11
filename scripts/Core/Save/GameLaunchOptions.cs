public static class GameLaunchOptions
{
	public static bool LoadSaveOnWorldReady { get; set; }

	public static void StartNewGame()
	{
		LoadSaveOnWorldReady = false;
	}

	public static void LoadSavedGame()
	{
		LoadSaveOnWorldReady = true;
	}
}
