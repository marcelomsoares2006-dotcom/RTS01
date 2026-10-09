<#
Executa o Unity em batch neste projeto com FILA: so uma execucao por vez (mutex global), porque o Unity nao abre
o mesmo projeto duas vezes. Varios agentes/sessoes podem chamar este script ao mesmo tempo; os demais esperam.
Nao encerra processos de ninguem: se um Unity ja estiver aberto neste projeto (editor interativo ou outra sessao
fora da fila), espera ate -WaitMinutes e desiste com erro.

Uso:
  Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name catapult-build -UnityArgs '-quit -executeMethod CatapultSetup.BatchBuild'
  Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name adv-tests -UnityArgs '-runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults TestResults\adv-tests.xml'
Log: TestResults\<Name>.log. Saida: linhas "error CS", excecoes e o codigo de saida.
Se o log mostrar erros de compilacao em arquivos de OUTRA unidade, ela esta no meio de uma edicao: aguarde alguns
minutos e repita; nao edite arquivos de outra unidade.
#>
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$UnityArgs,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [int]$TimeoutSeconds = 1500,
    [int]$WaitMinutes = 45
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
New-Item -ItemType Directory -Force -Path (Join-Path $project 'TestResults') | Out-Null
$log = Join-Path $project "TestResults\$Name.log"

$mutex = New-Object System.Threading.Mutex($false, 'Global\EraImperialUnityBatchQueue')
$deadline = (Get-Date).AddMinutes($WaitMinutes)
$acquired = $false
try {
    while (-not $acquired) {
        if ((Get-Date) -gt $deadline) { throw "Fila do Unity: esperou mais de $WaitMinutes min." }
        try { $acquired = $mutex.WaitOne(15000) } catch [System.Threading.AbandonedMutexException] { $acquired = $true }
        if ($acquired) {
            $open = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
                Where-Object { $_.CommandLine -like "*$project*" }
            if ($open) {
                $mutex.ReleaseMutex(); $acquired = $false
                Write-Host "Unity ja aberto neste projeto (PID $($open.ProcessId)); aguardando..."
                Start-Sleep -Seconds 20
            }
        }
    }
    $arguments = '-batchmode -acceptSoftwareTermsForThisRunOnly -projectPath "' + $project + '" -logFile "' + $log + '" ' + $UnityArgs
    $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id   # somente o processo criado aqui
        throw "$Name excedeu $TimeoutSeconds s. Veja $log"
    }
    $code = $process.ExitCode
    Select-String -Path $log -Pattern 'error CS\d+|Exception|Scripts have compiler errors|Aborting batchmode' -ErrorAction SilentlyContinue |
        Select-Object -First 25 | ForEach-Object { $_.Line.Trim() }
    Write-Host "UNITY_EXIT_CODE=$code (log: $log)"
    exit $code
}
finally {
    if ($acquired) { try { $mutex.ReleaseMutex() } catch {} }
}
