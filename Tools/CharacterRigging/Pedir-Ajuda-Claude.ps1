[CmdletBinding()]
param(
    [switch]$Copiar,
    [switch]$Visualizar
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$promptPath = Join-Path $projectRoot 'Docs\CLAUDE_ETAPA_1_HERALDIC_KNIGHT.md'
if (-not (Test-Path -LiteralPath $promptPath)) {
    throw "Pedido nao encontrado: $promptPath"
}
$taskPrompt = Get-Content -LiteralPath $promptPath -Raw -Encoding UTF8

if ($Visualizar) {
    Write-Output $taskPrompt
    return
}

$claudeCommand = Get-Command -Name 'claude.exe','claude.cmd','claude' -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($Copiar -or -not $claudeCommand) {
    Set-Clipboard -Value $taskPrompt
    Write-Host 'Pedido completo copiado. Cole na sessao local do Claude Code deste projeto.'
    Write-Host "Pasta: $projectRoot"
    if (-not $claudeCommand) {
        Write-Host 'O comando claude nao foi localizado no PATH; nenhuma instalacao foi iniciada.'
    }
    return
}

# CLI interativa com pedido inicial: https://code.claude.com/docs/en/cli-reference
# O pedido longo fica em arquivo para evitar limites/escaping na linha de comando.
Push-Location -LiteralPath $projectRoot
try {
    & $claudeCommand.Source 'Leia Docs/CLAUDE_ETAPA_1_HERALDIC_KNIGHT.md e execute somente a etapa 1 descrita. Registre os resultados e pare antes da etapa 2.'
    if ($LASTEXITCODE -ne 0) {
        throw "Claude Code encerrou com codigo $LASTEXITCODE. O pedido permanece salvo em $promptPath"
    }
}
finally {
    Pop-Location
}
