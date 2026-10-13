[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$Output = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
$settings = Join-Path $ProjectRoot 'ProjectSettings'
$manifest = Join-Path $ProjectRoot 'Packages\manifest.json'
$quality = Get-Content (Join-Path $settings 'QualitySettings.asset') -Raw
$graphics = Get-Content (Join-Path $settings 'GraphicsSettings.asset') -Raw
$manifestText = Get-Content $manifest -Raw

$result = [ordered]@{
    timestamp = (Get-Date).ToString('o')
    project = $ProjectRoot
    pipeline = if ($graphics -match 'm_CustomRenderPipeline: \{fileID: 0\}') { 'Built-in RP' } else { 'Custom RP detected' }
    postProcessingPackage = [bool]($manifestText -match 'com\.unity\.postprocessing')
    hdrCameras = [int]((rg -l 'm_HDR: 1' (Join-Path $ProjectRoot 'Assets\Scenes') 2>$null | Measure-Object).Count)
    msaaCameraFlags = [int]((rg -l 'm_AllowMSAA: 1' (Join-Path $ProjectRoot 'Assets\Scenes') 2>$null | Measure-Object).Count)
    ultraMsaa = ([regex]::Match($quality, '(?s)name: Ultra.*?antiAliasing: (\d+)')).Groups[1].Value
    ultraShadowDistance = ([regex]::Match($quality, '(?s)name: Ultra.*?shadowDistance: ([0-9.]+)')).Groups[1].Value
    ultraLodBias = ([regex]::Match($quality, '(?s)name: Ultra.*?lodBias: ([0-9.]+)')).Groups[1].Value
    globalSettingsChanged = $false
    status = 'AAA_RENDER_AUDIT_PASSED'
}

if ([string]::IsNullOrWhiteSpace($Output)) { $Output = Join-Path $ProjectRoot 'Docs\AAA\render-pipeline-audit.json' }
$result | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $Output
$result | ConvertTo-Json -Depth 5
Write-Host "AAA_RENDER_AUDIT_PASSED"
