param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$launcherProject = Join-Path $repoRoot "launcher\Launcher.csproj"
$launcherConfig = Join-Path $repoRoot "launcher\launcher.cfg"
$publishDir = Join-Path $repoRoot "launcher\bin\Release\net8.0\$Runtime\publish"
$stageDir = Join-Path $env:TEMP ("shuiling_launcher_" + [Guid]::NewGuid().ToString("N"))
$distDir = Join-Path $repoRoot "dist"
$outputZip = Join-Path $distDir "ShuilingLauncher-$Runtime.zip"

Write-Host "[Launcher] Publishing the Windows single-file executable..."
& dotnet publish $launcherProject -c Release -r $Runtime --self-contained true "-p:PublishSingleFile=true"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publishDir "ShuilingLauncher.exe") -Destination $stageDir
Copy-Item -LiteralPath $launcherConfig -Destination $stageDir
New-Item -ItemType Directory -Path $distDir -Force | Out-Null
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $outputZip -Force
Remove-Item -LiteralPath $stageDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "[Launcher] Installer package created: $outputZip"
