# Contrato declarativo de muralhas e portões

`WallGateContract` define a unidade de planejamento para segmentos, cantos, extremidades e portões modulares. O contrato cobre três decisões puras:

1. **Placement**: identidade, família, footprint livre, suporte do terreno, inclinação máxima e conexão exigida.
2. **Conexão**: mesma família, papéis compatíveis, endpoints livres e distância dentro do raio de encaixe.
3. **Abertura**: portão real, dono válido, estado diferente do solicitado e área livre para abrir.

Entradas de projeto devem permanecer com `runtimeEnabled = false` e `assetsReady = false` até que exista o pacote visual completo: prefab, modelo, colliders, estados de construção/dano/ruína, sockets e validação de editor. Assim o catálogo pode registrar a intenção de uma muralha ou portão sem fazer o runtime descobrir referências quebradas.

Este marco não adiciona assets, prefabs, componentes de cena ou integração automática em `BuildingPlacer`. A integração só deve ocorrer quando o primeiro conjunto modular real passar a auditoria de conteúdo. Os adaptadores futuros devem converter física/cena em `WallGatePlacementObservation` e consumir os resultados estáveis dos contratos, mantendo a simulação testável sem Unity.

## Critérios de aceitação para ativação

- `WallGateContract.IsRuntimeReady` somente com `runtimeEnabled` e `assetsReady` verdadeiros.
- A família deve possuir código estável e não pode conectar peças de famílias diferentes.
- A abertura deve respeitar dono e clearance, com estados `Closed`/`Open` como comandos persistentes.
- Testes `WallGateContractTests` devem passar no Unity Test Runner.
- Cada entrada ativada deve aparecer no catálogo de conteúdo com prefab, modelo, colliders, sockets, construção e ruína verificáveis.

Até esses critérios, o contrato é documentação executável e não altera a jogabilidade existente.
