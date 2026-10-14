# Auditoria visual AAA — 14/10/2026

## Escopo

Auditoria não destrutiva das construções em `Assets/CityArchitecture`, dos modelos de personagens em `Assets/CharacterRigging` e do enquadramento/pipeline AAA. Nenhum modelo, prefab de gameplay ou contrato foi substituído.

## Resultado executado

- 44 FBX de construções e personagens importados em Blender 5.2 em modo background.
- Nenhuma escala de objeto diferente de 1 foi encontrada nos objetos auditados.
- Nenhuma dimensão nula foi encontrada.
- Nenhum material contendo `pink`, `magenta` ou `error` no nome foi encontrado.
- Normais inválidas: 0; faces degeneradas foram apenas contabilizadas para revisão, sem alterar malhas.
- O relatório detalhado está em `visual-assets-audit.json`.
- A auditoria Unity `BuildingMeshAudit` não pôde iniciar porque havia outra instância do mesmo projeto aberta. O log está em `TestResults/city-building-mesh-audit.log`.

## Alertas que não foram alterados

Foram identificadas 20 ocorrências de UV0 fora de 0–1: 8 bordas de precisão nos postes da Market, 9 ocorrências de repetição provável na Crossbow, 2 peças para revisão manual (Bolt/Trigger) e 1 ilha quase no limite no LOD2 do HeraldicKnight. O overrun máximo das bordas da Market foi `7e-08`; a repetição da Crossbow chega a spans de até `9.06291` tiles. Esses objetos podem usar textura repetida; mover UV automaticamente alteraria texel density e aparência. Permanecem como revisão manual, sem substituição ou reexportação.

O detalhamento reproduzível está em `uv-review-2026-10-14.md` e `uv-review-2026-10-14.json`. O gate `Tools/CityArchitecture/Validate-AAAVisualGate.ps1` passou com 44 FBX e 20 ocorrências classificadas.

## Estado dos prefabs visuais isolados

Os prefabs arquiteturais em `Assets/CityArchitecture/Prefabs` não apresentam `LODGroup` ou collider próprios no YAML. Os prefabs de unidades auditados possuem collider; nenhum dos 21 prefabs auditados contém `LODGroup` explícito. Isso deve ser resolvido na frente de integração das construções/unidades, pois adicionar colliders ou LODGroup pode alterar bloqueio, placement e NavMesh. Não foi modificado nesta frente AAA.

## Enquadramento e evidência

A captura Unity não foi gerada nesta rodada porque o editor já estava aberto e o Unity recusou uma segunda instância em batch. Como evidência visual isolada, o preview Blender foi reenquadrado e renderizado em `TestResults/EraBuildingKitDetailedPreview_Framed.png`; ele é evidência de materialização do kit, não substitui a captura Unity final.

## Próximo passo seguro

Fechar o Unity aberto, executar novamente `BuildingMeshAudit.Run` e `AAARenderPipelineAudit.Capture` na cena isolada. Depois, revisar manualmente as UVs de Bolt/Trigger e HeraldicKnight LOD2 antes de qualquer ajuste; não normalizar as UVs classificadas como repetição provável sem verificar os materiais no Unity.
