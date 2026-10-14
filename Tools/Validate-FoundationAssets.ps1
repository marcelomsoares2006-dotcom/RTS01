<#
    Editor-free acceptance gate for the first playable construction slice.

    This gate intentionally fails while Town Center, Resource Dropoff, Lumber Camp,
    Mining Camp, Mill or Barracks are only design contracts. It does not create placeholder assets. Use
    -ReportOnly to print the complete checklist while artists are working.
    Unity is still required to verify serialized component types and runtime
    placement; this script validates the repository-facing part of the gate.
#>
param(
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot

function Relative-Path([string]$Path) {
    return $Path.Substring($projectRoot.Length + 1).Replace('\', '/')
}

function Add-Issue([System.Collections.Generic.List[string]]$Issues, [string]$Code, [string]$Requirement) {
    $Issues.Add("$Code :: $Requirement")
}

function Get-ManifestDouble([string]$Body, [string]$Field, [string]$Code, [System.Collections.Generic.List[string]]$Issues) {
    $match = [regex]::Match($Body, "(?m)^\s+${Field}:\s*([^\r\n]+)")
    $value = 0.0
    if (!$match.Success -or ![double]::TryParse(
            $match.Groups[1].Value.Trim(),
            [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref]$value) -or $value -le 0.0) {
        Add-Issue $Issues $Code "$Field must be a positive invariant-culture number."
        return $null
    }
    return $value
}

$issues = New-Object 'System.Collections.Generic.List[string]'
$manifestPath = Join-Path $projectRoot 'Docs/FOUNDATION_IMPORT_MANIFEST.yml'
$metricsPath = Join-Path $projectRoot 'Assets/Resources/Imports/Units/Buildings/foundation-asset-metrics.json'
if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Add-Issue $issues '<manifest>' 'FOUNDATION_IMPORT_MANIFEST.yml is missing.'
}
$manifestText = if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
    Get-Content -Raw -LiteralPath $manifestPath
} else { '' }
$manifestBlocks = [regex]::Matches($manifestText, '(?ms)^\s+- code:\s*(\S+)(.*?)(?=^\s+- code:|\z)')
$manifestByCode = @{}
foreach ($manifestBlock in $manifestBlocks) {
    $manifestByCode[$manifestBlock.Groups[1].Value.ToLowerInvariant()] = $manifestBlock.Groups[0].Value
}
foreach ($manifestCode in @('town_center', 'resource_dropoff', 'lumber_camp', 'mining_camp', 'mill', 'barracks')) {
    if (!$manifestByCode.ContainsKey($manifestCode)) {
        Add-Issue $issues $manifestCode 'Import manifest entry is missing.'
        continue
    }
    $manifestBody = $manifestByCode[$manifestCode]
    foreach ($manifestField in @('intermediateSource', 'authoredSource', 'scaleMeters', 'footprintMeters', 'footprintToleranceMeters', 'maxHeightMeters', 'expectedPrefab', 'expectedBuildingData', 'minimumEra', 'requiredComponents', 'approvalChecks', 'promotionCriteria')) {
        if ($manifestBody -notmatch "(?m)^\s+${manifestField}:\s*[^\r\n]+")
            { Add-Issue $issues $manifestCode "Import manifest field is missing: $manifestField" }
    }
    $intermediate = [regex]::Match($manifestBody, '(?m)^\s+intermediateSource:\s*([^\r\n]+)')
    if ($intermediate.Success -and !(Test-Path -LiteralPath (Join-Path $projectRoot $intermediate.Groups[1].Value.Trim()) -PathType Leaf))
        { Add-Issue $issues $manifestCode "Manifest intermediate source is missing: $($intermediate.Groups[1].Value.Trim())" }
    $authored = [regex]::Match($manifestBody, '(?m)^\s+authoredSource:\s*([^\r\n]+)')
    if ($authored.Success -and !(Test-Path -LiteralPath (Join-Path $projectRoot $authored.Groups[1].Value.Trim()) -PathType Leaf))
        { Add-Issue $issues $manifestCode "Manifest authored source is missing: $($authored.Groups[1].Value.Trim())" }
    $components = [regex]::Match($manifestBody, '(?m)^\s+requiredComponents:\s*([^\r\n]+)')
    if ($components.Success) {
        foreach ($component in @('BuildingManager', 'BuildingBT', 'MeshFilter', 'MeshRenderer', 'Collider')) {
            if ($components.Groups[1].Value -notmatch "(^|,)$component(,|$)")
                { Add-Issue $issues $manifestCode "Manifest component checklist is incomplete: $component" }
        }
    }
}
if ($manifestText -notmatch '(?m)^approval:\s*blocked-until-unity-review\s*$')
    { Add-Issue $issues '<manifest>' 'Import manifest must remain blocked until Unity review.' }
$metrics = $null
if (Test-Path -LiteralPath $metricsPath -PathType Leaf) {
    try { $metrics = Get-Content -Raw -LiteralPath $metricsPath | ConvertFrom-Json }
    catch { Add-Issue $issues '<metrics>' "Foundation metrics JSON is invalid: $($_.Exception.Message)" }
} else {
    Add-Issue $issues '<metrics>' 'foundation-asset-metrics.json is missing.'
}
$catalogPath = Join-Path $projectRoot 'Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset'
if (!(Test-Path -LiteralPath $catalogPath -PathType Leaf)) {
    Add-Issue $issues '<catalog>' 'Building Content Catalog.asset is missing.'
}

$guidIndex = @{}
foreach ($meta in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Recurse -File -Filter '*.meta') {
    $metaText = Get-Content -Raw -LiteralPath $meta.FullName
    $guidMatch = [regex]::Match($metaText, '(?m)^guid:\s*([0-9a-f]{32})\s*$')
    if (!$guidMatch.Success) { continue }
    $metaRelative = Relative-Path $meta.FullName
    $assetRelative = $metaRelative.Substring(0, $metaRelative.Length - 5)
    $guidIndex[$guidMatch.Groups[1].Value] = $assetRelative
}

$catalogText = if (Test-Path -LiteralPath $catalogPath -PathType Leaf) {
    Get-Content -Raw -LiteralPath $catalogPath
} else { '' }
$blocks = [regex]::Matches($catalogText, '(?ms)^\s*- code:\s*(\S+)(.*?)(?=^\s*- code:|\z)')
$byCode = @{}
foreach ($block in $blocks) {
    $byCode[$block.Groups[1].Value.ToLowerInvariant()] = $block.Groups[0].Value
}
foreach ($manifestCode in @('town_center', 'resource_dropoff', 'lumber_camp', 'mining_camp', 'mill', 'barracks')) {
    if ($manifestByCode.ContainsKey($manifestCode) -and $byCode.ContainsKey($manifestCode)) {
        $manifestBody = $manifestByCode[$manifestCode]
        $catalogBody = $byCode[$manifestCode]
        $manifestPrefab = [regex]::Match($manifestBody, '(?m)^\s+expectedPrefab:\s*([^\r\n]+)')
        $catalogPrefab = [regex]::Match($catalogBody, '(?m)^\s*prefabPath:\s*([^\r\n]+)')
        $pathsMatch = $manifestPrefab.Success -and $catalogPrefab.Success -and
            ($manifestPrefab.Groups[1].Value.Trim() -eq $catalogPrefab.Groups[1].Value.Trim())
        if ($manifestPrefab.Success -and $catalogPrefab.Success -and !$pathsMatch)
            { Add-Issue $issues $manifestCode 'Manifest expectedPrefab does not match catalog prefabPath.' }
    }
}

$requirements = @{
    'town_center' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
    'resource_dropoff' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
    'lumber_camp' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
    'mining_camp' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
    'mill' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
    'barracks' = @(
        'Playable status (status >= 2)',
        'BuildingData GUID resolves to an asset',
        'Prefab path and .meta exist',
        'Model path exists and contains the authored model set',
        'BuildingData links a prefab and constructionMeshes',
        'Prefab is reviewed in Unity with BuildingManager, BuildingBT, MeshFilter and collider',
        'Prefab root uses Unit layer (12), Unit tag and valid terrain placement footprint'
    )
}
$modelStates = @{}

foreach ($code in @('town_center', 'resource_dropoff', 'lumber_camp', 'mining_camp', 'mill', 'barracks')) {
    $modelStates[$code] = 'missing'
    if (!$byCode.ContainsKey($code)) {
        Add-Issue $issues $code 'Catalog entry is missing.'
        continue
    }

    $body = $byCode[$code]
    $status = [regex]::Match($body, '(?m)^\s*status:\s*(\d+)\s*$')
    if (!$status.Success -or [int]$status.Groups[1].Value -lt 2)
        { Add-Issue $issues $code 'Entry is still planned/prototype; promote only after every checklist item passes.' }

    $data = [regex]::Match($body, 'buildingData:\s*\{[^}]*guid:\s*([0-9a-f]{32})')
    $dataPath = $null
    if (!$data.Success -or !$guidIndex.ContainsKey($data.Groups[1].Value)) {
        Add-Issue $issues $code 'buildingData GUID is empty or does not resolve to an asset.'
    } else {
        $dataPath = $guidIndex[$data.Groups[1].Value]
        $dataFullPath = Join-Path $projectRoot $dataPath
        if (!(Test-Path -LiteralPath $dataFullPath -PathType Leaf)) {
            Add-Issue $issues $code "BuildingData asset is missing: $dataPath"
        } else {
            $dataText = Get-Content -Raw -LiteralPath $dataFullPath
            if ($dataText -notmatch '(?m)^\s*code:\s*' + [regex]::Escape($code) + '\s*$')
                { Add-Issue $issues $code "BuildingData code must be '$code'." }
            $prefabReference = [regex]::Match($dataText, '(?m)^\s*prefab:\s*\{[^}]*guid:\s*([0-9a-f]{32})')
            if (!$prefabReference.Success)
                { Add-Issue $issues $code 'BuildingData must reference a prefab GUID.' }
            elseif (!$guidIndex.ContainsKey($prefabReference.Groups[1].Value))
                { Add-Issue $issues $code 'BuildingData prefab GUID does not resolve to a repository asset.' }
            if ($dataText -match '(?m)^\s*constructionMeshes:\s*\[\s*\]\s*$' -or $dataText -notmatch '(?m)^\s*constructionMeshes:')
                { Add-Issue $issues $code 'BuildingData must contain at least one construction mesh.' }
        }
    }

    $prefab = [regex]::Match($body, '(?m)^\s*prefabPath:\s*([^\r\n]+)')
    if (!$prefab.Success) {
        Add-Issue $issues $code 'prefabPath is missing.'
    } else {
        $prefabPath = $prefab.Groups[1].Value.Trim()
        $prefabFullPath = Join-Path $projectRoot $prefabPath
        if (!(Test-Path -LiteralPath $prefabFullPath -PathType Leaf)) {
            Add-Issue $issues $code "Prefab file is missing: $prefabPath"
        } elseif (!(Test-Path -LiteralPath ($prefabFullPath + '.meta') -PathType Leaf)) {
            Add-Issue $issues $code "Prefab metadata is missing: $prefabPath.meta"
        } else {
            $prefabText = Get-Content -Raw -LiteralPath $prefabFullPath
            if ($prefabText -notmatch '(?m)^\s*m_Layer:\s*12\s*$')
                { Add-Issue $issues $code 'Prefab must include a Unit-layer object (layer 12).' }
            if ($prefabText -notmatch '(?m)^\s*m_TagString:\s*Unit\s*$')
                { Add-Issue $issues $code 'Prefab must include a Unit-tagged root/object.' }
        }
    }

    $model = [regex]::Match($body, '(?m)^\s*modelPath:\s*([^\r\n]+)')
    if (!$model.Success) {
        Add-Issue $issues $code 'modelPath is missing.'
    } else {
        $modelPath = $model.Groups[1].Value.Trim().TrimEnd('/')
        $modelFullPath = Join-Path $projectRoot $modelPath
        if (!(Test-Path -LiteralPath $modelFullPath)) {
            Add-Issue $issues $code "Model path is missing: $modelPath"
        } elseif ((Get-Item -LiteralPath $modelFullPath).PSIsContainer) {
            $modelFiles = @(Get-ChildItem -LiteralPath $modelFullPath -File)
            if ($modelFiles.Count -eq 0) {
                Add-Issue $issues $code "Model path contains no authored model file: $modelPath"
            } else {
                $grayboxFiles = @($modelFiles | Where-Object { $_.Extension -ieq '.obj' -and $_.Name -match '(?i)graybox' })
                $authoredFiles = @($modelFiles | Where-Object { $_.Extension -in @('.obj', '.fbx', '.glb', '.gltf') -and $_.Name -notmatch '(?i)graybox' })
                if ($authoredFiles.Count -eq 0) {
                    $modelStates[$code] = 'model-intermediate'
                    Add-Issue $issues $code 'Model source is model-intermediate graybox; replace with authored OBJ/FBX/GLB and import it in Unity.'
                    foreach ($graybox in $grayboxFiles) {
                        $grayboxText = Get-Content -Raw -LiteralPath $graybox.FullName
                        if ($grayboxText -notmatch '(?m)^\s*v\s+' -or $grayboxText -notmatch '(?m)^\s*f\s+')
                            { Add-Issue $issues $code "Graybox OBJ has no usable vertex/face data: $(Relative-Path $graybox.FullName)" }
                        if (!(Test-Path -LiteralPath ($graybox.FullName + '.meta') -PathType Leaf))
                            { Add-Issue $issues $code "Graybox OBJ metadata is missing: $(Relative-Path $graybox.FullName).meta" }
                    }
                } else {
                    $modelStates[$code] = 'model-authored'
                    $authoredManifest = [regex]::Match($manifestByCode[$code], '(?m)^\s+authoredSource:\s*([^\r\n]+)')
                    if ($authoredManifest.Success) {
                        $authoredRelative = $authoredManifest.Groups[1].Value.Trim().Replace('\\', '/')
                        $authoredFullPath = Join-Path $projectRoot $authoredRelative
                        if (!(Test-Path -LiteralPath $authoredFullPath -PathType Leaf)) {
                            Add-Issue $issues $code "Manifest authored source is missing: $authoredRelative"
                        } elseif (!$authoredRelative.StartsWith($modelPath + '/', [System.StringComparison]::OrdinalIgnoreCase)) {
                            Add-Issue $issues $code 'Manifest authoredSource must be inside the catalog modelPath.'
                        } elseif ($metrics -ne $null) {
                            $metric = @($metrics.assets | Where-Object { $_.path -eq $authoredRelative }) | Select-Object -First 1
                            if ($metric -eq $null) {
                                Add-Issue $issues $code "Metrics do not describe authoredSource: $authoredRelative"
                            } else {
                                $scale = Get-ManifestDouble $manifestByCode[$code] 'scaleMeters' $code $issues
                                $tolerance = Get-ManifestDouble $manifestByCode[$code] 'footprintToleranceMeters' $code $issues
                                $maxHeight = Get-ManifestDouble $manifestByCode[$code] 'maxHeightMeters' $code $issues
                                $footprint = [regex]::Match($manifestByCode[$code], '(?m)^\s+footprintMeters:\s*([0-9]+(?:\.[0-9]+)?)x([0-9]+(?:\.[0-9]+)?)\s*$')
                                if (!$footprint.Success) {
                                    Add-Issue $issues $code 'footprintMeters must use widthxdepth in metres.'
                                } else {
                                    $width = [double]$metric.boundsMax[0] - [double]$metric.boundsMin[0]
                                    $depth = [double]$metric.boundsMax[2] - [double]$metric.boundsMin[2]
                                    $height = [double]$metric.boundsMax[1] - [double]$metric.boundsMin[1]
                                    if ($scale -ne $null -and [math]::Abs($scale - 1.0) -gt 0.0001)
                                        { Add-Issue $issues $code 'Authored model scale must be 1 metre per Unity unit.' }
                                    if ($tolerance -ne $null -and ($width -gt ([double]$footprint.Groups[1].Value + $tolerance) -or $depth -gt ([double]$footprint.Groups[2].Value + $tolerance)))
                                        { Add-Issue $issues $code ("Authored bounds exceed footprint envelope: {0:F2}x{1:F2}m" -f $width, $depth) }
                                    if ($maxHeight -ne $null -and $height -gt $maxHeight)
                                        { Add-Issue $issues $code ("Authored height {0:F2}m exceeds maxHeightMeters {1:F2}m" -f $height, $maxHeight) }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

$stateSummary = @('town_center', 'resource_dropoff', 'lumber_camp', 'mining_camp', 'mill', 'barracks') | ForEach-Object {
    "$_=$($modelStates[$_])"
}
Write-Host ('Foundation model states: ' + ($stateSummary -join ', '))

if ($issues.Count -gt 0) {
    $message = "Foundation asset gate failed with $($issues.Count) issue(s):`n - " + ($issues -join "`n - ")
    Write-Host 'Unity import checklist still required for each foundation:'
    foreach ($code in @('town_center', 'resource_dropoff', 'lumber_camp', 'mining_camp', 'mill', 'barracks')) {
        foreach ($requirement in $requirements[$code]) { Write-Host " - $code :: $requirement" }
    }
    if ($ReportOnly) {
        Write-Warning $message
        exit 0
    }
    throw $message
}

Write-Host 'Foundation asset gate passed: Town Center, Resource Dropoff, Lumber Camp, Mining Camp, Mill and Barracks are repository-ready for Unity review.'
