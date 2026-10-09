<#
Reconstroi a Catapulta de ponta a ponta (tudo procedural, sem terceiros):
  1. BuildCatapult.py        (Blender)  texturas, 3 LODs, esqueleto generico, 6 clipes, FBX
  2. BuildCatapultProps.py   (Blender)  pedra do projetil, fragmentos, texturas de particulas
  3. CatapultSetup.BatchBuild (Unity)   importacao, materiais, controller, prefabs, efeitos, wrapper, contrato, catalogo
O Unity roda pela fila do projeto (Tools\AdvancedUnits\Invoke-UnityBatch.ps1): so uma execucao por vez, nao encerra o Unity de ninguem.
Uso: Tools\CharacterRigging\Build-Catapult.ps1 [-SkipUnity] [-Validate]
  -Validate  tambem roda CatapultAudit.Run e CatapultPlayModeCheck.Run e os testes NUnit EditMode da unidade.
#>
param([switch]$SkipUnity, [switch]$Validate)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$queue = Join-Path $root 'Tools\AdvancedUnits\Invoke-UnityBatch.ps1'
Push-Location $root
try {
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildCatapult.py
    if ($LASTEXITCODE -ne 0) { throw "BuildCatapult.py falhou ($LASTEXITCODE)" }
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildCatapultProps.py
    if ($LASTEXITCODE -ne 0) { throw "BuildCatapultProps.py falhou ($LASTEXITCODE)" }
    if (-not $SkipUnity) {
        & powershell -NoProfile -File $queue -Name catapult-build -UnityArgs '-quit -executeMethod CatapultSetup.BatchBuild'
        if ($LASTEXITCODE -ne 0) { throw "CatapultSetup.BatchBuild falhou ($LASTEXITCODE); veja TestResults\catapult-build.log" }
        if ($Validate) {
            & powershell -NoProfile -File $queue -Name catapult-audit -UnityArgs '-quit -executeMethod CatapultAudit.Run'
            if ($LASTEXITCODE -ne 0) { throw "CatapultAudit.Run falhou ($LASTEXITCODE); veja Docs\CharacterMeshAudit\Catapult\unity-audit.txt" }
            & powershell -NoProfile -File $queue -Name catapult-tests -UnityArgs '-runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults TestResults\catapult-tests.xml'
            & powershell -NoProfile -File $queue -Name catapult-playmode -UnityArgs '-executeMethod CatapultPlayModeCheck.Run'
            if ($LASTEXITCODE -ne 0) { throw "CatapultPlayModeCheck.Run falhou ($LASTEXITCODE); veja Docs\CharacterMeshAudit\Catapult\playmode.txt" }
        }
    }
    Write-Host 'CATAPULT_BUILD_OK'
}
finally { Pop-Location }
