[CmdletBinding()]
param(
    [int]$WaitSeconds = 8
)

$ErrorActionPreference = 'Stop'
$clientPath = 'C:\Program Files\Unity Hub\UnityLicensingClient_V1\Unity.Licensing.Client.exe'

if (!(Test-Path -LiteralPath $clientPath)) {
    throw "Unity Licensing Client não encontrado: $clientPath"
}

$running = @(Get-Process -Name 'Unity.Licensing.Client' -ErrorAction SilentlyContinue)
foreach ($process in $running) {
    Stop-Process -Id $process.Id -Force
}

Start-Process -FilePath $clientPath -WorkingDirectory (Split-Path $clientPath -Parent) | Out-Null
Start-Sleep -Seconds ([Math]::Max(1, $WaitSeconds))

$restarted = @(Get-Process -Name 'Unity.Licensing.Client' -ErrorAction SilentlyContinue)
if ($restarted.Count -eq 0) {
    throw 'O Unity Licensing Client não voltou a iniciar.'
}

$restarted | Select-Object ProcessName, Id, Responding, StartTime
Write-Host 'Licensing Client reiniciado. Confirme a conta no Unity Hub antes de executar a validação batch.'
