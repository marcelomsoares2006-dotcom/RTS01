<#
Release preflight and optional Unity validation for RTS01.

This script performs deterministic checks that do not require Unity, then can
invoke the project's existing Unity validation script when a licensed editor
is available. It deliberately does not edit project assets or gameplay files.
#>
param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [switch]$WithUnity,
    [switch]$Migrate,
    [switch]$Build
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot

function Require-File([string]$RelativePath) {
    $path = Join-Path $projectRoot $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required project file is missing: $RelativePath"
    }
}

function Get-AssetGuidIndex {
    $index = @{}
    $assetsRoot = Join-Path $projectRoot 'Assets'
    foreach ($meta in Get-ChildItem -LiteralPath $assetsRoot -Recurse -File -Filter '*.meta') {
        $metaText = Get-Content -Raw -LiteralPath $meta.FullName
        $guidMatch = [regex]::Match($metaText, '(?m)^guid:\s*([0-9a-f]{32})\s*$')
        if (!$guidMatch.Success) { continue }
        $assetPath = $meta.FullName.Substring($projectRoot.Length + 1, $meta.FullName.Length - $projectRoot.Length - 6).Replace('\', '/')
        $guid = $guidMatch.Groups[1].Value
        if ($index.ContainsKey($guid) -and $index[$guid] -ne $assetPath) {
            throw "Duplicate asset GUID $guid in $($index[$guid]) and $assetPath"
        }
        $index[$guid] = $assetPath
    }
    return $index
}

function Get-RelativeAssetPath([string]$AbsolutePath) {
    return $AbsolutePath.Substring($projectRoot.Length + 1).Replace('\', '/')
}

function Validate-ScriptMetaFiles {
    $missing = @()
    $checked = 0
    foreach ($script in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Assets') -Recurse -File |
        Where-Object { @('.cs', '.asmdef', '.shader', '.compute') -contains $_.Extension.ToLowerInvariant() }) {
        $checked++
        if (!(Test-Path -LiteralPath ($script.FullName + '.meta') -PathType Leaf)) {
            $missing += Get-RelativeAssetPath $script.FullName
        }
    }
    if ($missing.Count -gt 0) {
        throw ("Assets without Unity .meta files ({0}): {1}" -f $missing.Count, ($missing -join ', '))
    }
    return $checked
}

function Validate-BuildingContentCatalog([hashtable]$GuidIndex) {
    $catalogPath = 'Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset'
    Require-File $catalogPath
    $catalogText = Get-Content -Raw -LiteralPath (Join-Path $projectRoot $catalogPath)
    $blocks = [regex]::Matches($catalogText, '(?ms)^\s*- code:\s*(\S+)(.*?)(?=^\s*- code:|\z)')
    if ($blocks.Count -eq 0) { throw "Building content catalog has no entries: $catalogPath" }
    $codes = @{}
    $referenceCount = 0
    foreach ($block in $blocks) {
        $code = $block.Groups[1].Value
        if ($codes.ContainsKey($code.ToLowerInvariant())) { throw "Duplicate building catalog code: $code" }
        $codes[$code.ToLowerInvariant()] = $true
        $body = $block.Groups[2].Value
        $guidMatch = [regex]::Match($body, 'buildingData:\s*\{[^}]*guid:\s*([0-9a-f]{32})')
        if ($guidMatch.Success) {
            $referenceCount++
            $guid = $guidMatch.Groups[1].Value
            if (!$GuidIndex.ContainsKey($guid)) {
                throw "Building catalog entry '$code' references missing asset GUID $guid"
            }
        }
        $statusMatch = [regex]::Match($body, '(?m)^\s+status:\s*(\d+)\s*$')
        $prefabMatch = [regex]::Match($body, '(?m)^\s+prefabPath:\s*([^\r\n]+)\s*$')
        $modelMatch = [regex]::Match($body, '(?m)^\s+modelPath:\s*([^\r\n]+)\s*$')
        if (!$prefabMatch.Success -or [string]::IsNullOrWhiteSpace($prefabMatch.Groups[1].Value)) {
            throw "Building catalog entry '$code' has no prefabPath."
        }
        if (!$modelMatch.Success -or [string]::IsNullOrWhiteSpace($modelMatch.Groups[1].Value)) {
            throw "Building catalog entry '$code' has no modelPath."
        }
        if ($statusMatch.Success -and [int]$statusMatch.Groups[1].Value -ge 2) {
            $prefabPath = $prefabMatch.Groups[1].Value.Trim()
            Require-File $prefabPath
            $modelPath = $modelMatch.Groups[1].Value.Trim().TrimEnd('/')
            if (!(Test-Path -LiteralPath (Join-Path $projectRoot $modelPath))) {
                throw "Playable building catalog entry '$code' references missing model path: $modelPath"
            }
        }
    }
    return [pscustomobject]@{ Entries = $blocks.Count; BuildingDataReferences = $referenceCount }
}

Require-File 'ProjectSettings/ProjectVersion.txt'
Require-File 'Packages/manifest.json'
Require-File 'Packages/packages-lock.json'
Require-File 'ProjectSettings/EditorBuildSettings.asset'
Require-File 'Tools/Validate-Project.ps1'

$versionText = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
if ($versionText -notmatch 'm_EditorVersion:\s*6000\.6\.3f1\b') {
    throw 'ProjectVersion.txt does not target the supported Unity editor 6000.6.3f1.'
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Packages/manifest.json') | ConvertFrom-Json
$lock = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'Packages/packages-lock.json') | ConvertFrom-Json
foreach ($package in $manifest.dependencies.PSObject.Properties) {
    $locked = $lock.dependencies.PSObject.Properties[$package.Name]
    if ($null -eq $locked) { throw "Manifest package is absent from packages-lock.json: $($package.Name)" }
    if ([string]$locked.Value.version -ne [string]$package.Value) {
        throw "Package lock mismatch for $($package.Name): manifest=$($package.Value), lock=$($locked.Value.version)"
    }
}

$buildText = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'ProjectSettings/EditorBuildSettings.asset')
$guidIndex = Get-AssetGuidIndex
$sceneRecords = [regex]::Matches($buildText, '(?ms)^\s*- enabled:\s*(\d+)\s*\r?\n\s+path:\s*(Assets/[^\r\n]+)\s*\r?\n\s+guid:\s*([0-9a-f]{32})')
if (!$sceneRecords -or $sceneRecords.Count -eq 0) { throw 'EditorBuildSettings.asset contains no build scenes.' }
$scenePaths = @()
$sceneGuids = @{}
foreach ($record in $sceneRecords) {
    $enabled = [int]$record.Groups[1].Value
    $scenePath = $record.Groups[2].Value.Trim()
    $sceneGuid = $record.Groups[3].Value
    if ($enabled -ne 1) { throw "Build scene is disabled: $scenePath" }
    if ($sceneGuids.ContainsKey($sceneGuid)) { throw "Duplicate build scene GUID: $sceneGuid" }
    $sceneGuids[$sceneGuid] = $true
    Require-File $scenePath
    if (!$guidIndex.ContainsKey($sceneGuid) -or $guidIndex[$sceneGuid] -ne $scenePath) {
        throw "Build scene GUID/path mismatch: $scenePath ($sceneGuid)"
    }
    $scenePaths += $scenePath
}

$scriptMetaCount = Validate-ScriptMetaFiles
$catalogValidation = Validate-BuildingContentCatalog $guidIndex

$packageCount = @($manifest.dependencies.PSObject.Properties).Count
Write-Host ("Preflight passed: Unity 6000.6.3f1; {0} locked packages; {1} build scenes; {2} script metadata files; {3} catalog entries ({4} BuildingData references)." -f $packageCount, @($scenePaths).Count, $scriptMetaCount, $catalogValidation.Entries, $catalogValidation.BuildingDataReferences)

if ($WithUnity) {
    if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity executable not found: $UnityPath" }
    $arguments = @('-File', (Join-Path $projectRoot 'Tools/Validate-Project.ps1'), '-UnityPath', $UnityPath)
    if ($Migrate) { $arguments += '-Migrate' }
    if ($Build) { $arguments += '-Build' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass @arguments
    if ($LASTEXITCODE -ne 0) { throw "Unity validation failed with exit code $LASTEXITCODE." }
}

Write-Host 'Release validation complete.'
