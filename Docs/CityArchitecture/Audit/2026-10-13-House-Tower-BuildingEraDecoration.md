# Auditoria fiscal — House, Tower e BuildingEraDecoration

Data: 2026-10-13  
Escopo: somente leitura. Nenhum código de produção, prefab ou asset visual foi alterado. Nenhum commit foi criado.

## Resultado executivo

**Aprovado com ressalvas para a rodada atual.** Os prefabs `House` e `Tower` preservam escala raiz unitária, possuem collider de seleção em modo trigger e `NavMeshObstacle` com dimensões correspondentes. `BuildingEraDecoration` mantém a decoração fora do collider e do carve principal, reduzindo o risco de regressão de placement e navegação.

## Evidências dos prefabs

| Item | House | Tower | Avaliação |
|---|---:|---:|---|
| Escala raiz | `(1,1,1)` | `(1,1,1)` | Aprovado |
| Collider principal | Box `4 x 2 x 4`, centro Y `1`, trigger | Box `1 x 5 x 1`, centro Y `2.5`, trigger | Aprovado |
| NavMeshObstacle | Extents `2 x 1 x 2`, centro Y `1`, carve ativo | Extents `0.5 x 2.5 x 0.5`, centro Y `2.5`, carve ativo | Aprovado; coincide com o BoxCollider |
| Tag/camada | `Unit` / layer `12` | `Unit` / layer `12` | Aprovado |
| Child groups no arquivo | seleção, FOV, mesh, healthbar/minimap via prefab instances | seleção, FOV, mesh, healthbar/minimap via prefab instances | Aprovado; não há grupo de era serializado |
| `BuildingEraDecoration` no prefab | Não | Não | Esperado: é adicionado em runtime por `Building` |

## `BuildingEraDecoration`

- Aceita somente `house` e `tower`; códigos desconhecidos são ignorados sem alterar a instância.
- Cria quatro grupos filhos por construção: `EraDetails_Ancient`, `EraDetails_Classical`, `EraDetails_Medieval` e `EraDetails_Imperial`.
- Ativa apenas a era atual quando a construção está concluída; durante a construção os grupos ficam inativos.
- Cada detalhe é um `Cube` criado em runtime e o collider é imediatamente desabilitado/removido. Portanto, os detalhes não ampliam o collider de seleção nem o carve do NavMesh.
- Usa `Mesh.bounds` da malha concluída para posicionar os detalhes e preserva o `Transform` do visual como pai.
- Materiais referenciados por caminho existem no projeto: `Stone`, `Metal`, `Wood Light` e `Wood Log`.
- A assinatura de `AdvancedEraState.Changed` é removida em `OnDestroy`, evitando inscrição persistente conhecida.

## Riscos e recomendações

1. **Custo de runtime:** todas as quatro eras são construídas, mesmo que apenas uma fique ativa. Em uma cidade grande isso multiplica GameObjects e Renderers. Recomenda-se medir contagem de objetos e FPS antes de trocar para pooling ou geração sob demanda.
2. **Materiais:** `Resources.Load<Material>` não é validado. Se um material for renomeado ou movido, o detalhe ficará sem material e a falha aparecerá apenas em runtime. Criar teste/diagnóstico de referência, sem alterar a lógica nesta auditoria.
3. **Bounds e escala:** o posicionamento depende de `Mesh.bounds` e do transform do renderer. Deve haver uma inspeção visual em pelo menos quatro ângulos e com escala de prefab alterada; a auditoria textual não confirma encaixe visual real.
4. **Child groups:** os grupos de era não são serializados nos prefabs. Isso é seguro para o fluxo atual, mas ferramentas de inspeção/editor e cenas abertas fora do fluxo de `Building` não verão a decoração.
5. **Regressão de colisão:** os detalhes não têm colliders, o que é correto para o design atual. Caso futuramente sejam adicionadas seteiras, portões ou passarelas funcionais, eles precisarão de uma política explícita de colisão/NavMesh.
6. **Root component:** `House` e `Tower` têm `Rigidbody` cinemático e collider trigger, enquanto o bloqueio de navegação é feito por `NavMeshObstacle`. Não alterar essa combinação sem repetir o teste de placement e carve.

## Conclusão fiscal

Não foi identificado erro estrutural imediato nos dois prefabs nem violação do contrato de não ampliar colliders pela decoração visual. A rodada pode prosseguir. Antes de adicionar novas construções à cidade, recomenda-se uma etapa visual no Unity para confirmar enquadramento, z-fighting, leitura dos detalhes por era e custo de instâncias.

