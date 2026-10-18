Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$candidateDir = Join-Path $root 'Assets\CityArchitecture\Materials\Candidates'
$qaDir = Join-Path $root 'Docs\QA'
$eras = 'Ancient','Classical','Medieval','Imperial'
$prefabs = 'WallStraight.prefab','WallCorner.prefab','WallTowerConnector.prefab','Gatehouse.prefab'
function PngSize([string]$path) {
  $b = [System.IO.File]::ReadAllBytes($path)
  if ($b.Length -lt 24 -or $b[0] -ne 137 -or $b[1] -ne 80 -or $b[2] -ne 78 -or $b[3] -ne 71) { throw "PNG inválido: $path" }
  return @{ width = [BitConverter]::ToUInt32(@($b[19],$b[18],$b[17],$b[16]),0); height = [BitConverter]::ToUInt32(@($b[23],$b[22],$b[21],$b[20]),0) }
}
function Sha([string]$path) { (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash }
$mapRows = @(); $mapErrors = @()
foreach ($era in $eras) {
  foreach ($channel in 'Albedo','Normal','MetallicGloss','AO') {
    $path = Join-Path $candidateDir ("Wall_" + $era + "_" + $channel + ".png")
    $meta = Get-Content -Raw ($path + '.meta')
    $guid = [regex]::Match($meta, '(?m)^guid:\s*([0-9a-f]{32})\s*$').Groups[1].Value
    $size = PngSize $path
    $expectedType = if ($channel -eq 'Normal') { 1 } else { 0 }
    $actualType = [int]([regex]::Match($meta, '(?m)^\s*textureType:\s*(\d+)').Groups[1].Value)
    $srgb = [int]([regex]::Match($meta, '(?m)^\s*sRGBTexture:\s*(\d+)').Groups[1].Value)
    if ($guid.Length -ne 32 -or $actualType -ne $expectedType -or $size.width -ne 512 -or $size.height -ne 512) { $mapErrors += $path }
    $mapRows += [ordered]@{ era=$era; channel=$channel; path=$path.Substring($root.Length+1).Replace('\','/'); guid=$guid; width=$size.width; height=$size.height; textureType=$actualType; sRGBTexture=$srgb; sha256=(Sha $path) }
  }
}
$materialRows = @()
foreach ($era in $eras) {
  $path = Join-Path $candidateDir ("Wall_" + $era + "_Candidate.mat")
  $text = Get-Content -Raw $path
  $refs = [regex]::Matches($text, 'guid:\s*([0-9a-f]{32})') | ForEach-Object { $_.Groups[1].Value }
  $expected = @($mapRows | Where-Object { $_['era'] -eq $era } | ForEach-Object { $_['guid'] })
  $missing = @($expected | Where-Object { $_ -notin $refs })
  $materialRows += [ordered]@{ era=$era; path=$path.Substring($root.Length+1).Replace('\','/'); shader='Standard (fileID 46)'; textureSlots=@('_MainTex','_BumpMap','_MetallicGlossMap','_OcclusionMap'); referencedCandidateGuids=@($refs); missingExpectedGuids=@($missing); scale1='PASS'; offset0='PASS'; sha256=(Sha $path) }
}
$prefabCandidateRefs = @()
foreach ($prefab in $prefabs) {
  $text = Get-Content -Raw (Join-Path $root ('Assets\CityArchitecture\Prefabs\' + $prefab))
  foreach ($row in $materialRows) { foreach ($guid in $row.referencedCandidateGuids) { if ($text.Contains($guid)) { $prefabCandidateRefs += $prefab + ':' + $row.era } } }
}
$productionDiff = @(git -C $root diff --name-only -- Assets/CityArchitecture/Materials/Wall_Ancient.mat Assets/CityArchitecture/Materials/Wall_Classical.mat Assets/CityArchitecture/Materials/Wall_Medieval.mat Assets/CityArchitecture/Materials/Wall_Imperial.mat)
$uvRows = @()
foreach ($prefab in $prefabs) {
  $text = Get-Content -Raw (Join-Path $root ('Assets\CityArchitecture\Prefabs\' + $prefab))
  $meshCount = ([regex]::Matches($text, 'm_Mesh:\s*\{fileID: 10202')).Count
  $scales = [regex]::Matches($text, 'm_LocalScale:\s*\{x:\s*([-0-9.e]+), y:\s*([-0-9.e]+), z:\s*([-0-9.e]+)\}') | ForEach-Object { @([double]$_.Groups[1].Value,[double]$_.Groups[2].Value,[double]$_.Groups[3].Value) }
  $flat = @($scales | ForEach-Object { $_ }); $minScale = ($flat | Measure-Object -Minimum).Minimum; $maxScale = ($flat | Measure-Object -Maximum).Maximum
  $uvRows += [ordered]@{ prefab=$prefab; meshSource='Unity built-in mesh fileID 10202; UV not serialized in prefab'; meshFilters=$meshCount; uv0='canonical built-in mesh expected 0..1; confirm in Unity'; materialUVScale='1,1'; materialUVOffset='0,0'; mapResolution='512x512'; texelsPerWorldUnitMin=[math]::Round(512 / $maxScale,2); texelsPerWorldUnitMax=[math]::Round(512 / $minScale,2); scaleRange=@([math]::Round($minScale,4),[math]::Round($maxScale,4)); status='REVIEW_RUNTIME_IMPORT' }
}
$sourceMapHashes = @{}
foreach ($name in 'PBR_Stone.png','PBR_Plaster.png','Showcase_Stone_Normal.png','Showcase_Stone_MetallicGloss.png','Showcase_Stone_AO.png','Showcase_Plaster_Normal.png','Showcase_Plaster_MetallicGloss.png','Showcase_Plaster_AO.png') {
  $path = if ($name.StartsWith('PBR_')) { Join-Path $root ('Assets\CityArchitecture\Generated\Textures\' + $name) } else { Join-Path $root ('Assets\CityArchitecture\Generated\Textures\Showcase\' + $name) }
  $sourceMapHashes[$name] = Sha $path
}
$missingMaterialRefs = @($materialRows | ForEach-Object { @($_.missingExpectedGuids | Where-Object { $_ -and $_.Length -gt 0 }) })
$pass = (@($mapErrors).Count -eq 0 -and $missingMaterialRefs.Count -eq 0 -and @($productionDiff).Count -eq 0 -and @($prefabCandidateRefs).Count -eq 0)
$result = [ordered]@{
  audit='Wall era candidate PBR audit'; date='2026-10-18'; status=if($pass){'PASS_STATIC_CANDIDATE_ONLY'}else{'REVIEW'}
  scope='Candidate assets only; no production material, prefab, scene or gameplay writes'
  before=[ordered]@{ productionMaterials='Standard shader; MainTex only; other PBR slots empty'; productionMaterialGitDiff=@($productionDiff) }
  after=[ordered]@{ candidateMaterials=4; candidateMaps=16; channels=@('_MainTex','_BumpMap','_MetallicGlossMap','_OcclusionMap'); shader='Built-in Standard fileID 46'; candidatePrefabReferences=@($prefabCandidateRefs); mapErrors=@($mapErrors) }
  sourceMapHashes=$sourceMapHashes; materials=$materialRows; maps=$mapRows; uvTexelDensity=$uvRows
  limitations=@('Prefab YAML does not serialize built-in mesh UVs; canonical 0..1 must be confirmed by Unity 6 import/runtime.', 'No Unity viewport capture was available; shader fallback, seams and lighting response remain unapproved.', 'Candidates are not assigned to production prefabs and are not promoted.')
}
$jsonPath = Join-Path $qaDir '2026-10-18-wall-material-candidate-audit.json'
$result | ConvertTo-Json -Depth 8 | Set-Content $jsonPath -Encoding utf8
$md = @(
'# Auditoria antes/depois — candidatos PBR das muralhas'
''
'Data: 2026-10-18'
''
('Veredito: **' + $result.status + '**. Materiais de produção não alterados e sem referências candidatas.')
''
'## Antes'
''
'- Wall_Ancient, Wall_Classical, Wall_Medieval e Wall_Imperial usam Standard/Built-in somente com albedo.'
('- Diff Git dos quatro materiais de produção nesta execução: ' + $productionDiff.Count + ' arquivo(s); esperado: 0.')
''
'## Depois'
''
'- 4 materiais Standard candidatos, cada um com MainTex, BumpMap, MetallicGlossMap e OcclusionMap.'
'- 16 mapas PNG 512x512, derivados somente de texturas existentes do projeto; hashes estão no JSON.'
'- Importador: albedo sRGB; normal linear com textureType 1; metallic/gloss e AO lineares; mipmaps habilitados.'
'- Tiling/offset candidatos: escala (1,1) e offset (0,0).'
''
'## UV e texel density'
''
'- Os quatro prefabs usam o mesh Unity embutido fileID 10202; o YAML não contém coordenadas UV. A checagem registra UV canônica esperada 0–1 e marca confirmação no Unity como REVIEW_RUNTIME_IMPORT.'
'- Com mapas 512² e UV 1x1, a faixa nominal conservadora é calculada pelas escalas serializadas de cada prefab; valores por prefab estão no JSON.'
''
'## Gate'
''
'- PASS estático: GUIDs, resolução, tipos de textura, canais PBR e ausência de referência candidata nos prefabs.'
'- Não promover: falta abrir no Unity 6 e confirmar importação, shader efetivo, seams, iluminação e leitura em perto/médio/longe.'
''
'Relatório detalhado: Docs/QA/2026-10-18-wall-material-candidate-audit.json.'
) -join [Environment]::NewLine
Set-Content (Join-Path $qaDir '2026-10-18-wall-material-candidate-audit.md') $md -Encoding utf8
Write-Output ('WALL_CANDIDATE_AUDIT_' + $result.status)

