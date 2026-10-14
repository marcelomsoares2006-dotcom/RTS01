[CmdletBinding()]
param(
    [string]$BlenderPath = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [int]$ExpectedFiles = 44
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$auditScript = Join-Path $root 'Tools\CityArchitecture\AuditAAAVisualAssets.py'
$jsonPath = Join-Path $root 'Docs\AAA\VisualAudit\visual-assets-audit.json'
$uvPath = Join-Path $root 'Docs\AAA\VisualAudit\uv-review-2026-10-14.json'

if (-not (Test-Path -LiteralPath $BlenderPath)) { throw "Blender não encontrado: $BlenderPath" }
& $BlenderPath -b -P $auditScript
if ($LASTEXITCODE -ne 0) { throw "Auditoria Blender terminou com código $LASTEXITCODE" }

$audit = Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json
$uv = Get-Content -Raw -LiteralPath $uvPath | ConvertFrom-Json
$badRows = @($audit.rows | Where-Object {
    $_.has_non_unit_object_scale -or $_.has_zero_dimension_mesh -or $_.pink_material_keywords.Count -gt 0 -or
    ($_.meshes | Where-Object { $_.invalid_normals -gt 0 }).Count -gt 0
})

if ($audit.files -ne $ExpectedFiles) { throw "Quantidade de arquivos inesperada: $($audit.files), esperado $ExpectedFiles" }
if ($badRows.Count -gt 0) { throw "Falha de integridade visual em $($badRows.Count) arquivo(s)" }
if (@($uv.occurrences).Count -ne 20) { throw "Quantidade de ocorrências UV inesperada: $(@($uv.occurrences).Count), esperado 20" }

Write-Output "AAA_VISUAL_GATE_PASS files=$($audit.files) uv_review=$(@($uv.occurrences).Count) unsafe_rows=0"
Write-Output "UVs foram classificadas sem normalização automática; faces degeneradas são informadas no JSON para revisão, sem alteração automática."
Write-Output "Consulte Docs/AAA/VisualAudit/uv-review-2026-10-14.md"
