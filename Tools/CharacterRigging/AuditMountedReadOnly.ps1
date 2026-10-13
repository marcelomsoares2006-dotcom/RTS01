[CmdletBinding()]
param(
    [string]$ProjectRoot,
    [string]$Output = 'Docs\CharacterMeshAudit\Mounted\scan.json'
)

$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $PSCommandPath
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = Join-Path $scriptDir '..\..' }
$root = (Resolve-Path $ProjectRoot).Path
$outputPath = Join-Path $root $Output
$files = @(
    'Assets\CharacterRigging\Stage3\Horse\Models\Horse.fbx',
    'Assets\CharacterRigging\Prototypes\Horse_SkinnedPrototype.fbx',
    'Assets\CharacterRigging\Stage5\MountedFitPrototype.prefab',
    'Assets\CharacterRigging\Stage5\MountedReview.unity',
    'Assets\CharacterRigging\Stage5\MountedFitPrototypeNavMesh.unity',
    'Assets\CharacterRigging\Stage5\Mounted\ArmoredKnight_MountedIdle.fbx',
    'Assets\CharacterRigging\Stage5\Mounted\MountedIdle.controller',
    'Assets\Scripts\Units\Mounted\MountedSockets.cs',
    'Assets\Scripts\Units\Mounted\MountedIKController.cs',
    'Assets\Scripts\Units\Mounted\MountedMovementDriver.cs',
    'Assets\Scripts\Units\Mounted\MountedAnimatorSync.cs'
)
$entries = foreach ($relative in $files) {
    $path = Join-Path $root $relative
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    [pscustomobject]@{
        path = $relative
        exists = $exists
        bytes = if ($exists) { (Get-Item -LiteralPath $path).Length } else { 0 }
        sha256 = if ($exists) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash } else { $null }
    }
}
$prefab = Join-Path $root 'Assets\CharacterRigging\Stage5\MountedFitPrototype.prefab'
$prefabText = if (Test-Path $prefab) { Get-Content -Raw $prefab } else { '' }
$controller = Join-Path $root 'Assets\CharacterRigging\Stage5\Mounted\MountedIdle.controller'
$controllerText = if (Test-Path $controller) { Get-Content -Raw $controller } else { '' }
$report = [ordered]@{
    tool = 'AuditMountedReadOnly'
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    readOnly = $true
    files = $entries
    signals = [ordered]@{
        socketsComponent = $prefabText.Contains('Assembly-CSharp::MountedSockets')
        ikComponent = $prefabText.Contains('Assembly-CSharp::MountedIKController')
        movementComponent = $prefabText.Contains('Assembly-CSharp::MountedMovementDriver')
        movementDisabledInPrototype = $prefabText.Contains('m_Enabled: 0')
        animatorIkPass = $controllerText.Contains('m_IKPass: 1')
        navMeshDataMissingInReview = ((Get-Content -Raw (Join-Path $root 'Assets\CharacterRigging\Stage5\MountedReview.unity')) -match 'm_NavMeshData: \{fileID: 0\}')
    }
}
New-Item -ItemType Directory -Force (Split-Path $outputPath) | Out-Null
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $outputPath -Encoding UTF8
Write-Output "Mounted audit written: $outputPath"
