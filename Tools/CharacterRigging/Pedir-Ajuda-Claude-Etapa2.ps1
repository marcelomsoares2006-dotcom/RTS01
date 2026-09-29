[CmdletBinding()]
param([switch]$Copiar, [switch]$Visualizar)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$promptPath = Join-Path $projectRoot 'Docs\CLAUDE_ETAPA_2_CONCLUIR.md'
$taskPrompt = Get-Content -LiteralPath $promptPath -Raw -Encoding UTF8
if ($Visualizar) { Write-Output $taskPrompt; return }
$claudeCommand = Get-Command -Name 'claude.exe','claude.cmd','claude' -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($Copiar -or -not $claudeCommand) {
    Set-Clipboard -Value $taskPrompt
    Write-Host 'Pedido copiado. Cole no Claude Code aberto na pasta deste projeto.'
    Write-Host $projectRoot
    return
}
Push-Location -LiteralPath $projectRoot
try {
    & $claudeCommand.Source 'Leia Docs/CLAUDE_ETAPA_2_CONCLUIR.md, execute somente o escopo autorizado e registre os resultados. Pare antes da etapa 3. Respeite o bloqueio de execucao elevada documentado.'
    if ($LASTEXITCODE -ne 0) { throw "Claude Code encerrou com codigo $LASTEXITCODE. Pedido salvo em $promptPath" }
}
finally { Pop-Location }
