<#
Reconstroi o CombatMage (Mago de combate) de ponta a ponta a partir de Tools/CharacterRigging/Inputs/CombatMage:
  1. BuildCombatMage.py            (Blender)  malha Meshy + rig, retarget Mixamo (Idle/Hit/Death), Cast procedural, LODs, FBX/GLB
  2. BuildCombatMageAccessories.py (Blender)  cajado e orbe como objetos separados (procedural)
  3. CombatMageSetup.BatchBuild    (Unity)    avatar humanoide, clipes, controller, efeitos, prefabs, vinculo ao contrato, CharacterData
Unity: usa a fila do projeto (Tools\AdvancedUnits\Invoke-UnityBatch.ps1), que nunca fecha um Unity aberto.
Uso: Tools\CharacterRigging\Build-CombatMage.ps1 [-SkipUnity] [-Validate]
  -Validate roda tambem CombatMageAudit.Run e CombatMagePlayModeCheck.Run.
#>
param([switch]$SkipUnity, [switch]$Validate)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$queue = Join-Path $root 'Tools\AdvancedUnits\Invoke-UnityBatch.ps1'
Push-Location $root
try {
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildCombatMage.py
    if ($LASTEXITCODE -ne 0) { throw "BuildCombatMage.py falhou ($LASTEXITCODE)" }
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildCombatMageAccessories.py
    if ($LASTEXITCODE -ne 0) { throw "BuildCombatMageAccessories.py falhou ($LASTEXITCODE)" }
    if (-not $SkipUnity) {
        & powershell -NoProfile -File $queue -Name combatmage-build -UnityArgs '-quit -executeMethod CombatMageSetup.BatchBuild'
        if ($LASTEXITCODE -ne 0) { throw "CombatMageSetup.BatchBuild falhou ($LASTEXITCODE); veja TestResults\combatmage-build.log" }
        if ($Validate) {
            & powershell -NoProfile -File $queue -Name combatmage-audit -UnityArgs '-quit -executeMethod CombatMageAudit.Run'
            if ($LASTEXITCODE -ne 0) { throw "CombatMageAudit.Run falhou ($LASTEXITCODE); veja Docs\CharacterMeshAudit\CombatMage\unity-audit.txt" }
            & powershell -NoProfile -File $queue -Name combatmage-playmode -UnityArgs '-executeMethod CombatMagePlayModeCheck.Run'
            if ($LASTEXITCODE -ne 0) { throw "CombatMagePlayModeCheck.Run falhou ($LASTEXITCODE); veja TestResults\combatmage-playmode.txt" }
        }
    }
    Write-Host 'COMBATMAGE_BUILD_OK'
}
finally { Pop-Location }
