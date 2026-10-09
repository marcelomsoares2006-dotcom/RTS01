# Auditoria — ArcherElite do Drive

Data: 2026-10-07  
Escopo: somente `Tools/CharacterRigging/DriveImports`.

## Correções aplicadas

- Mantido `ArcherElite.glb` original intacto como fonte de importação.
- Criada a cópia `ArcherElite_Repaired.blend` com a malha `Archer` UV-unwrapped em `UVMap`.
- Removido o objeto auxiliar sem material (`Icosphere`) da cena corrigida.
- Preservados o material `Unit`, o `Armature`, o modificador de armature e os pesos da malha.
- Exportada também a cópia `ArcherElite_Repaired.glb`.

## Validação

Comando usado:

```text
blender -b --python Tools/CharacterRigging/ValidateGameAsset.py -- \
  Tools/CharacterRigging/DriveImports/ArcherElite_Repaired.blend \
  Tools/CharacterRigging/DriveImports/Validation/ArcherElite_Repaired_blend.json
```

Resultado: **PASS** (`exit 0`). O relatório confirma uma malha `Archer`, 1 camada UV, material `Unit`, rig `Armature` e zero erros.

O mesmo teste sobre `ArcherElite_Repaired.glb` ainda acusa `Icosphere` sem material. A inspeção do `.blend` corrigido confirma que esse objeto não existe na cena; ele é recriado pelo ciclo de importação do GLB do Blender 5.2, aparentemente como auxiliar do importador. Por isso o `.blend` é a cópia auditada e aprovada, enquanto o GLB fica registrado como exportação sujeita a essa limitação do conversor.

## Fora do escopo e proteção

Não foram alterados o arqueiro de produção, prefabs, catálogo, scripts de runtime ou qualquer asset em `Assets/`.

## Arquivos criados/alterados

- `Tools/CharacterRigging/DriveImports/ArcherElite_Repaired.blend` — criado, cópia corrigida e aprovada.
- `Tools/CharacterRigging/DriveImports/ArcherElite_Repaired.glb` — criado, exportação da cópia.
- `Tools/CharacterRigging/DriveImports/RepairArcherElite.py` — criado, reparo reproduzível.
- `Tools/CharacterRigging/DriveImports/Validation/ArcherElite_Repaired.json` — criado, validação do GLB (reprovação documentada).
- `Tools/CharacterRigging/DriveImports/Validation/ArcherElite_Repaired_blend.json` — criado, validação aprovada.
- `Docs/DriveImports-ArcherElite-Audit.md` — criado, este relatório.
