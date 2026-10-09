<#
Reconstroi o PaladinHealer de ponta a ponta a partir de Tools/CharacterRigging/Inputs/PaladinHealer:
  1. BuildPaladinHealer.py            (Blender)  malha Meshy + rig, retarget Mixamo, clipe Heal, LODs, FBX/GLB
  2. BuildPaladinHealerAccessories.py (Blender)  espada e escudo como objetos separados
  3. MountPaladinHealerAccessories.py (Blender)  encaixe das pecas nos ossos (mounts.json) + preview
  4. BuildPaladinHealerEffect.py      (Blender)  texturas procedurais do efeito de cura
  5. PaladinHealerSetup.BatchBuild    (Unity)    avatar humanoide, clipes, controller, prefabs, contrato, CharacterData
O Unity roda pela fila (Tools\AdvancedUnits\Invoke-UnityBatch.ps1). Uso: Tools\CharacterRigging\Build-PaladinHealer.ps1 [-SkipUnity]
#>
param([switch]$SkipUnity)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
Push-Location $root
try {
    function Run-Blender($args_) {
        & $blender @args_
        if ($LASTEXITCODE -ne 0) { throw "Blender falhou ($LASTEXITCODE): $($args_ -join ' ')" }
    }
    Run-Blender @('-b','--factory-startup','--python-exit-code','1','--python','Tools\CharacterRigging\BuildPaladinHealer.py')
    Run-Blender @('-b','--factory-startup','--python-exit-code','1','--python','Tools\CharacterRigging\BuildPaladinHealerAccessories.py')
    Run-Blender @('-b','Tools\CharacterRigging\Working\PaladinHealer\PaladinHealer.blend','--python-exit-code','1','--python','Tools\CharacterRigging\MountPaladinHealerAccessories.py')
    Run-Blender @('-b','--factory-startup','--python-exit-code','1','--python','Tools\CharacterRigging\BuildPaladinHealerEffect.py')
    if (-not $SkipUnity) {
        & powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name paladin-build -UnityArgs '-quit -executeMethod PaladinHealerSetup.BatchBuild'
        if ($LASTEXITCODE -ne 0) { throw "PaladinHealerSetup.BatchBuild falhou ($LASTEXITCODE); veja TestResults\paladin-build.log" }
    }
    Write-Host 'PALADINHEALER_BUILD_OK'
}
finally { Pop-Location }
