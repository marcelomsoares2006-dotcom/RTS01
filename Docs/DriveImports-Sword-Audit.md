# Auditoria da espada de madeira

Data: 2026-10-07

Foi criada uma cópia reparada em `Tools/CharacterRigging/DriveImports/Repaired/WoodSword_Repaired.fbx` a partir da importação original. A malha foi unificada, teve a geometria reduzida para uso RTS, recebeu limpeza de duplicatas/ramificações não-manifold, normais recalculadas e UV rep projetada. O `.blend` correspondente foi preservado para inspeção.

Validação com `Tools/CharacterRigging/ValidateGameAsset.py`:

- **Aprovado** — 9.936 triângulos, 4.972 vértices.
- 1 camada UV, material `Material.001`, sem armature (asset estático).
- 0 erros e 0 warnings; 0 arestas não-manifold e 0 arestas de borda abertas reportadas.

Escopo preservado: nenhum arquivo em `Assets/Resources` ou prefab de produção foi alterado. A espada permanece apenas em `DriveImports/Repaired`.
