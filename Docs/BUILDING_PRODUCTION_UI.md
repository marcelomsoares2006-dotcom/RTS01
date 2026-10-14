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
