<#
Reconstroi a Balestra (balista de carreta) de ponta a ponta, sem insumos externos (tudo procedural):
  1. BuildCrossbow.py  (Blender)  malha, rig, 3 LODs, texturas, clipes, virote  -> Assets/CharacterRigging/Crossbow
  2. CrossbowSetup.BatchBuild (Unity, pela fila Tools\AdvancedUnits\Invoke-UnityBatch.ps1)
        materiais, controller, efeitos, projetil, prefabs, wrapper, vinculo ao contrato, registro condicionado
Uso: Tools\CharacterRigging\Build-Crossbow.ps1 [-SkipUnity] [-Validate]
  -Validate: depois do build roda CrossbowAudit.Run, CrossbowPlayModeCheck.Run e os testes NUnit da unidade.
#>
param([switch]$SkipUnity, [switch]$Validate)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$queue = Join-Path $root 'Tools\AdvancedUnits\Invoke-UnityBatch.ps1'
Push-Location $root
try {
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildCrossbow.py
    if ($LASTEXITCODE -ne 0) { throw "BuildCrossbow.py falhou ($LASTEXITCODE)" }
    if (-not $SkipUnity) {
        & powershell -NoProfile -ExecutionPolicy Bypass -File $queue -Name crossbow-build -UnityArgs '-quit -executeMethod CrossbowSetup.BatchBuild'
        if ($LASTEXITCODE -ne 0) { throw "CrossbowSetup.BatchBuild falhou ($LASTEXITCODE); veja TestResults\crossbow-build.log" }
        if ($Validate) {
            & powershell -NoProfile -ExecutionPolicy Bypass -File $queue -Name crossbow-audit -UnityArgs '-quit -executeMethod CrossbowAudit.Run'
            if ($LASTEXITCODE -ne 0) { throw "CrossbowAudit falhou; veja Docs\CharacterMeshAudit\Crossbow\unity-audit.txt" }
            & powershell -NoProfile -ExecutionPolicy Bypass -File $queue -Name crossbow-playmode -UnityArgs '-executeMethod CrossbowPlayModeCheck.Run'
            if ($LASTEXITCODE -ne 0) { throw "CrossbowPlayModeCheck falhou; veja TestResults\crossbow-playmode.txt" }
            & powershell -NoProfile -ExecutionPolicy Bypass -File $queue -Name crossbow-tests -UnityArgs '-runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults TestResults\crossbow-tests.xml'
        }
    }
    Write-Host 'CROSSBOW_BUILD_OK'
}
finally { Pop-Location }
