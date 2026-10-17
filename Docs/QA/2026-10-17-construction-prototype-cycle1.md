# Construction Prototype — Cycle 1

Data: 2026-10-17
Escopo: harness PlayMode descartável para a sequência House
Status: implementado; promoção de assets não realizada

## Objetivo

Exercitar, sem alterar gameplay, prefabs ou cenas, o maior subconjunto seguro do
primeiro ciclo jogável de construção:

`catálogo/BuildingData → prévia/colocação → reserva de custo → progresso/conclusão → save/load`

O harness está em:

`Assets/Editor/FinalIntegration/ConstructionMechanicPlayModeHarness.cs`

Ele usa uma cena vazia, entra em PlayMode e encerra o Editor com marcador de
resultado. O método pode ser executado por Unity batch com:

`-executeMethod ConstructionMechanicPlayModeHarness.Run`

## Cobertura executada

- Carregamento de `Building Content Catalog` por `Resources`.
- Resolução da entrada `house` e de seu `BuildingData`.
- Verificação de `constructionMeshes` e prefab no `BuildingData`.
- Validação do contrato de catálogo da House.
- Snap de posição, terreno plano/inclinado e distância livre/ocupada.
- Reserva atômica do custo da House.
- Bloqueio de segunda reserva com o mesmo ID, sem segundo débito.
- Commit da reserva.
- Estados `UnderConstruction` e `Operational`.
- Atribuição segura de construtor e liberação na conclusão.
- Captura, validação, escrita e leitura do snapshot via `SaveFileStore`.

## Gate explicitamente bloqueado

`FoundationPlacementRules` existente cobre apenas entradas com papel
`TownCenter` ou `ResourceDropoff`. Portanto, a colocação específica de `House`
não foi inventada nem forçada. O harness registra:

`CONSTRUCTION_CYCLE1_BLOCKED: FoundationPlacementRules não cobre House; contrato geométrico validado`

Isso não bloqueia a validação das regras geométricas puras, mas mantém pendente
o adaptador de colocação de House em runtime.

## Critério de leitura do log

- `CONSTRUCTION_CYCLE1_PASS`: todos os contratos disponíveis passaram.
- `CONSTRUCTION_CYCLE1_BLOCKED`: uma API/integração existente não cobre a parte
  pedida; o motivo deve aparecer no mesmo log.
- `CONSTRUCTION_CYCLE1_FAIL`: regressão ou exceção inesperada no harness.

O estado `BLOCKED` é deliberado e não autoriza promover `House.prefab`, LODs,
PBR ou qualquer asset de produção.

## Resultado desta execução

`Tools/Validate-EditorFree.ps1 -SkipReleasePreflight` passou com:

- Unity 6000.6.3f1;
- 5 cenas de build;
- 43 contratos de construção;
- 11 entradas de catálogo jogáveis;
- 1.664 assets indexados.

Uma tentativa limitada de execução batch do harness foi feita com
`-executeMethod ConstructionMechanicPlayModeHarness.Run`. O Unity iniciou, mas
ficou bloqueado antes da compilação/execução pelo serviço de licenciamento:

`[Licensing::IpcConnector] Channel LicenseClient-User doesn't exist`

O processo batch foi encerrado após a coleta do log, sem esperar
indefinidamente. Portanto, a compilação C# e o marcador PlayMode deste harness
ficam `BLOCKED` por infraestrutura nesta máquina. Nenhuma instância do
`House.prefab` foi criada e nenhum asset de produção foi alterado.

## Verificação editor-free

As verificações estáticas podem confirmar que o arquivo novo existe e que não há
alterações em gameplay, prefab ou cena. A execução PlayMode depende do Unity e
do `LicenseClient-User`; se esse serviço falhar, o relatório deve permanecer
como bloqueado de infraestrutura, sem declarar o ciclo concluído.

## Arquivos alterados nesta etapa

- `Assets/Editor/FinalIntegration/ConstructionMechanicPlayModeHarness.cs`
- `Docs/QA/2026-10-17-construction-prototype-cycle1.md`

Nenhum script de gameplay, prefab, cena, asset visual ou arquivo Git foi
promovido ou alterado.
