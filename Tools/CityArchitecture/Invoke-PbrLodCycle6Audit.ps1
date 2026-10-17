param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)
$ErrorActionPreference = 'Stop'
$checks = @()
$blocked = @()
function Add-Check([string]$Name, [bool]$Ok) {
    $script:checks += "${Name}: " + ($(if ($Ok) { 'PASS' } else { 'FAIL' }))
    if (-not $Ok) { $script:blocked += $Name }
}
$house = Join-Path $ProjectRoot 'Assets\Resources\Prefabs\Units\Buildings\House.prefab'
$pilot = Join-Path $ProjectRoot 'Assets\Editor\CityArchitecture\HousePbrPilotAuthoring.cs'
$lodAudit = Join-Path $ProjectRoot 'Tools\CityArchitecture\AuditBuildingLODs.py'
Add-Check 'House.prefab presente' (Test-Path $house)
Add-Check 'piloto PBR isolado presente' (Test-Path $pilot)
Add-Check 'auditoria LOD presente' (Test-Path $lodAudit)
$unityLog = Get-ChildItem $env:TEMP -Filter 'EraImperial*Pbr*' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $unityLog) { $blocked += 'captura PBR/LOD dentro do Unity não executada' }
$report = Join-Path $ProjectRoot 'Docs\QA\2026-10-17-pbr-lod-cycle6.md'
$status = if ($blocked.Count -eq 0) { 'PASS' } else { 'BLOCKED' }
@("# Ciclo 6 — revisão Unity PBR/LOD", "", "Status: **$status**", "", "Auditoria offline/isolada; nenhum prefab, cena ou asset de produção foi alterado.", "", '## Checks', '', $(($checks | ForEach-Object { "- $_" }) -join "`n"), '', '## Bloqueios', '', $(($blocked | ForEach-Object { "- $_" }) -join "`n"), '') | Set-Content -Encoding UTF8 $report
Write-Output "PBR_LOD_CYCLE6_$status"
if ($blocked.Count -gt 0) { exit 2 }
exit 0
