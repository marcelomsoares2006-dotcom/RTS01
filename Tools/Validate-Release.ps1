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
$scenePaths = [regex]::Matches($buildText, 'path:\s*(Assets/[^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Trim() }
if (!$scenePaths -or $scenePaths.Count -eq 0) { throw 'EditorBuildSettings.asset contains no build scenes.' }
foreach ($scenePath in $scenePaths) { Require-File $scenePath }

$packageCount = @($manifest.dependencies.PSObject.Properties).Count
Write-Host ("Preflight passed: Unity 6000.6.3f1; {0} locked packages; {1} build scenes." -f $packageCount, @($scenePaths).Count)

if ($WithUnity) {
    if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) { throw "Unity executable not found: $UnityPath" }
    $arguments = @('-File', (Join-Path $projectRoot 'Tools/Validate-Project.ps1'), '-UnityPath', $UnityPath)
    if ($Migrate) { $arguments += '-Migrate' }
    if ($Build) { $arguments += '-Build' }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass @arguments
    if ($LASTEXITCODE -ne 0) { throw "Unity validation failed with exit code $LASTEXITCODE." }
}

Write-Host 'Release validation complete.'
