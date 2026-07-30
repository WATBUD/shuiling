$ErrorActionPreference = "Stop"

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$catalogRoot = Join-Path $projectRoot "configs\items"
$catalogs = @(
    @{ File = "equipment.json"; System = "equipment" },
    @{ File = "core_skills.json"; System = "core_skills" },
    @{ File = "support_cores.json"; System = "support_cores" },
    @{ File = "consumables.json"; System = "consumables" },
    @{ File = "materials.json"; System = "materials" }
)

$numericIds = @{}
$stableIds = @{}
$itemCount = 0

foreach ($catalog in $catalogs) {
    $path = Join-Path $catalogRoot $catalog.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing item catalog: $($catalog.File)"
    }

    $document = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($document.system -ne $catalog.System) {
        throw "$($catalog.File): expected system '$($catalog.System)', got '$($document.system)'."
    }
    if (-not $document.items -or $document.items.Count -eq 0) {
        throw "$($catalog.File): items must not be empty."
    }

    foreach ($item in $document.items) {
        $number = [int]$item.uniqueId
        $stableId = [string]$item.id
        if ($number -le 0) {
            throw "$($catalog.File): uniqueId must be a positive integer, got $number."
        }
        if ([string]::IsNullOrWhiteSpace($stableId)) {
            throw "$($catalog.File): uniqueId $number has an empty string id."
        }
        if ($numericIds.ContainsKey($number)) {
            throw "Duplicate uniqueId $number in '$($numericIds[$number])' and '$stableId'."
        }
        if ($stableIds.ContainsKey($stableId)) {
            throw "Duplicate string id '$stableId' at $($stableIds[$stableId]) and $number."
        }

        $numericIds[$number] = $stableId
        $stableIds[$stableId] = $number
        $itemCount++
    }
}

$nextUniqueId = (($numericIds.Keys | Measure-Object -Maximum).Maximum + 1)
Write-Host "Item catalog validation passed: $itemCount globally unique items. Next suggested uniqueId: $nextUniqueId."
