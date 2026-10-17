# Ciclo 6 — produção runtime de unidades

Data: 2026-10-17  
Escopo: protótipo isolado e auditável para produção real de `villager` e `spearman`.

## Regra de segurança

Este ciclo adiciona somente:

- `Assets/Editor/FinalIntegration/ProductionRuntimeCycle6Harness.cs`
- este relatório em `Docs/QA`.

Nenhum prefab, cena, `BuildingData`, `CharacterData`, asset visual, catálogo de produção ou script de runtime de produção foi alterado. Nenhum commit ou push foi feito.

## O que foi exercitado

O harness usa as APIs já existentes, sem duplicar a lógica da produção:

1. carrega `Building Content Catalog`;
2. resolve as entradas `town_center/villager` e `barracks/spearman`;
3. cria um `BuildingProductionRuntimeAdapter` isolado, com economia e população reais do domínio;
4. enfileira cada unidade e verifica a reserva de população;
5. avança o tempo de produção;
6. verifica o produto concluído, a remoção da fila e o callback de materialização;
7. verifica se a população permanece ocupada depois da conclusão;
8. inspeciona as dependências para ligar `BuildingProductionRuntimeDriver` a uma partida real.

## Resultado esperado

O fluxo determinístico do adaptador pode passar, mas o ciclo inteiro só pode ser `PASS` quando houver um `Building` operacional em uma cena de produção, `GameManager.instance`, `DataHandler.EconomyRuntime` e `CharacterData.prefab` resolvidos, com a unidade realmente instanciada no runtime.

O harness não fabrica essas dependências. Quando executado na cena vazia que ele próprio cria, deve retornar:

```text
PRODUCTION_RUNTIME_CYCLE6_BLOCKED
```

com bloqueios explícitos para:

- `GameManager.instance` ausente;
- `DataHandler.EconomyRuntime` não inicializado;
- ligação do `BuildingProductionRuntimeDriver` a um `Building` operacional não realizada;
- materialização real de `Character/Unit` em uma partida de produção não comprovada.

Isso é um bloqueio honesto de integração Unity/PlayMode, não uma falha do contrato de fila.

## Limites desta rodada

- Não há evidência de produção real em um prefab/cena de produção.
- Não há confirmação de `Unit.UNITS_BY_OWNER` recebendo a unidade concluída via partida.
- Não há teste de UI, animação, collider, NavMesh ou spawn visual.
- Não foi executado Unity batch nesta rodada; o histórico do projeto registra o bloqueio intermitente do `LicenseClient-User`.

## Verificações estáticas realizadas

- inspeção das APIs `BuildingProductionRuntimeDriver`, `BuildingProductionRuntimeAdapter`, `BuildingProductionQueue` e `BuildingProductionCatalogRules`;
- conferência de que os dois arquivos novos ficam dentro dos diretórios autorizados;
- `git diff --check` nos arquivos novos;
- nenhuma alteração em prefabs, cenas ou assets de produção.

## Próximo passo desbloqueador

Executar este harness, ou uma variante equivalente, em uma cena de produção aberta por um `GameManager` válido, com o Licensing Client funcionando. Só depois de observar `Character`/`Unit` instanciado e registrado deve-se promover qualquer integração para gameplay.

## Arquivos alterados

- `Assets/Editor/FinalIntegration/ProductionRuntimeCycle6Harness.cs` — novo.
- `Docs/QA/2026-10-17-production-runtime-cycle6.md` — novo.
