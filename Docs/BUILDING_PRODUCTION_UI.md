# Contrato de UI da fila de produção

`BuildingProductionUiAdapter` é a ponte entre `BuildingProductionRuntimeAdapter` e a UI existente. O contrato não instancia prefabs nem conhece `MonoBehaviour`: a UI assina `StateChanged`, lê `State` ou chama `Refresh()` e renderiza o resultado.

Cada `BuildingProductionUiState` contém a identidade do prédio, proprietário, disponibilidade, limite e quantidade da fila. Cada `BuildingProductionUiOrder` contém índice, código do produto, custo copiado, população reservada, duração, restante e progresso entre `0` e `1`.

Comandos de produção passam pelo mesmo limite de propriedade e disponibilidade do runtime:

- `TryEnqueue(ownerId, entry, out queueResult)` adiciona a ordem e publica um snapshot quando aceita.
- `Advance(ownerId, deltaTime, out completedProducts)` avança a fila e publica o progresso.
- `TryCancel(ownerId, index, out cancelled)` cancela, reembolsa custo/população e publica o novo estado.
- `Refresh()` publica o snapshot atual para uma UI que acabou de abrir.

Os snapshots e custos são cópias profundas. Alterar dados recebidos pela UI não altera a fila nem permite que o painel contorne o controle de proprietário. A integração visual deve apenas ligar os campos a elementos já existentes e fornecer o índice da ordem ao botão de cancelamento.

Validação determinística: `BuildingProductionUiContractTests` cobre progresso, custo, cancelamento, reembolso, isolamento do snapshot, autorização e notificações. A execução no Unity Test Runner licenciado ainda é o gate final do projeto.
## Ligação ao catálogo e ao runtime

`BuildingProductionRuntimeBinding.TryCreate(building, catalog, economy, out binding, out reason)` cria uma sessão para um prédio já existente. A ligação verifica o código estável do `BuildingData`, usa o `EconomyPlayerState` do proprietário e expõe `binding.Ui` para o painel. `binding.TryEnqueue(ownerId, productCode, out queueResult)` resolve o produto em `BuildingCatalogData.production`, portanto o painel não precisa duplicar custos ou tempos.

Para o roster planejado, `BuildingProductionCatalogRules` valida `BuildingContentCatalog` antes da fila. Barracks, Archery Range e Stable permanecem bloqueados enquanto `BuildingData`/prefab não existem. A ponte também identifica `UnsupportedEconomyResource` quando a declaração usa Food, pois a fila legada ainda aceita apenas Wood, Gold e Stone; nenhum recurso é convertido silenciosamente.

O avanço permanece responsabilidade do loop de partida: chame `binding.Advance(ownerId, deltaTime, out completedProducts)` e assine `binding.ProductCompleted` para materializar a unidade ou o efeito correspondente. A ponte não instancia prefabs, não altera cenas e rejeita catálogos ou estados econômicos ausentes antes de criar o adapter.
