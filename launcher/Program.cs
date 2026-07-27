using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

// 水靈 遊戲更新器（Launcher）
// -----------------------------------------------------------------------------
// 放在遊戲資料夾外層，朋友執行「這個」而不是遊戲本體。開機流程：
//   1. 讀取 launcher.cfg 取得 GitHub owner/repo 與遊戲執行檔名。
//   2. 抓 GitHub Releases 最新版的 version.json，比對本機已安裝版本。
//   3. 有新版 → 下載 game.zip、解壓到暫存、覆蓋 app/ 資料夾、寫入版本號。
//   4. 啟動 app/ 內的遊戲主程式後結束。
// GitHub 的 /releases/latest/download/<asset> 會永遠指向最新發佈，故不需 API token。
internal static class Program
{
	private sealed class LauncherConfig
	{
		public string Owner = "YOUR_GITHUB_ACCOUNT";
		public string Repo = "shuiling";
		public string GameExe = "shuiling.exe";
		public string AppDir = "app";
	}

	private static async Task<int> Main()
	{
		Console.Title = "水靈 更新器";
		string baseDir = AppContext.BaseDirectory;
		LauncherConfig cfg = LoadConfig(Path.Combine(baseDir, "launcher.cfg"));
		string appDir = Path.Combine(baseDir, cfg.AppDir);
		string gamePath = Path.Combine(appDir, cfg.GameExe);
		string versionUrl = $"https://github.com/{cfg.Owner}/{cfg.Repo}/releases/latest/download/version.json";
		string zipUrl = $"https://github.com/{cfg.Owner}/{cfg.Repo}/releases/latest/download/game.zip";

		try
		{
			string localVersion = ReadLocalVersion(appDir);
			Log($"目前版本：{(string.IsNullOrEmpty(localVersion) ? "(未安裝)" : localVersion)}");
			Log("檢查更新中…");

			string? remoteVersion = await TryGetRemoteVersionAsync(versionUrl);
			if (remoteVersion == null)
			{
				Log("無法連線到更新伺服器。");
				if (File.Exists(gamePath))
				{
					Log("改用目前已安裝的版本啟動。");
					LaunchGame(gamePath, appDir);
					return 0;
				}

				return Fail("尚未安裝遊戲且無法連線，請確認網路後重試。");
			}

			if (remoteVersion != localVersion)
			{
				Log($"發現新版本：{remoteVersion}，開始更新…");
				await DownloadAndInstallAsync(zipUrl, appDir, remoteVersion, baseDir);
				Log("更新完成！");
			}
			else
			{
				Log("已是最新版本。");
			}

			if (!File.Exists(gamePath))
			{
				return Fail($"找不到遊戲主程式：{gamePath}（請確認 launcher.cfg 的 GameExe 設定）。");
			}

			LaunchGame(gamePath, appDir);
			return 0;
		}
		catch (Exception ex)
		{
			return Fail("更新發生錯誤：" + ex.Message);
		}
	}

	private static LauncherConfig LoadConfig(string path)
	{
		var cfg = new LauncherConfig();
		if (!File.Exists(path))
		{
			return cfg;
		}

		foreach (string raw in File.ReadAllLines(path))
		{
			string line = raw.Trim();
			if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";") || !line.Contains('='))
			{
				continue;
			}

			int eq = line.IndexOf('=');
			string key = line.Substring(0, eq).Trim().ToLowerInvariant();
			string value = line.Substring(eq + 1).Trim();
			switch (key)
			{
				case "owner": cfg.Owner = value; break;
				case "repo": cfg.Repo = value; break;
				case "gameexe": cfg.GameExe = value; break;
				case "appdir": cfg.AppDir = value; break;
			}
		}

		return cfg;
	}

	private static string ReadLocalVersion(string appDir)
	{
		string versionFile = Path.Combine(appDir, "installed_version.txt");
		return File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : string.Empty;
	}

	private static async Task<string?> TryGetRemoteVersionAsync(string versionUrl)
	{
		try
		{
			using var http = CreateHttpClient();
			string json = await http.GetStringAsync(versionUrl);
			using JsonDocument doc = JsonDocument.Parse(json);
			return doc.RootElement.TryGetProperty("version", out JsonElement v) ? v.GetString()?.Trim() : null;
		}
		catch
		{
			return null;
		}
	}

	private static async Task DownloadAndInstallAsync(string zipUrl, string appDir, string remoteVersion, string baseDir)
	{
		string tempZip = Path.Combine(baseDir, "update_download.zip");
		string stageDir = Path.Combine(baseDir, "update_stage");

		Log("下載更新包…");
		using (var http = CreateHttpClient())
		using (HttpResponseMessage resp = await http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead))
		{
			resp.EnsureSuccessStatusCode();
			await using FileStream fs = File.Create(tempZip);
			await resp.Content.CopyToAsync(fs);
		}

		Log("解壓縮…");
		if (Directory.Exists(stageDir))
		{
			Directory.Delete(stageDir, true);
		}

		ZipFile.ExtractToDirectory(tempZip, stageDir);

		// 若 zip 內含單一頂層資料夾，將其視為根（避免多一層）。
		string sourceRoot = ResolveExtractedRoot(stageDir);

		Log("套用更新…");
		if (Directory.Exists(appDir))
		{
			Directory.Delete(appDir, true);
		}

		Directory.Move(sourceRoot, appDir);
		File.WriteAllText(Path.Combine(appDir, "installed_version.txt"), remoteVersion);

		// 清理暫存。
		TryDelete(tempZip);
		if (Directory.Exists(stageDir))
		{
			try { Directory.Delete(stageDir, true); } catch { /* ignore */ }
		}
	}

	private static string ResolveExtractedRoot(string stageDir)
	{
		string[] dirs = Directory.GetDirectories(stageDir);
		string[] files = Directory.GetFiles(stageDir);
		return dirs.Length == 1 && files.Length == 0 ? dirs[0] : stageDir;
	}

	private static void LaunchGame(string gamePath, string appDir)
	{
		Log("啟動遊戲…");
		var psi = new ProcessStartInfo
		{
			FileName = gamePath,
			WorkingDirectory = appDir,
			UseShellExecute = true,
		};
		Process.Start(psi);
	}

	private static HttpClient CreateHttpClient()
	{
		var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
		// GitHub 需要 User-Agent，否則部分請求會被拒。
		http.DefaultRequestHeaders.UserAgent.ParseAdd("shuiling-launcher/1.0");
		return http;
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
	}

	private static void Log(string message)
	{
		Console.WriteLine($"[更新器] {message}");
	}

	private static int Fail(string message)
	{
		Console.WriteLine();
		Console.WriteLine("[錯誤] " + message);
		Console.WriteLine("按 Enter 關閉…");
		Console.ReadLine();
		return 1;
	}
}
