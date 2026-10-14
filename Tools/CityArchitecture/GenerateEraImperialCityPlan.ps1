$ErrorActionPreference = 'Stop'
$cli = 'D:\AI\ArtCraft\CADCraft\cadcraft-0.5.0-windows-x64-portable\cadcraft-cli.exe'
$outDir = 'D:\AI\ArtCraft\Projects\EraImperial'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# Planta de referência em unidades de jogo: 120 x 80 m.
# O desenho é intencionalmente 2D; a extrusão e os colisores serão feitos no Blender/Unity.
$script = @'
RECTANG 0,0 120,80
RECTANG 6,6 114,74
RECTANG 12,12 108,68
RECTANG 56,0 64,6
RECTANG 56,74 64,80
RECTANG 0,36 6,44
RECTANG 114,36 120,44
RECTANG 20,20 36,32
RECTANG 84,20 100,32
RECTANG 20,48 36,60
RECTANG 84,48 100,60
RECTANG 45,28 55,38
RECTANG 65,28 75,38
RECTANG 45,42 55,52
RECTANG 65,42 75,52
RECTANG 57,6 63,74
RECTANG 6,37 114,43
CIRCLE 6,6 3
CIRCLE 114,6 3
CIRCLE 6,74 3
CIRCLE 114,74 3
CIRCLE 60,6 2.5
CIRCLE 60,74 2.5
CIRCLE 6,40 2.5
CIRCLE 114,40 2.5
ZOOM E
'@
$png = Join-Path $outDir 'era-imperial-city-plan.png'
$dxf = Join-Path $outDir 'era-imperial-city-plan.dxf'
& $cli run --script $script --save $dxf --export $png
if ($LASTEXITCODE -ne 0) { throw "CADCraft terminou com código $LASTEXITCODE" }
Write-Output "CITY_PLAN_CREATED $dxf"
Write-Output "CITY_PLAN_PREVIEW $png"
