[CmdletBinding()]
param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path)
$ErrorActionPreference = 'Stop'
$contractPath = Join-Path $ProjectRoot 'Docs\CharacterMeshAudit\ArcherPreparation\archer-preparation.contract.json'
$readmePath = Join-Path $ProjectRoot 'Docs\CharacterMeshAudit\ArcherPreparation\README.md'
if (-not (Test-Path -LiteralPath $contractPath -PathType Leaf)) { throw "Contrato ausente: $contractPath" }
if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) { throw "README ausente: $readmePath" }
$contract = Get-Content -Raw -LiteralPath $contractPath | ConvertFrom-Json
if ($contract.characterId -ne 'archer') { throw 'characterId inválido.' }
if ($contract.invariants.mustNotModifyExistingCharacters -ne $true -or $contract.invariants.mustNotInventMissingMesh -ne $true) { throw 'Invariantes de preservação ausentes.' }
if ($contract.requiredArtifacts.Count -lt 5) { throw 'Contrato incompleto: artefatos insuficientes.' }

switch ($contract.status) {
    'blocked-until-source-mesh' {
        # Estado antigo: nenhuma malha-fonte; nenhum asset pode existir.
        if ($contract.source.provided -ne $false) { throw 'source.provided deve ser false enquanto bloqueado.' }
        if (Test-Path -LiteralPath (Join-Path $ProjectRoot 'Assets\CharacterRigging\Archer')) { throw 'A pasta de assets do Arqueiro não deve existir antes da fonte.' }
        Write-Output 'PASS ArcherPreparation: bloqueado, nenhum asset inventado.'
    }
    'integrated-validated' {
        # Estado atual: fonte e licença registradas e todos os artefatos exigidos existem.
        if ($contract.source.provided -ne $true -or $contract.source.licenseConfirmed -ne $true) { throw 'Fonte/licença devem estar registradas.' }
        if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $contract.source.path))) { throw "Fonte ausente: $($contract.source.path)" }
        foreach ($artifact in $contract.requiredArtifacts) {
            if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $artifact) -PathType Leaf)) { throw "Artefato ausente: $artifact" }
        }
        $audit = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'Docs\CharacterMeshAudit\Archer\unity-audit.txt')
        if ($audit -notmatch 'ARCHER_AUDIT_PASSED') { throw 'Auditoria Unity do Archer não passou.' }
        $play = Get-Content -Raw -LiteralPath (Join-Path $ProjectRoot 'TestResults\archer-playmode.txt')
        if ($play -notmatch 'ARCHER_PLAYMODE_PASSED') { throw 'Play Mode do Archer não passou.' }
        Write-Output "PASS ArcherPreparation: integrado e validado ($($contract.requiredArtifacts.Count) artefatos, auditoria e Play Mode aprovados)."
    }
    default { throw "Estado desconhecido: $($contract.status)" }
}
