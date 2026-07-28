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

`launcher/launcher.cfg` 已經設定為目前專案：

```
owner=WATBUD
repo=shuiling
gameExe=shuiling.exe
appDir=app
```

如果日後更改 GitHub repo 或 Godot 匯出的 exe 名稱，再修改這個檔案即可。

---

## 2. 編譯更新器 → 產生 ShuilingLauncher.exe（一次）

在 repo 根目錄執行：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build_launcher.ps1
```

產物：`dist\ShuilingLauncher-win-x64.zip`。
壓縮包內已包含 `ShuilingLauncher.exe` 與 `launcher.cfg`，自帶 .NET，朋友電腦不用安裝 SDK。

---

## 3. 做「初始安裝包」寄給朋友（只需一次）

直接把 `dist\ShuilingLauncher-win-x64.zip` 寄給朋友一次。之後**再也不用寄新版遊戲檔**。

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

## 4. 每次要發新測試版（已自動化）

平常開發照常 commit 並 push 到 `main`，不會發布遊戲。確認版本可交付後，
才把完成內容推送到 `release`：

```bash
git push origin main:release
```

`.github/workflows/release-game.yml` 會在 GitHub 的 Windows runner：

- 安裝 Godot 4.7 Mono 與匯出模板；
- 編譯 C# 並匯出 Windows 遊戲；
- 將最新版號自動加一；
- 產生 `game.zip`、SHA-256 與 `version.json`；
- 建立 GitHub Release。

只有 `release` 分支會觸發發布。朋友下次開 `ShuilingLauncher.exe` 就會自動更新。只修改 Markdown、`docs/`
或 `launcher/` 不會發布整個遊戲，避免無意義下載。

若需要指定大版本，可到 GitHub → Actions → **Build and publish game update** →
**Run workflow**，輸入例如 `0.2.0`；留空則仍會自動加一。

---

## 5. 運作原理（簡述）

- 更新器讀取 GitHub 的 `https://github.com/OWNER/REPO/releases/latest/download/version.json`
  （`latest/download` 永遠指向最新發佈，免 API token、不會被限流）。
- 和本機 `app/installed_version.txt` 比對，不同就抓 `game.zip`。
- 下載完成後先驗證檔案大小及 SHA-256，再解壓到 `.update/` 暫存區。
- 驗證遊戲主程式存在後才切換版本；若安裝失敗，會自動還原上一版。
- 更新器與遊戲是兩個獨立程式，覆蓋 `app/`（含 C# 的 dll）時不會被鎖檔。

## 6. 安全性

- **朋友端零帳密**：只做「下載公開檔案」，程式裡沒有任何 token/密碼。
- **更新包完整性**：發布時產生 SHA-256，朋友端驗證一致才會安裝。
- **失敗可回復**：下載中斷、壓縮包損壞或替換失敗都不會刪掉可用舊版。
- 你的 GitHub 授權由 `gh` 管理，token 只存在你 Windows 本機（系統憑證管理員），不進 repo、不外流。
- 代價：公開 repo 的 Release 任何人有網址都可下載測試包（封測通常可接受）。要非公開再另談私有載點。

## 7. 常見問題

- **朋友按了沒反應/閃退**：多半是 `launcher.cfg` 的 `owner`/`repo`/`gameExe` 填錯，或該 repo 還沒有任何 Release。
- **`gh` 說沒權限**：先 `gh auth login`，並確認登入的帳號對該 repo 有寫入權。
- **想改載點（不用 GitHub）**：更新器是你的，改 `Program.cs` 裡組 URL 的那兩行（`versionUrl`/`zipUrl`）指向你自己的伺服器即可；朋友端不用換（除非要重出 exe）。
- **git 忽略**：建議把 `launcher/bin/` 與 `launcher/obj/` 加進 `.gitignore`（編譯產物不必進版控）。
