<#
    Editor-free acceptance gate for the first playable construction slice.

    This gate intentionally fails while Town Center or Resource Dropoff are
    only design contracts. It does not create placeholder assets. Use
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

$issues = New-Object 'System.Collections.Generic.List[string]'
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
}
$modelStates = @{}

foreach ($code in @('town_center', 'resource_dropoff')) {
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
                if ($grayboxFiles.Count -gt 0) {
                    $modelStates[$code] = 'model-intermediate'
                    Add-Issue $issues $code 'Model source is model-intermediate graybox; replace with authored FBX/GLB and import it in Unity.'
                    foreach ($graybox in $grayboxFiles) {
                        $grayboxText = Get-Content -Raw -LiteralPath $graybox.FullName
                        if ($grayboxText -notmatch '(?m)^\s*v\s+' -or $grayboxText -notmatch '(?m)^\s*f\s+')
                            { Add-Issue $issues $code "Graybox OBJ has no usable vertex/face data: $(Relative-Path $graybox.FullName)" }
                        if (!(Test-Path -LiteralPath ($graybox.FullName + '.meta') -PathType Leaf))
                            { Add-Issue $issues $code "Graybox OBJ metadata is missing: $(Relative-Path $graybox.FullName).meta" }
                    }
                } else {
                    $modelStates[$code] = 'model-authored'
                }
            }
        }
    }
}

$stateSummary = @('town_center', 'resource_dropoff') | ForEach-Object {
    "$_=$($modelStates[$_])"
}
Write-Host ('Foundation model states: ' + ($stateSummary -join ', '))

if ($issues.Count -gt 0) {
    $message = "Foundation asset gate failed with $($issues.Count) issue(s):`n - " + ($issues -join "`n - ")
    Write-Host 'Unity import checklist still required for each foundation:'
    foreach ($code in @('town_center', 'resource_dropoff')) {
        foreach ($requirement in $requirements[$code]) { Write-Host " - $code :: $requirement" }
    }
    if ($ReportOnly) {
        Write-Warning $message
        exit 0
    }
    throw $message
}

Write-Host 'Foundation asset gate passed: Town Center and Resource Dropoff are repository-ready for Unity review.'
