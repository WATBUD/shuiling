using Godot;

// 開發者測試用設定，從 res://dev_config.cfg 讀取（正式版預設關閉）。
// 目前僅提供「是否贈送初始測試寵物」旗標；日後可再擴充其他開發旗標。
public static class DevConfig
{
	private const string ConfigPath = "res://dev_config.cfg";
	private static bool _loaded;
	private static bool _testMode;
	private static bool _grantStarterPet;
	private static int _deadTestPets;
	private static bool _grantAllGems;

	// 總開關：只有 test_mode=true 時，下面所有測試旗標才會生效。
	// 檔案不存在（例如正式版沒打包 dev_config.cfg）時預設 false → 全部關閉。
	public static bool TestMode
	{
		get
		{
			EnsureLoaded();
			return _testMode;
		}
	}

	// 為 true 時，新遊戲會贈送初始測試寵物；需 test_mode 開啟才生效。
	public static bool GrantStarterPet
	{
		get
		{
			EnsureLoaded();
			return _testMode && _grantStarterPet;
		}
	}

	// 新遊戲時要贈送幾隻「已死亡」的測試寵物；需 test_mode 開啟才生效。0 = 不贈送。
	public static int DeadTestPets
	{
		get
		{
			EnsureLoaded();
			return _testMode ? _deadTestPets : 0;
		}
	}

	// 為 true 時，新遊戲會一次贈送所有種類的寶石各一顆；需 test_mode 開啟才生效。
	public static bool GrantAllGems
	{
		get
		{
			EnsureLoaded();
			return _testMode && _grantAllGems;
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

		_testMode = (bool)config.GetValue("dev", "test_mode", false);
		_grantStarterPet = (bool)config.GetValue("dev", "grant_starter_pet", false);
		_deadTestPets = config.GetValue("dev", "dead_test_pets", 0).AsInt32();
		_grantAllGems = (bool)config.GetValue("dev", "grant_all_gems", false);
	}
}
