# Auditoria final de gaps QA — 2026-10-17

Projeto: `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido`  
Unity alvo: `6000.6.3f1`  
Escopo: auditoria somente leitura dos contratos de fundações, catálogo de unidades, produção real, avanço de eras, save/load, combate/cerco e scripts de validação.

## Resultado executivo

| Área | Estado | Resultado verificável |
|---|---|---|
| Contratos e assets de fundações | **PASS / PENDING** | Gate de repositório passou para 9/9 fundações; a aceitação Unity completa de colocação, construção e save/load ainda requer matriz PlayMode dedicada. |
| Catálogo de unidades/construções | **PASS** | 11 entradas, 11 referências `BuildingData`, todas em status Playable no catálogo atual. |
| Produção real | **PASS / PENDING** | Produção viva de trabalhador passou no PlayMode; não há evidência atual de matriz viva para todos os produtores militares e unidades avançadas. |
| Avanço de eras | **PASS / PENDING** | Contratos, regras sequenciais e save/load de tecnologia passam; falta evidência PlayMode de uma partida completa pelas quatro eras. |
| Save/load | **PASS / PENDING** | Reload em partida e contratos de construção/produção passam; migração/compatibilidade do caminho legado `BinaryFormatter` permanece sem fechamento. |
| Combate e cerco | **PASS / PENDING** | Contratos e evidências PlayMode retidas passam para os quatro tipos avançados; falta rerun fresco integrado produção→combate/cerco no estado atual. |
| Scripts de validação/release | **PASS** | `Validate-FoundationAssets`, `Validate-Release` e `Validate-EditorFree` passaram. |
| Higiene do worktree | **FAIL** | `git diff --check` encontra whitespace preexistente em `Docs/Runtime/2026-10-14-runtime-standalone-audit.md:3`; não foi corrigido por estar fora do escopo e para preservar alterações do usuário. |

Conclusão: **não há fechamento de release para toda a matriz solicitada**. O núcleo estático e os smoke tests disponíveis estão verdes, porém as lacunas `PENDING` impedem declarar cobertura completa de produção militar, progressão de eras e cadeia completa de cerco.

## 1. Contratos de fundações

### Status: PASS no repositório; PENDING na aceitação Unity completa

Evidências existentes:

- `Docs/FOUNDATION_BUILDING_ACCEPTANCE.md` define as nove fundações: Town Center, Resource Dropoff, Lumber Camp, Mining Camp, Mill, Barracks, Archery Range, Stable e Siege Workshop.
- `Docs/FOUNDATION_IMPORT_MANIFEST.yml` define os prefabs, `BuildingData`, modelos, componentes e o bloqueio até revisão Unity.
- `Tools/Validate-FoundationAssets.ps1` confirmou os nove estados como `model-authored`, referências de catálogo/GUID, prefabs, meshes de construção e contratos de layer/tag.
- Saída atual do gate: `Foundation asset gate passed ... repository-ready for Unity review.` Exit code `0`.
- `TestResults/sync-foundation-navmesh-obstacles.log` registra `FOUNDATION_NAVMESH_OBSTACLES_SYNCED=9`.
- `TestResults/unit-tests.xml`: `FoundationPlacementRulesTests` 6/6, `BuildingConstructionGateTests` 6/6, `BuildingConstructionRulesTests` 7/7 e `BuildingConstructionSaveContractsTests` 5/5.

Gap: o próprio gate declara prontidão para revisão Unity, não aceitação runtime. Não há, neste conjunto de evidências, uma matriz fresca que tente cada uma das nove fundações em terreno válido/inválido, custo único, construção operacional, produção/dropoff e restauração runtime.

Observação de evidência: `TestResults/foundation-promotion-batch.txt` registra promoção existente como PASS, enquanto `TestResults/foundation-promotion-batch.log` contém uma tentativa histórica com `MissingMethodException`. O gate estático atual é a evidência vigente; o log de falha histórica deve permanecer rastreado, não ser reinterpretado como aprovação.

## 2. Catálogo de unidades e construções

### Status: PASS

Evidências:

- `Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset` possui 11 entradas: Town Center, House, Resource Dropoff, Barracks, Stable, Archery Range, Siege Workshop, Mining Camp, Lumber Camp, Mill e Tower.
- As 11 entradas atuais estão com `status: 2` (`Playable`) e possuem referência `BuildingData`.
- `Tools/Validate-Release.ps1` passou com `11 catalog entries (11 BuildingData references)`.
- `Tools/Validate-EditorFree.ps1` passou com `11 catalog entries (11 playable)`.
- `TestResults/unit-tests.xml`: `BuildingContentCatalogTests` 10/10, `BuildingCatalogResolverTests` 3/3, `CharacterCatalogCompatibilityTests` 1/1, `UnitCodesAndProgressionTests` 13/13 e `BridgeAndCatalogTests` 6/6.

O contrato runtime está coerente com `BuildingContentCatalog.IsRuntimeReady`: status mínimo Playable e `BuildingData` resolvido. A aprovação aqui é de catálogo e resolução; ela não substitui a validação viva de cada combinação produtor/produto.

## 3. Produção real

### Status: PASS para trabalhador; PENDING para cobertura militar completa

Evidências:

- `Assets/Scripts/Units/Buildings/Building.cs` anexa `BuildingProductionRuntimeDriver` quando a construção se torna operacional.
- `Assets/Scripts/Units/Buildings/BuildingProductionRuntimeDriver.cs` liga catálogo/economia, avança a fila e, no callback de conclusão, resolve `CharacterData`, instancia o personagem e tenta posicioná-lo no NavMesh.
- `Assets/Scripts/Units/Buildings/BuildingProductionRuntimeAdapter.cs` e `BuildingProductionQueue.cs` cobrem autorização, fila FIFO, reserva/refund de recursos e conclusão determinística.
- `Assets/Editor/EraImperialSmokeTests.cs` executa produção viva de `villager -> worker` em uma partida nova.
- `TestResults/playmode-smoke.txt` contém `Live production enqueued: villager -> worker` e `PLAYMODE_PRODUCTION_PASSED: worker spawned in the live match.`
- `TestResults/unit-tests.xml`: `BuildingProductionCatalogRulesTests` 4/4, `BuildingProductionCommandRulesTests` 2/2, `BuildingProductionQueueTests` 4/4, `BuildingProductionReplayRulesTests` 4/4, `BuildingProductionRuntimeAdapterTests` 4/4, `BuildingProductionSaveContractsTests` 4/4, `BuildingProductionUiContractTests` 6/6 e `DataHandlerProductionQueueTests` 3/3.

Gap: a evidência PlayMode atual não demonstra todos os produtores militares: Barracks (spearman, combat mage, paladin healer), Archery Range (archer, crossbow), Stable (horseman) e Siege Workshop (battering ram, catapult). O catálogo e os contratos estão ligados, mas falta uma matriz viva que confirme spawn, custo, população, era, prefab e colocação para cada produto.

## 4. Avanço de eras

### Status: PASS para contratos; PENDING para fluxo PlayMode completo

Evidências:

- `Assets/Scripts/Units/AdvancedBridge/Runtime/AdvancedEraState.cs` mantém a era atual, emite mudança e reinicia em Ancient no início de sessão.
- `Assets/Scripts/StrategicAI/AgeUpTechnologyPlanningRules.cs` exige Town Center operacional, avanço sequencial, prédios/tecnologias prévias e recursos.
- `Assets/Scripts/Progression/ProgressionContracts.cs` rejeita saltos de era e valida população, landmark, prédios, tecnologias e custo antes de mutar o estado.
- `TestResults/unit-tests.xml`: `AgeUpTechnologyPlanningRulesTests` 4/4, `AdvancedEraStateVisualTests` 1/1, `AdvancedEraTechnologySaveLoadTests` 1/1 e `BuildingProgressionSyncRulesTests` 4/4.

Gap: `TestResults/playmode-smoke.txt` cobre partidas novas, movimentação e reload, mas não registra uma partida que execute Ancient → Classical → Medieval → Imperial por meio do fluxo real de tecnologia/recursos. A existência de regras e teste de save/load não prova a integração viva de todas as eras.

## 5. Save/load

### Status: PASS funcional básico; PENDING para legado/migração e cobertura integrada total

Evidências:

- `TestResults/playmode-smoke.txt`: `Scenario 3: reload saved Map1 ... PASSED` e `PLAYMODE_SMOKE_PASSED: 3 scenarios, 0 runtime errors.`
- `Assets/Scripts/Tools/DataHandler.cs` serializa unidades, construção runtime, filas de produção, manutenção, tecnologias desbloqueadas e estado/replay; restaura a era antes de reconstruir dependências.
- `Assets/Scripts/Tools/SaveFileStore.cs` implementa envelope versionado, magic/version, arquivo temporário, backup e recuperação de backup.
- `TestResults/unit-tests.xml`: `ConstructionReplayRuntimeRestoreTests` 3/3, `ConstructionReplayContractsTests` 9/9, `BuildingConstructionSaveContractsTests` 5/5, `BuildingProductionSaveContractsTests` 4/4, `EconomySaveStateTests` 2/2 e `SaveFileStoreTests` 2/2.
- `TestResults/runtime-standalone-audit.json` registra PlayMode e standalone como PASS; o smoke integrado também está sem erros runtime.

Gap: o caminho legado em `SaveFileStore` ainda utiliza `BinaryFormatter` isolado/avisado. Não há evidência deste audit de uma matriz de migração, compatibilidade entre versões ou análise de segurança para arquivos legados. Também não há evidência PlayMode atual de restaurar uma fila militar não vazia com todos os providers reais conectados.

## 6. Combate e cerco

### Status: PASS para contratos e evidência retida; PENDING para rerun integrado fresco

Evidências:

- `TestResults/unit-tests.xml` totaliza 392/392 PASS, incluindo `CombatAttackProfileTests` 3/3, `CombatAttackRuntimeTests` 6/6, `CombatDamageProfileTests` 3/3, `BuildingDefenseContractsTests` 4/4, `RangedAndSiegeCombatTests` 17/17 e `SiegeWorkshopMilitaryContractTests` 4/4.
- `Assets/Tests/Editor/AdvancedUnits/RangedAndSiegeCombatTests.cs` cobre limites de alcance, cooldown, impacto direto, dano radial/falloff, aliados, unidades mortas, projéteis balísticos e piercing.
- `Assets/Scripts/Units/Siege/CatapultDefaults.cs` possui quatro tiers de era, alcance mínimo/máximo, cooldown, dano radial e configuração balística de cerco.
- Evidência PlayMode retida: `TestResults/combatmage-playmode.txt`, `TestResults/crossbow-playmode.txt`, `TestResults/paladin-playmode.txt` e `TestResults/catapult-playmode-final-5.log` contêm marcadores PASS, zero erros runtime e verificações de combate/LOD/morte correspondentes.

Gap: os logs PlayMode são evidência retida de execuções anteriores, não um rerun completo desta auditoria em 2026-10-17. Além disso, falta o cenário integrado `produzir unidade de cerco -> posicionar -> atacar defesa -> aplicar dano/projétil -> salvar/recarregar`. Existem logs históricos com falhas de versões anteriores; eles estão superseded apenas pelas execuções PASS mais novas e não devem ser apagados.

## 7. Scripts de validação e evidência de release

### Status: PASS nos gates executados; FAIL de higiene do worktree; PENDING de rerun Unity neste audit

Comandos somente leitura executados nesta auditoria e resultados:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-FoundationAssets.ps1
# Exit 0; 9 fundações model-authored; Foundation asset gate passed.

powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-Release.ps1
# Exit 0; Unity 6000.6.3f1; 48 packages; 5 build scenes; 374 script metas; 11 catalog entries.

powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-EditorFree.ps1
# Exit 0; 5 build scenes; 43 construction contracts; 11 playable catalog entries; 1635 assets indexed.

git status --short
git diff --check
# git diff --check falha por whitespace preexistente em
# Docs/Runtime/2026-10-14-runtime-standalone-audit.md:3.
```

Evidência de execução já existente, lida sem reexecutar:

- `TestResults/unit-tests.xml`: 392/392 PASS, 0 failed.
- `TestResults/playmode-smoke.txt`: 3/3 cenários PASS, produção viva PASS, 0 erros runtime.
- `TestResults/windows-build.txt`: `Result: Succeeded`, `Errors: 0`.
- `TestResults/runtime-standalone-audit.json`: EditMode PASS, PlayMode PASS, build PASS, standalone PASS; 4 PASS, 0 FAIL, 0 BLOCKED; FPS médio 59.72.

Comandos destrutivos ou que escrevem artefatos **não foram executados** neste audit:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-Project.ps1 -Build
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\RuntimeStandaloneAudit\Invoke-RuntimeStandaloneAudit.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name adv-tests -UnityArgs '-runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults TestResults\adv-tests.xml'
```

Esses comandos são reproduzíveis para fechamento posterior, mas escrevem `TestResults`, podem gerar build e, no caso do audit standalone, removem/reescrevem saída anterior. O `Validate-Project.ps1 -Build` também depende do Unity/licenciamento e não foi tratado como evidência nova desta auditoria.

## Gaps que bloqueiam a declaração final de PASS

1. Executar uma matriz PlayMode atual para as nove fundações: colocação, rejeição, custo, construção operacional, produção/dropoff e save/load.
2. Executar produção viva de todos os produtores e produtos catalogados, incluindo as quatro unidades avançadas e as unidades de cerco.
3. Executar uma progressão viva Ancient → Classical → Medieval → Imperial, com requisitos, custo, tecnologia, desbloqueios e reload.
4. Executar o fluxo integrado de cerco produção→combate, incluindo projétil, dano a defesa, aliados, morte, LOD e save/load.
5. Definir e testar migração/compatibilidade do caminho legado `BinaryFormatter`.
6. Corrigir, em mudança separada e autorizada, o whitespace apontado por `git diff --check`.

## Integridade da auditoria

Nenhum código ou asset foi editado. O único arquivo novo produzido por esta solicitação é este relatório. Alterações já existentes no worktree foram preservadas e não foram tratadas como parte da implementação da auditoria.
