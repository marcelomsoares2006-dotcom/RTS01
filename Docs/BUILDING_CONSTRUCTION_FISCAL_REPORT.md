# Relatório fiscal — construções e muralhas

Data: 2026-10-13

## Escopo

Revisão somente leitura dos testes e do suporte atual a construções. Nenhuma alteração foi feita em `BuildingEraDecoration.cs` e nenhum commit foi criado.

## Cobertura adicionada

Foi criado `Assets/Tests/Editor/BuildingConstructionFiscalTests.cs`, separado dos testes existentes, com três verificações:

1. `House` e `Tower` existem no catálogo e possuem malhas de construção válidas.
2. A decoração visual é idempotente, cria quatro grupos de era e não duplica grupos quando inicializada novamente.
3. A torre oferece muralha visual, contrafortes e ameias em todas as eras, e seteiras a partir da era Medieval.

## Lacuna encontrada: kit modular

Não foi localizado no projeto suporte executável para `WallStraight`, `WallCorner`, `Gatehouse` ou `WallTowerConnector`: não há prefab, `BuildingData`, catálogo `Resources` ou componente runtime com esses códigos. Os documentos existentes são apenas roadmap/direção visual.

Por isso, não foi criado um teste artificial para muralhas modulares. O próximo teste fiscal seguro deverá ser adicionado quando o kit possuir um contrato/catalogação real, cobrindo encaixe, orientação, escala, ausência de colisores duplicados e conexão com torre/portão.

## Resultado

Os testes novos estão preparados para execução no Unity EditMode. A execução Unity não foi feita nesta rodada para evitar interferência com uma instância aberta do editor; o arquivo de produção permaneceu intocado.
