[CmdletBinding()]
param(
    [ValidateSet(2560,3840)][int]$Width = 2560,
    [ValidateSet(1440,2160)][int]$Height = 1440,
    [string]$OutputDirectory = '',
    [int]$Frames = 180,
    [int]$TargetFps = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'TestResults\AAA' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$manifest = [ordered]@{
    width = $Width
    height = $Height
    frames = $Frames
    targetFps = $TargetFps
    output = (Join-Path $OutputDirectory "AAA-$Width`x$Height-$stamp.png")
    note = 'A captura real é executada pelo método Unity AAARenderPipelineAudit.Capture; este script prepara o contrato e não altera o pipeline global.'
}
$manifest | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $OutputDirectory "AAA-$stamp.json")
Write-Host ($manifest | ConvertTo-Json)
Write-Host 'Use -executeMethod AAARenderPipelineAudit.Capture no Unity batch ou a janela de captura dedicada.'
