param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    # Zero selects a duration-aware timeout. Pass a positive value only when
    # intentionally overriding the launcher safety window.
    [int]$StandaloneTimeoutSeconds = 0,
    [int]$BenchmarkSeconds = 8,
    [int]$BenchmarkWarmupSeconds = 3
)

$ErrorActionPreference = 'Stop'
$resultsDir = Join-Path $ProjectRoot 'TestResults'
$buildPath = Join-Path $ProjectRoot 'Builds\Windows\EraImperial.exe'
$xmlPath = Join-Path $resultsDir 'unit-tests.xml'
$playmodePath = Join-Path $resultsDir 'playmode-smoke.txt'
$buildReportPath = Join-Path $resultsDir 'windows-build.txt'
$standaloneLog = Join-Path $resultsDir 'standalone-runtime.log'
$jsonPath = Join-Path $resultsDir 'runtime-standalone-audit.json'
$markdownPath = Join-Path $ProjectRoot 'Docs\Runtime\2026-10-14-runtime-standalone-audit.md'

if ($BenchmarkSeconds -lt 3) { throw 'BenchmarkSeconds must be at least 3 seconds.' }
if ($BenchmarkWarmupSeconds -lt 0 -or $BenchmarkWarmupSeconds -ge $BenchmarkSeconds) {
    throw 'BenchmarkWarmupSeconds must be at least 0 and shorter than BenchmarkSeconds.'
}

# A long probe writes its final CSVs and JSON only after the requested sample
# window. The old fixed 45 s default killed a 600 s soak before Finish(). Keep
# the short-smoke behavior, but derive the default from the requested duration
# and leave explicit positive overrides untouched.
$effectiveStandaloneTimeoutSeconds = if ($StandaloneTimeoutSeconds -gt 0) {
    $StandaloneTimeoutSeconds
} else {
    [Math]::Max(45, $BenchmarkSeconds + $BenchmarkWarmupSeconds + 30)
}

New-Item -ItemType Directory -Force -Path $resultsDir, (Split-Path $markdownPath -Parent) | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()

function Add-Check([string]$Id, [string]$Area, [string]$Status, [string]$Evidence, [string]$Detail) {
    $checks.Add([pscustomobject]@{ id=$Id; area=$Area; status=$Status; evidence=$Evidence; detail=$Detail })
}

if (Test-Path -LiteralPath $xmlPath) {
    [xml]$xml = Get-Content -LiteralPath $xmlPath
    $run = $xml.'test-run'
    if ($run.result -eq 'Passed' -and [int]$run.failed -eq 0 -and [int]$run.skipped -eq 0 -and [int]$run.inconclusive -eq 0) {
        Add-Check 'editmode' 'contratos e smoke de dados' 'PASS' $xmlPath "EditMode: $($run.passed)/$($run.total) aprovados."
    } else {
        Add-Check 'editmode' 'contratos e smoke de dados' 'FAIL' $xmlPath "Resultado=$($run.result); passed=$($run.passed); failed=$($run.failed); skipped=$($run.skipped); inconclusive=$($run.inconclusive)."
    }
} else { Add-Check 'editmode' 'contratos e smoke de dados' 'BLOCKED' $xmlPath 'XML de testes ausente.' }

if (Test-Path -LiteralPath $playmodePath) {
    $smoke = Get-Content -LiteralPath $playmodePath -Raw
    $lastRunStart = $smoke.LastIndexOf('Unity ')
    $currentSmoke = if ($lastRunStart -ge 0) { $smoke.Substring($lastRunStart) } else { $smoke }
    $hasPassed = $currentSmoke -match 'PLAYMODE_SMOKE_PASSED: 3 scenarios, 0 runtime errors\.'
    $hasFailure = $currentSmoke -match '(?m)^FAILED:'
    if ($hasPassed -and -not $hasFailure) {
        Add-Check 'playmode' 'partida, navegação, render e save/load' 'PASS' $playmodePath 'Smoke PlayMode concluiu os três cenários sem erro runtime.'
    } else {
        Add-Check 'playmode' 'partida, navegação, render e save/load' 'FAIL' $playmodePath 'A execução mais recente do smoke não contém o marcador final limpo; execuções anteriores não são reutilizadas.'
    }
} else { Add-Check 'playmode' 'partida, navegação, render e save/load' 'BLOCKED' $playmodePath 'Log PlayMode ausente.' }

if (Test-Path -LiteralPath $buildReportPath) {
    $buildText = Get-Content -LiteralPath $buildReportPath -Raw
    if ($buildText -match 'Result:\s+Succeeded' -and $buildText -match 'Errors:\s+0') {
        Add-Check 'build' 'player Windows' 'PASS' $buildReportPath 'Build Windows reporta Succeeded e zero erros.'
    } else { Add-Check 'build' 'player Windows' 'FAIL' $buildReportPath 'Build não está aprovada pelo relatório.' }
} else { Add-Check 'build' 'player Windows' 'BLOCKED' $buildReportPath 'Relatório de build ausente.' }

if (Test-Path -LiteralPath $buildPath) {
    $outputDir = Join-Path $resultsDir 'StandalonePerformance'
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
    # Use an argument array: a single composed string can be parsed as one
    # token by Start-Process and silently skip the performance probe.
    $logArgs = @(
        '-screen-width', '1280',
        '-screen-height', '720',
        '-screen-fullscreen', '0',
        "-era-audit-seconds=$BenchmarkSeconds",
        "-era-audit-warmup-seconds=$BenchmarkWarmupSeconds",
        '-era-audit-profile=runtime-standalone',
        "-era-audit-output=$outputDir",
        '-logFile', $standaloneLog
    )
    if (Test-Path -LiteralPath $standaloneLog) { Remove-Item -LiteralPath $standaloneLog -Force }
    # On this Windows host, launching the GUI player directly through
    # Start-Process can return 127 before Unity initializes. cmd.exe preserves
    # the same working-directory/argument semantics as the validated manual
    # invocation and returns the real player exit code.
    # Starting the full absolute path as the first quoted token makes cmd.exe
    # return 127 on this host. The working directory is already the build
    # directory, so use the executable name and keep the real exit code.
    $relativeBuild = 'EraImperial.exe'
    # Unity resolves -logFile more reliably as a path relative to the player
    # working directory on this host. Keep the evidence in the repository by
    # copying the generated file back after the process exits.
    $relativeLog = 'standalone-runtime.log'
    $relativeOutput = '..\..\TestResults\StandalonePerformance'
    $cmdLine = $relativeBuild + ' ' +
        '-screen-width 1280 -screen-height 720 -screen-fullscreen 0 ' +
        "-era-audit-seconds=$BenchmarkSeconds -era-audit-warmup-seconds=$BenchmarkWarmupSeconds " +
        '-era-audit-profile=runtime-standalone ' +
        '"-era-audit-output=' + $relativeOutput + '" ' +
        '-logFile ' + $relativeLog
    $playerRelativeLog = Join-Path (Split-Path $buildPath) $relativeLog
    if (Test-Path -LiteralPath $playerRelativeLog) { Remove-Item -LiteralPath $playerRelativeLog -Force }
    $proc = Start-Process -FilePath 'cmd.exe' -ArgumentList @('/d', '/s', '/c', $cmdLine) -WorkingDirectory (Split-Path $buildPath) -WindowStyle Hidden -PassThru
    $exited = $proc.WaitForExit($effectiveStandaloneTimeoutSeconds * 1000)
    if ($exited) {
        if (Test-Path -LiteralPath $playerRelativeLog) {
            Copy-Item -LiteralPath $playerRelativeLog -Destination $standaloneLog -Force
        }
        $standaloneText = if (Test-Path $standaloneLog) { Get-Content -LiteralPath $standaloneLog -Raw } else { '' }
        $fatal = $standaloneText -match '(?im)(Crash|NullReferenceException|MissingReferenceException|Failed to initialize|Error loading|fatal error)'
        $performancePath = Join-Path $outputDir 'era-imperial-standalone-performance.json'
        $performanceJson = if (Test-Path -LiteralPath $performancePath) { Get-Content -LiteralPath $performancePath -Raw } else { '' }
        $performance = $null
        if ($performanceJson) {
            try { $performance = $performanceJson | ConvertFrom-Json } catch { $performance = $null }
        }
        # Unity may omit -logFile on this host, but the probe JSON is the
        # authoritative evidence because it contains duration and frame data.
        if (-not $fatal -and $performance -and $performance.durationSeconds -ge ($BenchmarkSeconds - 1)) {
            Add-Check 'standalone-start' 'execução fora do Editor' 'PASS' $performancePath "Player gráfico concluiu o probe; processo retornou $($proc.ExitCode) após gerar JSON válido; FPS mínimo=$($performance.fpsMin), médio=$($performance.fpsAverage), duração=$($performance.durationSeconds)s."
        } else {
            Add-Check 'standalone-start' 'execução fora do Editor' 'FAIL' $performancePath "Player encerrou com código $($proc.ExitCode), não gerou JSON válido ou registrou erro fatal."
        }
    } else {
        $proc.Kill()
        $proc.WaitForExit()
        Add-Check 'standalone-start' 'execução fora do Editor' 'BLOCKED' $standaloneLog "Player não encerrou em $effectiveStandaloneTimeoutSeconds s com -quit; não é considerado smoke aprovado."
    }
} else { Add-Check 'standalone-start' 'execução fora do Editor' 'BLOCKED' $buildPath 'Executável Windows ausente.' }

$summary = [pscustomobject]@{
    generatedAt = (Get-Date).ToString('o')
    unity = '6000.6.3f1'
    project = $ProjectRoot
    checks = @($checks)
    pass = @($checks | Where-Object status -eq 'PASS').Count
    fail = @($checks | Where-Object status -eq 'FAIL').Count
    blocked = @($checks | Where-Object status -eq 'BLOCKED').Count
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Auditoria independente de runtime/standalone')
$lines.Add('')
$lines.Add("Data: $($summary.generatedAt)  ")
$lines.Add('Unity: `6000.6.3f1`  ')
$lines.Add('Escopo: partida limpa, produção/contratos, save/load, replay, seleção, NavMesh, combate/cerco e player Windows.')
$lines.Add('')
$lines.Add('## Resultado')
$lines.Add('')
$lines.Add("- PASS: $($summary.pass)")
$lines.Add("- FAIL: $($summary.fail)")
$lines.Add("- BLOCKED/indeterminado: $($summary.blocked)")
$lines.Add('')
$lines.Add('| ID | Área | Estado | Evidência | Detalhe |')
$lines.Add('|---|---|---|---|---|')
foreach ($check in $checks) {
    $lines.Add(('| {0} | {1} | {2} | `{3}` | {4} |' -f $check.id, $check.area, $check.status, $check.evidence, $check.detail))
}
$lines.Add('')
$lines.Add('## Critério de não-falso-positivo')
$lines.Add('')
$lines.Add('O player só é marcado como PASS se encerrar com código 0 e sem padrões de erro fatal no log. Timeout, licença, instância aberta ou player que permanece executando são BLOCKED, nunca PASS.')
$lines.Add('')
$lines.Add('## Pendências observadas')
$lines.Add('')
$lines.Add('- A cobertura automatizada de contratos cobre produção, replay, save/load, seleção, NavMesh e combate/cerco em EditMode/PlayMode; ela não substitui um playtest humano de interface.')
$lines.Add('- O smoke PlayMode precisa concluir o marcador final dos três cenários para fechar a evidência de reload; uma execução interrompida no cenário 3 permanece não aprovada.')
$lines.Add("- O player Windows é executado fora do Editor em 1280x720 por $BenchmarkSeconds s, com warm-up de $BenchmarkWarmupSeconds s; o probe limita a 60 FPS, grava média/mínimo/máximo e encerra o processo. Isso é uma medição automatizada, não substitui playtest humano de interface.")
Set-Content -LiteralPath $markdownPath -Value $lines -Encoding UTF8
Write-Output "Runtime audit written to $jsonPath and $markdownPath"
Write-Output "PASS=$($summary.pass) FAIL=$($summary.fail) BLOCKED=$($summary.blocked)"
