using Godot;

// 開發者測試用設定，從 res://dev_config.cfg 讀取（正式版預設關閉）。
// 目前僅提供「是否贈送初始測試寵物」旗標；日後可再擴充其他開發旗標。
public static class DevConfig
{
	private const string ConfigPath = "res://dev_config.cfg";
	private static bool _loaded;
	private static bool _grantStarterPet;

	// 為 true 時，新遊戲會贈送初始測試寵物；檔案不存在或值為 false 時不贈送。
	public static bool GrantStarterPet
	{
		get
		{
			EnsureLoaded();
			return _grantStarterPet;
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
	}
}
