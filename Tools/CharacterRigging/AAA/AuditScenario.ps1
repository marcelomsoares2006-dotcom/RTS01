[CmdletBinding()]
param(
    [string]$ProjectRoot = '',
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) { $ProjectRoot = (Resolve-Path (Join-Path $scriptRoot '..\..\..')).Path }
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $ProjectRoot 'Docs\AAA\ScenarioAudit' }
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Count-Matches([string]$Path, [string]$Pattern) {
    if (-not (Test-Path $Path)) { return 0 }
    return @(Select-String -LiteralPath $Path -Pattern $Pattern -AllMatches -ErrorAction SilentlyContinue).Count
}

$scenes = @('Map1','Map2') | ForEach-Object {
    $scene = Join-Path $ProjectRoot "Assets\Scenes\Maps\$_.unity"
    $terrain = Join-Path $ProjectRoot "Assets\Scenes\Maps\Terrain-$_.asset"
    $text = if (Test-Path $scene) { Get-Content -Raw -LiteralPath $scene } else { '' }
    [pscustomobject]@{
        map = $_
        sceneExists = Test-Path $scene
        sceneBytes = if (Test-Path $scene) { (Get-Item $scene).Length } else { 0 }
        terrainExists = Test-Path $terrain
        terrainBytes = if (Test-Path $terrain) { (Get-Item $terrain).Length } else { 0 }
        terrainComponents = ([regex]::Matches($text, 'm_TerrainData:')).Count
        lights = ([regex]::Matches($text, '--- !u!108 ')).Count
        reflectionProbes = ([regex]::Matches($text, '--- !u!215 ')).Count
        reflectionBounces = ([regex]::Matches($text, 'm_ReflectionBounces:')).Count
        bakedLightmapBlocks = ([regex]::Matches($text, 'm_LightmapEditorSettings:')).Count
        navMeshAsset = Test-Path (Join-Path $ProjectRoot "Assets\Scenes\Maps\$($_)\NavMesh-Terrain.asset")
        pinkKeywords = ([regex]::Matches($text, '(?i)magenta|pink|InternalErrorShader')).Count
    }
}

$materialRoot = Join-Path $ProjectRoot 'Assets\Resources\Materials'
$materials = @(Get-ChildItem -LiteralPath $materialRoot -Filter '*.mat' -Recurse -File -ErrorAction SilentlyContinue)
$shaderRows = foreach ($m in $materials) {
    $line = Select-String -LiteralPath $m.FullName -Pattern 'm_Shader:' -ErrorAction SilentlyContinue | Select-Object -First 1
    [pscustomobject]@{ material = $m.FullName.Substring($ProjectRoot.Length+1); shader = if ($line) { $line.Line.Trim() } else { 'MISSING' }; pinkKeyword = ((Get-Content -Raw $m.FullName) -match '(?i)magenta|pink|InternalErrorShader') }
}

$result = [ordered]@{
    generatedAt = (Get-Date).ToString('s')
    project = $ProjectRoot
    renderPipeline = 'Built-in RP (observed from project context; confirm in Unity)'
    maps = $scenes
    materials = $shaderRows
    checklist = [ordered]@{
        scale = 'PENDING_EDITOR: verify TerrainData size and scene units against character height.'
        uv = 'PENDING_EDITOR: inspect terrain layers and mesh UV channels.'
        normals = 'PENDING_EDITOR: inspect imported mesh normals/tangents and terrain normal maps.'
        lod = 'PENDING_EDITOR: quantify terrain/detail LOD by camera distance.'
        magenta = (($scenes | Measure-Object -Property pinkKeywords -Sum).Sum -eq 0 -and @($shaderRows | Where-Object pinkKeyword).Count -eq 0)
        lighting = 'WARNING: scene text has no configured m_Sun reference; validate lighting in Editor.'
        shaders = 'WARNING: materials reference built-in shader IDs; confirm compatibility after Unity import.'
    }
}

$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputRoot 'scenario-audit.json') -Encoding UTF8
Write-Output "Scenario audit written to $OutputRoot\scenario-audit.json"
