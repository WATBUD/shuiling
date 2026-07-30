$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeExtensions = @(
    ".cs", ".gd", ".tscn", ".tres", ".res", ".godot",
    ".cfg", ".json", ".shader", ".gdshader"
)

$violations = @()
$trackedFiles = @(git -C $projectRoot ls-files)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to list Git-tracked project files."
}

foreach ($relativePath in $trackedFiles) {
    if ($runtimeExtensions -notcontains [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()) {
        continue
    }

    $fullPath = Join-Path $projectRoot $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        continue
    }

    $matches = Select-String -LiteralPath $fullPath -SimpleMatch -Pattern "assets/_downloads", "assets\_downloads"
    foreach ($match in $matches) {
        $violations += "$relativePath`:$($match.LineNumber): $($match.Line.Trim())"
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Runtime files must not reference the ignored local download pool:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    throw "Move required assets into a Git-tracked runtime directory before publishing."
}

Write-Host "Runtime asset validation passed: no tracked runtime file references assets/_downloads."
