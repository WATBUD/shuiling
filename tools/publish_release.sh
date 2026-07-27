#!/usr/bin/env bash
# 一鍵發佈新測試版到 GitHub Releases（在 Mac/Linux 上執行）。
#
# 用法:
#   tools/publish_release.sh <版本號> <Godot 匯出資料夾>
# 範例:
#   tools/publish_release.sh 0.2.0 ~/exports/shuiling-windows
#
# 需求:
#   - 已安裝 GitHub CLI (gh) 並登入:  brew install gh && gh auth login
#   - <Godot 匯出資料夾> 內是 Windows 匯出的完整檔案（含 shuiling.exe、.pck、dll…）
#
# 動作:
#   1. 產生 version.json（內含版本號）
#   2. 把匯出資料夾壓成 game.zip
#   3. 用 gh 建立標籤為 v<版本號> 的 Release，並上傳 game.zip + version.json
# 之後朋友的更新器會自動抓到最新版。

set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "用法: $0 <版本號> <Godot 匯出資料夾>"
  echo "範例: $0 0.2.0 ~/exports/shuiling-windows"
  exit 1
fi

VERSION="$1"
EXPORT_DIR="$2"

if [ ! -d "$EXPORT_DIR" ]; then
  echo "[錯誤] 找不到匯出資料夾: $EXPORT_DIR"
  exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

echo "[發佈] 版本 $VERSION"

# 1) version.json
printf '{\n  "version": "%s"\n}\n' "$VERSION" > "$WORK_DIR/version.json"

# 2) game.zip（把匯出資料夾「內容」壓進 zip 的頂層；更新器會處理是否多一層）
GAME_ZIP="$WORK_DIR/game.zip"
echo "[發佈] 壓縮遊戲檔案…"
( cd "$EXPORT_DIR" && zip -r -q "$GAME_ZIP" . )

# 3) 建立 Release 並上傳資產
TAG="v$VERSION"
echo "[發佈] 建立 GitHub Release $TAG 並上傳…"
if gh release view "$TAG" >/dev/null 2>&1; then
  # 已存在同標籤：覆蓋資產（--clobber）
  gh release upload "$TAG" "$GAME_ZIP" "$WORK_DIR/version.json" --clobber
else
  gh release create "$TAG" "$GAME_ZIP" "$WORK_DIR/version.json" \
    --title "水靈 測試版 $VERSION" \
    --notes "自動發佈的測試版 $VERSION"
fi

echo "[完成] 已發佈 $VERSION。朋友下次開更新器就會自動更新。"
