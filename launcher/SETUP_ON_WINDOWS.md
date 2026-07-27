# 在 Windows 建置遊戲更新器 — 操作手冊

這份是「帶到 Windows 電腦照做」的完整步驟。相關檔案已在 repo：
`launcher/`（更新器程式、設定、說明）與 `tools/`（發佈腳本）。

目標：朋友只執行一次安裝，之後每次你發新版，他們開更新器就自動更新，你不用再打包寄檔。

---

## 0. 前置安裝（Windows，一次）

1. **.NET 8 SDK**：https://dotnet.microsoft.com/download/dotnet/8.0 （用來編譯更新器）
2. **GitHub CLI**：`winget install GitHub.cli`，然後 `gh auth login`（瀏覽器登入，token 只存在你本機，不會進程式碼）
3. 這個專案已在 GitHub 有一個 repo（記下你的帳號 `owner` 與 repo 名，例如 `shuiling`）

---

## 1. 設定 launcher.cfg（一次）

打開 `launcher/launcher.cfg`，改成你的資料：

```
owner=你的GitHub帳號
repo=shuiling
gameExe=shuiling.exe      ← 改成 Godot 匯出的實際 exe 檔名
appDir=app
```

> `gameExe` 要跟 Godot「匯出設定」裡的檔名一致。

---

## 2. 編譯更新器 → 產生 ShuilingLauncher.exe（一次）

在 `launcher/` 資料夾開 PowerShell：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

產物：`launcher\bin\Release\net8.0\win-x64\publish\ShuilingLauncher.exe`
（自帶 .NET，朋友電腦不用另外安裝任何東西。）

---

## 3. 做「初始安裝包」寄給朋友（只需一次）

新建一個資料夾，放入：

- `ShuilingLauncher.exe`（上一步的產物）
- `launcher.cfg`（你改好的那份）

壓成 zip 寄給朋友。之後**再也不用寄檔**。

朋友的資料夾第一次啟動後會變成：

```
水靈/
├─ ShuilingLauncher.exe   ← 朋友執行這個
├─ launcher.cfg
└─ app/                   ← 更新器自動下載/覆蓋（遊戲本體在這）
   ├─ shuiling.exe
   └─ *.pck / *.dll / 資料 …
```

---

## 4. 每次要發新測試版（重點循環）

1. Godot → 匯出 **Windows** 版到一個資料夾，例如 `C:\exports\shuiling-windows`
   （裡面要有 `shuiling.exe`、`.pck`、`.dll` 等完整檔案）
2. 在 repo 根目錄開 PowerShell 跑：

```powershell
powershell -ExecutionPolicy Bypass -File tools\publish_release.ps1 -Version 0.2.0 -ExportDir C:\exports\shuiling-windows
```

腳本會自動：產生 `version.json` → 壓成 `game.zip` → 建立 GitHub Release `v0.2.0` 並上傳。

3. 朋友下次開 `ShuilingLauncher.exe`：偵測到新版 → 下載覆蓋 `app/` → 啟動遊戲。

> 版本號自己定（`0.2.0`、`0.2.1`…），只要每次都改動即可，更新器用字串比對判斷是否更新。

---

## 5. 運作原理（簡述）

- 更新器讀取 GitHub 的 `https://github.com/OWNER/REPO/releases/latest/download/version.json`
  （`latest/download` 永遠指向最新發佈，免 API token、不會被限流）。
- 和本機 `app/installed_version.txt` 比對，不同就抓 `game.zip` 覆蓋。
- 更新器與遊戲是兩個獨立程式，覆蓋 `app/`（含 C# 的 dll）時不會被鎖檔。

## 6. 安全性

- **朋友端零帳密**：只做「下載公開檔案」，程式裡沒有任何 token/密碼。
- 你的 GitHub 授權由 `gh` 管理，token 只存在你 Windows 本機（系統憑證管理員），不進 repo、不外流。
- 代價：公開 repo 的 Release 任何人有網址都可下載測試包（封測通常可接受）。要非公開再另談私有載點。

## 7. 常見問題

- **朋友按了沒反應/閃退**：多半是 `launcher.cfg` 的 `owner`/`repo`/`gameExe` 填錯，或該 repo 還沒有任何 Release。
- **`gh` 說沒權限**：先 `gh auth login`，並確認登入的帳號對該 repo 有寫入權。
- **想改載點（不用 GitHub）**：更新器是你的，改 `Program.cs` 裡組 URL 的那兩行（`versionUrl`/`zipUrl`）指向你自己的伺服器即可；朋友端不用換（除非要重出 exe）。
- **git 忽略**：建議把 `launcher/bin/` 與 `launcher/obj/` 加進 `.gitignore`（編譯產物不必進版控）。
