param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$ExportDir,
    [string]$GameExe = "shuiling.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ExportDir -PathType Container)) {
    throw "Export directory does not exist: $ExportDir"
}
if (-not (Test-Path -LiteralPath (Join-Path $ExportDir $GameExe) -PathType Leaf)) {
    throw "Game executable was not found: $(Join-Path $ExportDir $GameExe)"
}
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI was not found. Install it with: winget install GitHub.cli"
}

$work = Join-Path $env:TEMP ("shuiling_release_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    Write-Host "[Release] Building version $Version"

    $gameZip = Join-Path $work "game.zip"
    Write-Host "[Release] Compressing the exported game..."
    Compress-Archive -Path (Join-Path $ExportDir "*") -DestinationPath $gameZip -Force

    $versionJson = Join-Path $work "version.json"
    $package = Get-Item -LiteralPath $gameZip
    $hash = (Get-FileHash -LiteralPath $gameZip -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{
        version = $Version
        sha256 = $hash
        size = $package.Length
    } | ConvertTo-Json | Set-Content -LiteralPath $versionJson -Encoding utf8

    $tag = "v$Version"
    Write-Host "[Release] Uploading $tag to GitHub Releases..."
    & gh release view $tag *> $null
    if ($LASTEXITCODE -eq 0) {
        & gh release upload $tag $gameZip $versionJson --clobber
    } else {
        & gh release create $tag $gameZip $versionJson --title "Shuiling Test Build $Version" --notes "Automatic game update $Version"
    }
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub Release upload failed."
    }

    Write-Host "[Release] Version $Version is live. Launchers will install it automatically."
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
