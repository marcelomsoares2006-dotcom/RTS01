# Ciclo 2 — protótipo economia/produção

## Escopo

Harness isolado em `Assets/Editor/FinalIntegration/ProductionEconomyPlayModeHarness.cs`.
O ciclo usa apenas APIs já existentes e não altera gameplay, prefabs, cenas ou Git.

## Gates exercitados

- carregamento e validação do `Building Content Catalog`;
- ledger inicial, soma e gasto atômico;
- contrato existente de `EconomyCollector`/`EconomyPlayerState.TickCollectors`;
- resolução da fila `Town Center -> villager`, quando o catálogo está RuntimeReady;
- resolução da fila `Barracks -> spearman`, quando os requisitos existentes estão disponíveis;
- reserva de custo e população;
- cancelamento e reembolso;
- captura/restauração de `BuildingProductionQueueSnapshot`;
- persistência pelo `SaveFileStore` e limpeza dos artefatos temporários.

## Critério de saída

- `Exit(0)`: todos os gates passam e não há bloqueios.
- `Exit(2)`: há qualquer bloqueio conhecido, inclusive ausência de integração de coleta runtime.
- `Exit(1)`: falha inesperada, exceção ou assert inválido.

O marcador `PRODUCTION_ECONOMY_CYCLE2_PASS` só é emitido quando a lista de bloqueios está vazia. Em caso de bloqueio, o marcador é `PRODUCTION_ECONOMY_CYCLE2_BLOCKED`; não há falso PASS.

## Limite honesto da coleta

Existe um coletor determinístico que acumula carga e deposita no `EconomyLedger`, mas não existe neste harness uma API existente que faça o fluxo vivo de trabalhador, movimento, chegada ao prédio e entrega em um `Building` instanciado. O harness valida o contrato do coletor e registra esse fluxo como `BLOCKED`, sem inventar runtime.

## Execução

Método Unity:

```text
ProductionEconomyPlayModeHarness.Run
```

Execução editor-free deve, no mínimo, verificar a presença do arquivo, a assinatura do método, os códigos de saída documentados e a ausência de referências a alteração de cena/prefab. A execução PlayMode real depende do Unity Editor/licenciamento.

## Resultado desta implementação

Editor-free concluído em 2026-10-17:

- arquivos, método `Run` e `.meta` presentes;
- catálogo contém `town_center` e `barracks`;
- os três caminhos de saída estão presentes: 0 sem bloqueio, 2 com bloqueio, 1 por falha;
- marcador `PRODUCTION_ECONOMY_CYCLE2_PASS` está protegido por `Blocked.Count == 0`;
- nenhum `PrefabUtility`, `AssetDatabase.SaveAssets`, `SaveScene` ou alteração de cena aparece no harness;
- `git diff --check` passou.

Como solicitado, não aguardei a inicialização/licenciamento do Unity. Portanto, não há alegação de execução PlayMode nem de compilação C# pelo Unity nesta etapa. A execução Unity futura deverá registrar o log completo; pela cobertura atual, o fluxo de coleta viva deverá retornar `PRODUCTION_ECONOMY_CYCLE2_BLOCKED` até existir uma API de movimento/entrega runtime.
