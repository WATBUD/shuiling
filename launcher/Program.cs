using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// 水靈 Windows 自動更新器
// 下載永遠先進暫存區，驗證完成後才替換 app；安裝失敗會還原上一版。
internal static class Program
{
	private const string ManifestAsset = "version.json";
	private const string PackageAsset = "game.zip";
	private const string LauncherVersion = "2.1.1";
	private const uint DetachedProcess = 0x00000008;
	private const uint CreateNewProcessGroup = 0x00000200;
	private static readonly HttpClient Http = CreateHttpClient();

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct StartupInfo
	{
		public int Size;
		public string? Reserved;
		public string? Desktop;
		public string? Title;
		public int X;
		public int Y;
		public int XSize;
		public int YSize;
		public int XCountChars;
		public int YCountChars;
		public int FillAttribute;
		public int Flags;
		public short ShowWindow;
		public short Reserved2;
		public IntPtr Reserved2Pointer;
		public IntPtr StandardInput;
		public IntPtr StandardOutput;
		public IntPtr StandardError;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct ProcessInformation
	{
		public IntPtr Process;
		public IntPtr Thread;
		public int ProcessId;
		public int ThreadId;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CreateProcess(
		string applicationName,
		StringBuilder commandLine,
		IntPtr processAttributes,
		IntPtr threadAttributes,
		[MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
		uint creationFlags,
		IntPtr environment,
		string currentDirectory,
		ref StartupInfo startupInfo,
		out ProcessInformation processInformation);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool CloseHandle(IntPtr handle);

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool FreeConsole();

	private sealed class LauncherConfig
	{
		public string Owner = "WATBUD";
		public string Repo = "shuiling";
		public string GameExe = "shuiling.exe";
		public string AppDir = "app";
		public string BaseUrl = string.Empty;
	}

	private sealed record UpdateManifest(string Version, string Sha256, long Size);

	private static async Task<int> Main()
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		Console.Title = $"水靈更新器 v{LauncherVersion}";
		Log($"Launcher v{LauncherVersion}");

		using var singleInstance = new Mutex(true, "ShuilingLauncher.UpdateLock", out bool ownsMutex);
		if (!ownsMutex)
		{
			return Fail("更新器已經在執行中。");
		}

		string baseDir = AppContext.BaseDirectory;
		LauncherConfig cfg = LoadConfig(Path.Combine(baseDir, "launcher.cfg"));
		if (!IsSafeRelativePath(cfg.AppDir) || !IsSafeRelativePath(cfg.GameExe))
		{
			return Fail("launcher.cfg 的 appDir 或 gameExe 不可使用絕對路徑或 '..'。");
		}

		string appDir = Path.GetFullPath(Path.Combine(baseDir, cfg.AppDir));
		string gamePath = Path.GetFullPath(Path.Combine(appDir, cfg.GameExe));
		string releaseBase = cfg.BaseUrl.Length > 0
			? cfg.BaseUrl.TrimEnd('/')
			: $"https://github.com/{Uri.EscapeDataString(cfg.Owner)}/{Uri.EscapeDataString(cfg.Repo)}/releases/latest/download";

		try
		{
			CleanupInterruptedUpdate(baseDir, appDir);
			string localVersion = ReadLocalVersion(appDir);
			Log($"目前版本：{(localVersion.Length == 0 ? "尚未安裝" : localVersion)}");
			Log("正在檢查更新…");

			UpdateManifest? remote = await TryGetManifestAsync($"{releaseBase}/{ManifestAsset}");
			if (remote == null)
			{
				return await LaunchInstalledOrFailAsync(gamePath, appDir, "暫時無法連線到更新伺服器");
			}

			bool needsInstall = !File.Exists(gamePath)
				|| !string.Equals(remote.Version, localVersion, StringComparison.OrdinalIgnoreCase);
			if (needsInstall)
			{
				Log($"發現版本 {remote.Version}，準備更新。");
				await DownloadAndInstallAsync($"{releaseBase}/{PackageAsset}", appDir, cfg.GameExe, remote, baseDir);
				Log("更新完成。");
			}
			else
			{
				Log("目前已是最新版本。");
			}

			if (!File.Exists(gamePath))
			{
				return Fail($"更新完成但找不到遊戲：{cfg.AppDir}\\{cfg.GameExe}");
			}

			await LaunchGameAndCloseAsync(gamePath, appDir);
			return 0;
		}
		catch (Exception ex)
		{
			return await LaunchInstalledOrFailAsync(gamePath, appDir, $"更新失敗：{ex.Message}");
		}
	}

	private static async Task<UpdateManifest?> TryGetManifestAsync(string url)
	{
		try
		{
			using HttpResponseMessage response = await Http.GetAsync(url);
			response.EnsureSuccessStatusCode();
			string json = (await response.Content.ReadAsStringAsync()).TrimStart('\uFEFF');
			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;
			string version = root.TryGetProperty("version", out JsonElement versionNode)
				? versionNode.GetString()?.Trim() ?? string.Empty
				: string.Empty;
			string sha256 = root.TryGetProperty("sha256", out JsonElement hashNode)
				? hashNode.GetString()?.Trim().ToLowerInvariant() ?? string.Empty
				: string.Empty;
			long size = root.TryGetProperty("size", out JsonElement sizeNode) && sizeNode.TryGetInt64(out long parsedSize)
				? parsedSize
				: 0;
			return version.Length == 0 ? null : new UpdateManifest(version, sha256, size);
		}
		catch
		{
			return null;
		}
	}

	private static async Task DownloadAndInstallAsync(
		string packageUrl,
		string appDir,
		string gameExe,
		UpdateManifest manifest,
		string baseDir)
	{
		string updateRoot = Path.Combine(baseDir, ".update");
		string packagePath = Path.Combine(updateRoot, PackageAsset + ".download");
		string stageDir = Path.Combine(updateRoot, "stage");
		string backupDir = Path.Combine(baseDir, ".app_backup");

		DeleteDirectoryRequired(stageDir);
		Directory.CreateDirectory(updateRoot);
		Log("下載更新包…");
		await DownloadFileAsync(packageUrl, packagePath, manifest.Size);

		if (manifest.Size > 0 && new FileInfo(packagePath).Length != manifest.Size)
		{
			throw new InvalidDataException("下載大小與發布資訊不符。");
		}

		if (manifest.Sha256.Length > 0)
		{
			Log("驗證更新檔…");
			string actualHash = await ComputeSha256Async(packagePath);
			if (!string.Equals(actualHash, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("SHA-256 驗證失敗，更新包可能不完整。");
			}
		}

		Log("解壓縮更新…");
		Directory.CreateDirectory(stageDir);
		ExtractZipSafely(packagePath, stageDir);
		string sourceRoot = ResolveExtractedRoot(stageDir);
		if (!File.Exists(Path.Combine(sourceRoot, gameExe)))
		{
			throw new InvalidDataException($"更新包內找不到 {gameExe}。");
		}

		File.WriteAllText(Path.Combine(sourceRoot, "installed_version.txt"), manifest.Version);
		Log("套用更新…");
		DeleteDirectoryRequired(backupDir);
		bool oldVersionMoved = false;
		try
		{
			if (Directory.Exists(appDir))
			{
				Directory.Move(appDir, backupDir);
				oldVersionMoved = true;
			}

			Directory.Move(sourceRoot, appDir);
			DeleteDirectoryIfPresent(backupDir);
		}
		catch
		{
			if (oldVersionMoved)
			{
				DeleteDirectoryRequired(appDir);
				if (Directory.Exists(backupDir))
				{
					Directory.Move(backupDir, appDir);
				}
			}

			throw;
		}
		finally
		{
			TryDelete(packagePath);
			DeleteDirectoryIfPresent(updateRoot);
		}
	}

	private static async Task DownloadFileAsync(string url, string destination, long expectedSize)
	{
		using HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		long total = expectedSize > 0 ? expectedSize : response.Content.Headers.ContentLength ?? 0;
		await using Stream input = await response.Content.ReadAsStreamAsync();
		await using FileStream output = new(destination, FileMode.Create, FileAccess.Write, FileShare.None);
		byte[] buffer = new byte[128 * 1024];
		long received = 0;
		int lastPercent = -1;
		while (true)
		{
			int read = await input.ReadAsync(buffer);
			if (read == 0)
			{
				break;
			}

			await output.WriteAsync(buffer.AsMemory(0, read));
			received += read;
			if (total > 0)
			{
				int percent = (int)Math.Min(100, received * 100 / total);
				if (percent >= lastPercent + 5 || percent == 100)
				{
					Console.Write($"\r[更新器] 下載進度：{percent,3}%");
					lastPercent = percent;
				}
			}
		}

		if (total > 0)
		{
			Console.WriteLine();
		}
	}

	private static void ExtractZipSafely(string zipPath, string destination)
	{
		string destinationRoot = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
		using ZipArchive archive = ZipFile.OpenRead(zipPath);
		foreach (ZipArchiveEntry entry in archive.Entries)
		{
			string target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
			if (!target.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException("更新包包含不安全的路徑。");
			}

			if (entry.FullName.EndsWith("/", StringComparison.Ordinal)
				|| entry.FullName.EndsWith("\\", StringComparison.Ordinal))
			{
				Directory.CreateDirectory(target);
				continue;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			entry.ExtractToFile(target, true);
		}
	}

	private static async Task<string> ComputeSha256Async(string path)
	{
		await using FileStream stream = File.OpenRead(path);
		byte[] hash = await SHA256.HashDataAsync(stream);
		return Convert.ToHexString(hash).ToLowerInvariant();
	}

	private static void CleanupInterruptedUpdate(string baseDir, string appDir)
	{
		string backupDir = Path.Combine(baseDir, ".app_backup");
		if (!Directory.Exists(appDir) && Directory.Exists(backupDir))
		{
			Directory.Move(backupDir, appDir);
		}
		else
		{
			DeleteDirectoryIfPresent(backupDir);
		}

		DeleteDirectoryIfPresent(Path.Combine(baseDir, ".update"));
	}

	private static async Task<int> LaunchInstalledOrFailAsync(string gamePath, string appDir, string reason)
	{
		Log(reason + "。");
		if (!File.Exists(gamePath))
		{
			return Fail("本機尚未安裝可離線啟動的遊戲，請確認網路後重試。");
		}

		Log("將啟動目前已安裝的版本。");
		await LaunchGameAndCloseAsync(gamePath, appDir);
		return 0;
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
			if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
			{
				continue;
			}

			int equals = line.IndexOf('=');
			if (equals <= 0)
			{
				continue;
			}

			string key = line[..equals].Trim().ToLowerInvariant();
			string value = line[(equals + 1)..].Trim();
			if (value.Length == 0)
			{
				continue;
			}

			switch (key)
			{
				case "owner": cfg.Owner = value; break;
				case "repo": cfg.Repo = value; break;
				case "gameexe": cfg.GameExe = value; break;
				case "appdir": cfg.AppDir = value; break;
				case "baseurl": cfg.BaseUrl = value; break;
			}
		}

		return cfg;
	}

	private static bool IsSafeRelativePath(string path)
	{
		return path.Length > 0
			&& !Path.IsPathRooted(path)
			&& !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains("..");
	}

	private static string ReadLocalVersion(string appDir)
	{
		string path = Path.Combine(appDir, "installed_version.txt");
		return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
	}

	private static string ResolveExtractedRoot(string stageDir)
	{
		string[] directories = Directory.GetDirectories(stageDir);
		string[] files = Directory.GetFiles(stageDir);
		return directories.Length == 1 && files.Length == 0 ? directories[0] : stageDir;
	}

	private static async Task LaunchGameAndCloseAsync(string gamePath, string appDir)
	{
		Log("完成，將於 3 秒後啟動遊戲並自動關閉更新器。");
		await Task.Delay(TimeSpan.FromSeconds(3));
		Console.Out.Flush();
		if (OperatingSystem.IsWindows())
		{
			FreeConsole();
		}

		if (OperatingSystem.IsWindows())
		{
			var startupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
			var commandLine = new StringBuilder($"\"{gamePath}\" --quiet");
			bool started = CreateProcess(
				gamePath,
				commandLine,
				IntPtr.Zero,
				IntPtr.Zero,
				false,
				DetachedProcess | CreateNewProcessGroup,
				IntPtr.Zero,
				appDir,
				ref startupInfo,
				out ProcessInformation processInformation);
			if (!started)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error(), "無法以獨立程序啟動遊戲。");
			}

			CloseHandle(processInformation.Thread);
			CloseHandle(processInformation.Process);
		}
		else
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = gamePath,
				WorkingDirectory = appDir,
				Arguments = "--quiet",
				UseShellExecute = true,
			});
		}

		Environment.Exit(0);
	}

	private static HttpClient CreateHttpClient()
	{
		var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
		client.DefaultRequestHeaders.UserAgent.ParseAdd("shuiling-launcher/2.0");
		return client;
	}

	private static void DeleteDirectoryIfPresent(string path)
	{
		if (Directory.Exists(path))
		{
			try { Directory.Delete(path, true); } catch { /* 下一步會回報實際占用錯誤 */ }
		}
	}

	private static void DeleteDirectoryRequired(string path)
	{
		if (Directory.Exists(path))
		{
			Directory.Delete(path, true);
		}
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { }
	}

	private static void Log(string message) => Console.WriteLine($"[更新器] {message}");

	private static int Fail(string message)
	{
		Console.WriteLine();
		Console.WriteLine("[錯誤] " + message);
		Console.WriteLine("按 Enter 關閉…");
		Console.ReadLine();
		return 1;
	}
}
