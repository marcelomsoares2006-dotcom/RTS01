[CmdletBinding()]
param([switch]$Copiar, [switch]$Visualizar)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$promptPath = Join-Path $projectRoot 'Docs\CLAUDE_ETAPA_3_CAVALO_CERVO.md'
$taskPrompt = Get-Content -LiteralPath $promptPath -Raw -Encoding UTF8
if ($Visualizar) { Write-Output $taskPrompt; return }
$claudeCommand = Get-Command -Name 'claude.exe','claude.cmd','claude' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($Copiar -or -not $claudeCommand) {
    Set-Clipboard -Value $taskPrompt
    Write-Host 'Pedido da etapa 3 copiado. Cole no Claude Code aberto nesta pasta:'
    Write-Host $projectRoot
    return
}
Push-Location -LiteralPath $projectRoot
try {
    & $claudeCommand.Source 'Leia Docs/CLAUDE_ETAPA_3_CAVALO_CERVO.md e execute somente a etapa 3 conforme as condicoes de inicio. Registre evidencias e pare antes da etapa 4.'
    if ($LASTEXITCODE -ne 0) { throw "Claude Code encerrou com codigo $LASTEXITCODE. Pedido salvo em $promptPath" }
}
finally { Pop-Location }
