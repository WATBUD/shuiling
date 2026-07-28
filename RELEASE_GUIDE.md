# 水靈：發布與自動更新極簡教學

## 平常開發

程式碼放在 `main` 分支。平常照常提交並推送，不會發布遊戲：

```powershell
git push origin main
```

## 要讓朋友更新

確認遊戲已測試完成後，把 `main` 推送到 `release`：

```powershell
git push origin main:release
```

接下來不用手動打包。GitHub Actions 會自動：

1. 安裝 Godot 4.7 Mono。
2. 編譯並匯出 Windows 遊戲。
3. 自動增加版本號。
4. 建立 `game.zip` 與 SHA-256 驗證資訊。
5. 發布到 GitHub Release。

朋友重新開啟 `ShuilingLauncher.exe` 就會自動下載最新版。

> 未完成或未測試的內容不要推送到 `release`。

## 查看發布結果

- [自動建置紀錄](https://github.com/WATBUD/shuiling/actions)
- [已發布版本](https://github.com/WATBUD/shuiling/releases)

Actions 顯示綠色勾勾才代表發布成功。若失敗，不會覆蓋原本的 Release，
朋友仍可繼續使用已安裝的版本。

## 第一次把遊戲給朋友

只需給朋友一次 `ShuilingLauncher-win-x64.zip`。朋友解壓後執行
`ShuilingLauncher.exe`，遊戲本體會自動下載。

只有 Launcher 本身修改時，才需要重新建立初始包：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build_launcher.ps1
```

產物位於：

```text
dist\ShuilingLauncher-win-x64.zip
```

一般遊戲內容更新不需要重新傳送 Launcher。
