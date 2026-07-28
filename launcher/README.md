# 水靈 遊戲更新器（Launcher）

讓朋友「按一下就拿到最新測試版」，你不用每次重新打包寄檔。
更新檔案託管在 **GitHub Releases**，更新器抓 `releases/latest/download/...`。
下載後會驗證 SHA-256，並使用暫存安裝與失敗回復，避免更新中斷破壞現有遊戲。

---

## 架構

朋友電腦上的資料夾長這樣：

```
水靈/
├─ ShuilingLauncher.exe   ← 朋友執行「這個」
├─ launcher.cfg           ← 設定（GitHub 帳號/repo、遊戲執行檔名）
└─ app/                   ← 遊戲本體（更新器會整包覆蓋這裡）
   ├─ shuiling.exe
   ├─ *.dll / *.pck / 資料…
   └─ installed_version.txt  ← 更新器記錄目前版本
```

因為更新器和遊戲是**兩個獨立程式**，覆蓋 `app/`（含 C# 的 dll）時不會被執行中的遊戲鎖住檔案。

> 註：Godot **C# 專案無法在執行期熱抽換程式碼**，所以走「整包自動更新」。每次改 C# 玩法都能靠這個更新器送到朋友手上。

---

## 一次性設定

### 1. 填 `launcher.cfg`
目前專案已設定完成：

```
owner=WATBUD
repo=shuiling
gameExe=shuiling.exe
appDir=app
```

### 2. 編譯更新器（產生 Windows 單一執行檔）
在 repo 根目錄執行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build_launcher.ps1
```

產物在 `dist\ShuilingLauncher-win-x64.zip`。

### 3. 打包「初始安裝包」寄給朋友（只需一次）
直接把 `dist\ShuilingLauncher-win-x64.zip` 給朋友一次。
`app/` 會在第一次啟動更新器時自動下載建立。

之後**再也不用寄檔**——朋友重開更新器就會自動更新。

---

## 每次要發新測試版

1. 在 Godot 匯出 **Windows** 版到某個資料夾（例如 `C:\exports\shuiling-windows`，內含 `shuiling.exe`、`.pck`、`.dll` 等）。
2. 執行發佈腳本：

   **Windows（你的打包環境，推薦）** — 需先 `winget install GitHub.cli` 然後 `gh auth login`：

   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\publish_release.ps1 -Version 0.2.0 -ExportDir C:\exports\shuiling-windows
   ```

   **Mac/Linux（備用）** — 需先 `brew install gh && gh auth login`：

   ```bash
   tools/publish_release.sh 0.2.0 ~/exports/shuiling-windows
   ```

   腳本會：壓縮 `game.zip` → 計算 SHA-256 → 產生 `version.json` →
   建立 GitHub Release `v0.2.0` 並上傳。

3. 朋友下次開 `ShuilingLauncher.exe` → 偵測到新版 → 自動下載 `app/` 覆蓋 → 啟動遊戲。

---

## 版本比對規則
更新器把遠端 `version.json` 的 `version` 和本機 `app/installed_version.txt`
比對，不一樣就更新。更新包的 `size` 與 `sha256` 也必須驗證成功才會安裝。

## 之後想換掉 GitHub
更新機制是你自己的：把 `launcher.cfg` 或 `Program.cs` 裡的下載網址改成你自己的伺服器 base URL 即可，朋友端不用換更新器（除非 exe 本身要改）。
