# Harness PlayMode de produção viva — villager e spearman

## Escopo

`Assets/Editor/FinalIntegration/VillagerSpearmanLiveProductionPlayModeHarness.cs`
é um probe delimitado que abre a composição real `Core + GameScene + Map1`.
Ele não salva cenas e não altera prefabs, catálogos ou scripts de gameplay.

O cenário usa os `BuildingData` já publicados no catálogo para criar apenas
instâncias temporárias em memória de um Town Center e de um Barracks, ambos
operacionais e posicionados em pontos navegáveis do `Map1`. A fila é acionada
pelo `BuildingProductionRuntimeDriver`, com economia real inicializada pelo
`GameManager`/`DataHandler`.

## Observações exigidas

- `villager` é enfileirado no Town Center e observado como `worker`, que é o
  alias runtime documentado pelo driver.
- `spearman` é enfileirado no Barracks.
- Para cada produto, o harness confirma instanciação, `owner`,
  `CharacterManager`, `NavMeshAgent.isOnNavMesh` e entrada/saída da seleção
  através de `Globals.SELECTED_UNITS`/`UnitManager`.
- `combat_mage` é tentado como unidade avançada somente se o catálogo e o
  `CharacterData` já possuírem o produto/prefab. Ausência desse suporte não
  reprova a cobertura básica.
- O limite é de 45 segundos. O processo termina com código `0` em PASS,
  `2` em BLOCKED e `1` em FAIL.

Marcadores esperados:

```text
LIVE_PRODUCTION_VILLAGER_PASS
LIVE_PRODUCTION_SPEARMAN_PASS
LIVE_PRODUCTION_<ADVANCED>_PASS   (quando suportado)
LIVE_PRODUCTION_PLAYMODE_PASS
LIVE_PRODUCTION_PLAYMODE_BLOCKED
LIVE_PRODUCTION_PLAYMODE_FAIL
```

## Execução

Executar pelo Console/Batch da Unity 6000.6.3f1 chamando:

```csharp
VillagerSpearmanLiveProductionPlayModeHarness.Run();
```

A execução não foi repetida nesta alteração porque a auditoria anterior deste
checkout encontrou a Unity parada antes do PlayMode por licenciamento
(`LicenseClient-User` / `Licensing is not yet initialized`). Isso é BLOCKED
ambiental; não autoriza declarar PASS. Reexecutar somente quando o editor
estiver licenciado e puder carregar a cena real.

## Resultado desta revisão

**BLOCKED — execução Unity/licença pendente.** O arquivo do harness foi criado,
mas não há nova evidência runtime nesta revisão. Nenhum prefab, cena ou script
de gameplay foi modificado.
