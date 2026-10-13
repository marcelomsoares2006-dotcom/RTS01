# Frente AAA — Paladino e Balestra

Data: 2026-10-12  
Escopo: cópias isoladas do pipeline Blender; nenhum prefab, runtime ou asset de produção foi alterado.

## Artefatos auditados

- `Tools/CharacterRigging/AAA/PaladinHealer/PaladinHealer_AAA.blend`
- `Tools/CharacterRigging/AAA/Crossbow/Crossbow_AAA.blend`
- Renders em `Docs/CharacterMeshAudit/AAA/` com resolução de 1440×2080 (Paladino) e 1920×1350 (Balestra).

## Resultado

### PaladinHealer — APROVADO para a próxima revisão

- 3 LODs: 31.000 / 9.300 / 3.100 triângulos.
- UV: 1 camada por LOD, sem erro de UV.
- Materiais: `PaladinHealer_Mat` presente nos 3 LODs.
- Skinning: armature `PaladinHealer_Armature`, todos os LODs skinned.
- Erros de validação: 0.
- Warnings: 3 avisos de bordas abertas, decorrentes da malha de personagem aberta em regiões de roupa/armadura; não há não-manifold reportado.
- Renders de Run, Attack e Death gerados para comparação visual em alta resolução.

Decisão: não integrar automaticamente. A deformação observada em Run/Attack/Death precisa de aprovação visual/reexportação final antes de substituir o asset de produção.

### Crossbow — REPROVADO para integração AAA

- 3 LODs presentes e renderizados.
- Materiais PBR nomeados e atribuídos (`Crossbow_Wood`, `Crossbow_DarkWood`, `Crossbow_Iron`, `Crossbow_Feather`).
- Falha bloqueadora: `Crossbow_Bolt` está sem referência de armature.
- UVs fora de 0–1 em peças do corpo, rodas, corda, parafuso, guincho, gatilho e LODs.
- Bordas abertas detectadas em corpo/rodas/bolt/LODs.
- Muitos avisos de faces flat-shaded; isso não é erro por si só em hard-surface, mas requer revisão de normais/suavização no reexport.

Decisão: não integrar LOD2 nem alterar o prefab. Corrigir UVs, separar corretamente o virote estático ou vinculá-lo ao rig, revisar bordas/normais e repetir a validação.

## Evidências

- `PaladinHealer-validation.json`: `passed=true`, 0 erros.
- `Crossbow-validation.json`: `passed=false`, 1 erro bloqueador e avisos de UV/bordas.
- `Crossbow/LOD1/` e `Crossbow/LOD2/`: comparação visual dos LODs em alta resolução.
- `RenderPaladinHealerAAA.py` e `RenderCrossbowAAA.py`: scripts reproduzíveis da rodada.

## Limitação da skill

As referências `references/patterns.md`, `references/sharp_edges.md` e `references/validations.md` exigidas pela skill não existem na instalação local. Foram aplicados os critérios equivalentes do `ValidateGameAsset.py` e do pipeline documentado do projeto.

## Próxima etapa segura

1. Corrigir a cópia AAA da Balestra no Blender.
2. Reexportar e validar novamente UV, manifold, normais, escala, pesos e LOD.
3. Submeter o Paladino a revisão visual comparativa das três animações deformadas.
4. Só após aprovação, preparar proposta de integração — sem integração automática nesta rodada.
