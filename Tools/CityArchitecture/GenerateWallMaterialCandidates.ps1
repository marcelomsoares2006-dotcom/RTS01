Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$out = Join-Path $root 'Assets\CityArchitecture\Materials\Candidates'
New-Item -ItemType Directory -Force $out | Out-Null
$guids = @{
 Ancient_Albedo='9d774da4740b4f29b8665aace2900a4d'; Ancient_Normal='b90d85b3bcb94d3abe54530a98cba00a'; Ancient_MetallicGloss='b1f9b000099a4d2691f4fe54e1d9ea49'; Ancient_AO='ca7084f11bbf4b8d8a8202640a53d865'
 Classical_Albedo='f1c5ffe9ef6b434786919afcdd6e3689'; Classical_Normal='c10253c51fb54ca0a98644812e28288c'; Classical_MetallicGloss='f6e90563cda5491da8a6cf5bd32f4947'; Classical_AO='aefd17dd919c4fa390316530bb19055b'
 Medieval_Albedo='7b32f2aa14ad48598316a8bfc9b9eb32'; Medieval_Normal='90d7d8f005de4b528c1821ef38f7fdb7'; Medieval_MetallicGloss='b86209f966664948818ca90aea2b2369'; Medieval_AO='02833506104f4ce190d2857ee005f857'
 Imperial_Albedo='7a43f1d36bfd4b16ac880b82924b5a85'; Imperial_Normal='eba7af68723a42749ff427d59a2003aa'; Imperial_MetallicGloss='632cb4181bc04ed7a0290c77e244621d'; Imperial_AO='16adf8bb6eba4caaa454c7881cd25690'
 AncientMat='4986af28762c44f2affaa619f3394068'; ClassicalMat='595f4d1241954ecabd100fcc1e901232'; MedievalMat='d3bc69ebeeeb4f3d9ea34c2158b485b4'; ImperialMat='1dddcb53f11f478b880a24ee77cbccb5'
}
$profiles = [ordered]@{ Ancient='Stone'; Classical='Plaster'; Medieval='Stone'; Imperial='Plaster' }
$folderMeta = @"
fileFormatVersion: 2
guid: e9f71bb6531e493f8669b4c1b3c0dee3
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
Set-Content (Join-Path $out 'Candidates.meta') $folderMeta -Encoding utf8
function Write-TextureMeta([string]$name, [string]$key, [int]$textureType, [int]$srgb) {
 $text = @"
fileFormatVersion: 2
guid: $($guids[$key])
TextureImporter:
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: $srgb
    linearTexture: 0
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    wrapU: 0
    wrapV: 0
    wrapW: 0
  textureType: $textureType
  textureShape: 1
  spriteMode: 0
  platformSettings: []
"@
 Set-Content (Join-Path $out ($name + '.png.meta')) $text -Encoding utf8
}
function Write-Material([string]$era) {
 $name = 'Wall_' + $era + '_Candidate'
 $c = if ($era -eq 'Ancient') { '0.34,0.20,0.10' } elseif ($era -eq 'Classical') { '0.55,0.52,0.44' } elseif ($era -eq 'Medieval') { '0.28,0.30,0.32' } else { '0.17,0.20,0.23' }
 $smooth = if ($era -eq 'Imperial') { '0.32' } elseif ($era -eq 'Classical') { '0.28' } else { '0.20' }
 $albedo=$guids[($era + '_Albedo')]; $normal=$guids[($era + '_Normal')]; $metal=$guids[($era + '_MetallicGloss')]; $ao=$guids[($era + '_AO')]
 $mat = @"
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_Name: $name
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_Parent: {fileID: 0}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BumpMap:
        m_Texture: {fileID: 2800000, guid: $normal, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MainTex:
        m_Texture: {fileID: 2800000, guid: $albedo, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _MetallicGlossMap:
        m_Texture: {fileID: 2800000, guid: $metal, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    - _OcclusionMap:
        m_Texture: {fileID: 2800000, guid: $ao, type: 3}
        m_Scale: {x: 1, y: 1}
        m_Offset: {x: 0, y: 0}
    m_Ints: []
    m_Floats:
    - _BumpScale: 0.55
    - _GlossMapScale: 1
    - _Glossiness: $smooth
    - _GlossyReflections: 1
    - _Metallic: 0
    - _OcclusionStrength: 1
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _DstBlend: 0
    - _ZWrite: 1
    m_Colors:
    - _Color: {r: $($c.Split(',')[0]), g: $($c.Split(',')[1]), b: $($c.Split(',')[2]), a: 1}
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"@
 Set-Content (Join-Path $out ($name + '.mat')) $mat -Encoding utf8
 $meta = @(
  'fileFormatVersion: 2'
  ('guid: ' + $guids[($era + 'Mat')])
  'NativeFormatImporter:'
  '  externalObjects: {}'
  '  mainObjectFileID: 2100000'
  '  userData: '
  '  assetBundleName: '
  '  assetBundleVariant: '
 ) -join [Environment]::NewLine
 Set-Content (Join-Path $out ($name + '.mat.meta')) $meta -Encoding utf8
}
foreach ($entry in $profiles.GetEnumerator()) {
 $era=$entry.Key; $source=$entry.Value
 $srcAlbedo=Join-Path $root ('Assets\CityArchitecture\Generated\Textures\PBR_' + $source + '.png')
 $srcMaps=Join-Path $root 'Assets\CityArchitecture\Generated\Textures\Showcase'
 Copy-Item $srcAlbedo (Join-Path $out ('Wall_' + $era + '_Albedo.png')) -Force
 foreach ($channel in 'Normal','MetallicGloss','AO') { Copy-Item (Join-Path $srcMaps ('Showcase_' + $source + '_' + $channel + '.png')) (Join-Path $out ('Wall_' + $era + '_' + $channel + '.png')) -Force }
 Write-TextureMeta ('Wall_' + $era + '_Albedo') ($era + '_Albedo') 0 1
 Write-TextureMeta ('Wall_' + $era + '_Normal') ($era + '_Normal') 1 0
 Write-TextureMeta ('Wall_' + $era + '_MetallicGloss') ($era + '_MetallicGloss') 0 0
 Write-TextureMeta ('Wall_' + $era + '_AO') ($era + '_AO') 0 0
 Write-Material $era
}
Write-Output "WALL_CANDIDATES_GENERATED eras=$($profiles.Count) output=$out"

