# Auditoria visual AAA — 14/10/2026

## Escopo

Auditoria não destrutiva das construções em `Assets/CityArchitecture`, dos modelos de personagens em `Assets/CharacterRigging` e do enquadramento/pipeline AAA. Nenhum modelo, prefab de gameplay ou contrato foi substituído.

## Resultado executado

- 44 FBX de construções e personagens importados em Blender 5.2 em modo background.
- Nenhuma escala de objeto diferente de 1 foi encontrada nos objetos auditados.
- Nenhuma dimensão nula foi encontrada.
- Nenhum material contendo `pink`, `magenta` ou `error` no nome foi encontrado.
- O relatório detalhado está em `visual-assets-audit.json`.
- A auditoria Unity `BuildingMeshAudit` não pôde iniciar porque havia outra instância do mesmo projeto aberta. O log está em `TestResults/city-building-mesh-audit.log`.

## Alertas que não foram alterados

Foram identificadas 20 ocorrências de UV0 fora de 0–1, principalmente em postes da Market e peças da Crossbow. Esses objetos podem usar textura repetida; mover UV automaticamente alteraria texel density e aparência. Permanecem como revisão manual, sem substituição ou reexportação.

## Estado dos prefabs visuais isolados

Os prefabs em `Assets/CityArchitecture/Prefabs` não apresentam `LODGroup` ou collider próprios no YAML. Isso deve ser resolvido na frente de integração das construções, pois adicionar colliders pode alterar bloqueio, placement e NavMesh. Não foi modificado nesta frente AAA.

## Enquadramento e evidência

A captura Unity não foi gerada nesta rodada porque o editor já estava aberto e o Unity recusou uma segunda instância em batch. Como evidência visual isolada, o preview Blender foi reenquadrado e renderizado em `TestResults/EraBuildingKitDetailedPreview_Framed.png`; ele é evidência de materialização do kit, não substitui a captura Unity final.

## Próximo passo seguro

Fechar o Unity aberto, executar novamente `BuildingMeshAudit.Run` e `AAARenderPipelineAudit.Capture` na cena isolada. Depois, revisar manualmente as UVs repetidas antes de qualquer ajuste.
