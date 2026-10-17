[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$ReportPath = ""
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $ProjectRoot 'Docs\QA\2026-10-17-performance-integration-cycle5.md'
}

$runtimePath = Join-Path $ProjectRoot 'TestResults\runtime-standalone-audit.json'
$cyclePaths = @(
    (Join-Path $ProjectRoot 'Docs\QA\2026-10-17-construction-prototype-cycle1.md'),
    (Join-Path $ProjectRoot 'Docs\QA\2026-10-17-production-economy-cycle2.md'),
    (Join-Path $ProjectRoot 'Docs\QA\2026-10-17-era-progression-cycle3.md'),
    (Join-Path $ProjectRoot 'Docs\QA\2026-10-17-combat-siege-cycle4.md')
)

function Get-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Arquivo obrigatorio nao encontrado: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw
}

function Get-CycleStatus([string]$Text) {
    # BLOCKED tem precedencia: os ciclos podem conter marcadores de contratos
    # aprovados, mas continuam bloqueados sem PlayMode real.
    if ($Text -match '(?im)\bBLOCKED\b|_BLOCKED\b|STATUS[^\r\n]*bloquead|resultado[^\r\n]*bloquead|codigo\s+2') {
        return 'BLOCKED'
    }
    if ($Text -match '(?im)\bREVIEW\b|revisao pendente|em revisao') {
        return 'REVIEW'
    }
    if ($Text -match '(?im)\bPASS\b|aprovad|concluido') {
        return 'PASS'
    }
    return 'REVIEW'
}

function Has-RealPlayMode([string]$Text) {
    # Evidencia valida precisa indicar execucao real e aprovacao; mencoes a
    # depende, nao houve ou bloqueio de licenciamento invalidam o gate.
    $hasReal = $Text -match '(?im)playmode\s+real[^\r\n]*(execut|pass|aprov)|execucao\s+playmode[^\r\n]*(real|aprov)|marker[^\r\n]*playmode[^\r\n]*pass'
    $negated = $Text -match '(?im)nao\s+(?:houve|foi|foi\s+comprovad|aguardei)|playmode[^\r\n]*(?:blocked|bloquead|depende)|licenca|licenseclient|sem\s+execucao\s+playmode|execucao\s+playmode\s+real[^\r\n]*nao'
    return ($hasReal -and -not $negated)
}

$runtime = Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json
$runtimeChecks = @($runtime.checks)
$runtimeFailCount = 0
foreach ($check in $runtimeChecks) {
    if ([string]$check.status -eq 'FAIL') { $runtimeFailCount++ }
}

$fpsAverage = 0.0
$fpsMinimum = 0.0
$fpsMatch = [regex]::Match([string]$runtimeChecks[-1].detail, 'FPS:\s*\{[^}]*"fpsMin":(?<min>[0-9.]+),"fpsAverage":(?<avg>[0-9.]+)')
if (-not $fpsMatch.Success) {
    $fpsMatch = [regex]::Match(($runtime | ConvertTo-Json -Depth 10), '"fpsMin"\s*:\s*(?<min>[0-9.]+).*?"fpsAverage"\s*:\s*(?<avg>[0-9.]+)')
}
if ($fpsMatch.Success) {
    $fpsMinimum = [double]$fpsMatch.Groups['min'].Value
    $fpsAverage = [double]$fpsMatch.Groups['avg'].Value
}

$cycleResults = @()
foreach ($cyclePath in $cyclePaths) {
    $cycleText = Get-Text $cyclePath
    $cycleName = [IO.Path]::GetFileNameWithoutExtension($cyclePath)
    $status = Get-CycleStatus $cycleText
    $realPlayMode = Has-RealPlayMode $cycleText
    $cycleResults += [pscustomobject]@{
        Name = $cycleName
        Status = $status
        RealPlayMode = $realPlayMode
        Path = $cyclePath
    }
}

$blockedReasons = New-Object System.Collections.Generic.List[string]
if ($fpsAverage -lt 55) { [void]$blockedReasons.Add(('FPS medio abaixo de 55: {0:N2}' -f $fpsAverage)) }
if ($fpsMinimum -lt 30) { [void]$blockedReasons.Add(('FPS minimo abaixo de 30: {0:N2}' -f $fpsMinimum)) }
if ($runtimeFailCount -gt 0 -or [int]$runtime.fail -gt 0) { [void]$blockedReasons.Add(('runtime-standalone-audit possui FAIL: {0}' -f $runtimeFailCount)) }
foreach ($cycle in $cycleResults) {
    if (-not $cycle.RealPlayMode) {
        [void]$blockedReasons.Add(('{0} nao possui PlayMode real aprovado' -f $cycle.Name))
    }
}

$overallStatus = if ($blockedReasons.Count -gt 0) { 'BLOCKED' } elseif (($cycleResults | Where-Object Status -eq 'REVIEW').Count -gt 0) { 'REVIEW' } else { 'PASS' }
$timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'
$lines = New-Object System.Collections.Generic.List[string]
[void]$lines.Add('# Ciclo 5 - orcamento de performance e integracao')
[void]$lines.Add('')
[void]$lines.Add("Data da execucao: $timestamp")
[void]$lines.Add('Escopo: gate isolado, somente leitura, sem alteracao de gameplay, prefabs, cenas ou Git.')
[void]$lines.Add('')
[void]$lines.Add("## Resultado: **$overallStatus**")
[void]$lines.Add('')
[void]$lines.Add('O gate exige simultaneamente: media FPS >= 55, FPS minimo >= 30, zero FAIL no runtime e PlayMode real aprovado nos ciclos 1-4.')
[void]$lines.Add('')
[void]$lines.Add('## Runtime standalone')
[void]$lines.Add('')
[void]$lines.Add('| Metrica | Valor | Criterio | Estado |')
[void]$lines.Add('|---|---:|---:|---|')
$avgStatus = if ($fpsAverage -ge 55) { 'PASS' } else { 'BLOCKED' }
$minStatus = if ($fpsMinimum -ge 30) { 'PASS' } else { 'BLOCKED' }
$failStatus = if ($runtimeFailCount -eq 0 -and [int]$runtime.fail -eq 0) { 'PASS' } else { 'BLOCKED' }
[void]$lines.Add(('| FPS medio | {0:N2} | >= 55 | {1} |' -f $fpsAverage, $avgStatus))
[void]$lines.Add(('| FPS minimo | {0:N2} | >= 30 | {1} |' -f $fpsMinimum, $minStatus))
[void]$lines.Add(('| FAILs | {0} | 0 | {1} |' -f $runtimeFailCount, $failStatus))
[void]$lines.Add('')
[void]$lines.Add('Evidencia: `TestResults/runtime-standalone-audit.json`.')
[void]$lines.Add('')
[void]$lines.Add('## Ciclos 1-4')
[void]$lines.Add('')
[void]$lines.Add('| Ciclo | Classificacao | PlayMode real aprovado |')
[void]$lines.Add('|---|---|---|')
foreach ($cycle in $cycleResults) {
    [void]$lines.Add(('| {0} | **{1}** | {2} |' -f $cycle.Name, $cycle.Status, $(if ($cycle.RealPlayMode) { 'PASS' } else { 'BLOCKED' })))
}
[void]$lines.Add('')
[void]$lines.Add('A classificacao `BLOCKED` tem precedencia sobre marcadores parciais de contratos aprovados. Os ciclos so podem entrar como PASS quando houver evidencia de PlayMode real aprovado.')
[void]$lines.Add('')
[void]$lines.Add('## Motivos de bloqueio')
[void]$lines.Add('')
if ($blockedReasons.Count -eq 0) {
    [void]$lines.Add('- Nenhum.')
} else {
    foreach ($reason in $blockedReasons) { [void]$lines.Add("- $reason") }
}
[void]$lines.Add('')
[void]$lines.Add('## Conclusao')
[void]$lines.Add('')
if ($overallStatus -eq 'PASS') {
    [void]$lines.Add('O orcamento e a integracao estao aprovados por este gate. Isso nao substitui a revisao visual LOD/PBR nem a aceitacao artistica.')
} else {
    [void]$lines.Add('O ciclo 5 nao esta aprovado. O relatorio separa os resultados tecnicos dos bloqueios de integracao e nao promove assets ou gameplay.')
}
[void]$lines.Add('')
[void]$lines.Add('Arquivos alterados nesta etapa: somente este script e este relatorio.')

$reportDirectory = Split-Path -Parent $ReportPath
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
Set-Content -LiteralPath $ReportPath -Value ($lines -join [Environment]::NewLine) -Encoding UTF8

Write-Output ($lines -join [Environment]::NewLine)
if ($overallStatus -eq 'BLOCKED') { exit 2 }
if ($overallStatus -eq 'REVIEW') { exit 3 }
exit 0
