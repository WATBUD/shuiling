<#
  一鍵發佈新測試版到 GitHub Releases（在 Windows PowerShell 執行）。

  用法:
    powershell -ExecutionPolicy Bypass -File tools\publish_release.ps1 -Version 0.2.0 -ExportDir C:\exports\shuiling-windows

  需求:
    - 已安裝 GitHub CLI (gh) 並登入:  winget install GitHub.cli  然後  gh auth login
    - -ExportDir 內是 Godot 匯出的 Windows 完整檔案（含 shuiling.exe、.pck、dll…）

  動作:
    1. 產生 version.json（內含版本號）
    2. 把匯出資料夾壓成 game.zip
    3. 用 gh 建立標籤為 v<版本號> 的 Release，並上傳 game.zip + version.json
  之後朋友的更新器會自動抓到最新版。
#>

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ExportDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExportDir -PathType Container)) {
    Write-Error "找不到匯出資料夾: $ExportDir"
    exit 1
}

$work = Join-Path $env:TEMP ("shuiling_release_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Write-Host "[發佈] 版本 $Version"

    # 1) version.json
    $versionJson = Join-Path $work "version.json"
    "{`n  `"version`": `"$Version`"`n}" | Set-Content -LiteralPath $versionJson -Encoding utf8

    # 2) game.zip（把匯出資料夾內容壓進 zip 頂層）
    $gameZip = Join-Path $work "game.zip"
    Write-Host "[發佈] 壓縮遊戲檔案…"
    Compress-Archive -Path (Join-Path $ExportDir "*") -DestinationPath $gameZip -Force

    # 3) 建立 Release 並上傳資產
    $tag = "v$Version"
    Write-Host "[發佈] 建立 GitHub Release $tag 並上傳…"
    & gh release view $tag *> $null
    if ($LASTEXITCODE -eq 0) {
        & gh release upload $tag $gameZip $versionJson --clobber
    } else {
        & gh release create $tag $gameZip $versionJson --title "水靈 測試版 $Version" --notes "自動發佈的測試版 $Version"
    }

    Write-Host "[完成] 已發佈 $Version。朋友下次開更新器就會自動更新。"
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
