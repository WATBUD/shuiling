using Godot;

// 開發者測試用設定，從 res://dev_config.cfg 讀取（正式版預設關閉）。
// 目前僅提供「是否贈送初始測試寵物」旗標；日後可再擴充其他開發旗標。
public static class DevConfig
{
	private const string ConfigPath = "res://dev_config.cfg";
	private static bool _loaded;
	private static bool _grantStarterPet;
	private static int _deadTestPets;

	// 為 true 時，新遊戲會贈送初始測試寵物；檔案不存在或值為 false 時不贈送。
	public static bool GrantStarterPet
	{
		get
		{
			EnsureLoaded();
			return _grantStarterPet;
		}
	}

	// 新遊戲時要贈送幾隻「已死亡」的測試寵物（用來測水池復活與 U 面板顯示）。0 = 不贈送。
	public static int DeadTestPets
	{
		get
		{
			EnsureLoaded();
			return _deadTestPets;
		}
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}

		_loaded = true;
		var config = new ConfigFile();
		if (config.Load(ConfigPath) != Error.Ok)
		{
			_grantStarterPet = false;
			return;
		}

		_grantStarterPet = (bool)config.GetValue("dev", "grant_starter_pet", false);
		_deadTestPets = config.GetValue("dev", "dead_test_pets", 0).AsInt32();
	}
}
