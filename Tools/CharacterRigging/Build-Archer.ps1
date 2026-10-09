<#
Reconstroi o Archer de ponta a ponta a partir de Tools/CharacterRigging/Inputs/Archer:
  1. BuildArcher.py            (Blender)  malha Meshy + rig, retarget dos clipes, LODs, FBX/GLB
  2. MeasureArcherTorso.py     (Blender)  perfil do torso para encaixar aljava/bandoleira
  3. BuildArcherAccessories.py (Blender)  arco, aljava e flecha como objetos separados
  4. ArcherSetup.BatchBuild    (Unity)    avatar humanoide, clipes, controller, prefabs, CharacterData
Feche o Unity antes de rodar. Uso: Tools\CharacterRigging\Build-Archer.ps1 [-SkipUnity]
#>
param([switch]$SkipUnity)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe'
Push-Location $root
try {
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildArcher.py
    if ($LASTEXITCODE -ne 0) { throw "BuildArcher.py falhou ($LASTEXITCODE)" }
    & $blender -b Tools\CharacterRigging\Working\Archer\Archer.blend --python-exit-code 1 --python Tools\CharacterRigging\MeasureArcherTorso.py
    if ($LASTEXITCODE -ne 0) { throw "MeasureArcherTorso.py falhou ($LASTEXITCODE)" }
    & $blender -b --factory-startup --python-exit-code 1 --python Tools\CharacterRigging\BuildArcherAccessories.py
    if ($LASTEXITCODE -ne 0) { throw "BuildArcherAccessories.py falhou ($LASTEXITCODE)" }
    if (-not $SkipUnity) {
        New-Item -ItemType Directory -Force TestResults | Out-Null
        & $unity -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath $root -logFile TestResults\archer-build.log -executeMethod ArcherSetup.BatchBuild
        if ($LASTEXITCODE -ne 0) { throw "ArcherSetup.BatchBuild falhou ($LASTEXITCODE); veja TestResults\archer-build.log" }
    }
    Write-Host 'ARCHER_BUILD_OK'
}
finally { Pop-Location }
