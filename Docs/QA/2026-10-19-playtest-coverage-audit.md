# Auditoria de cobertura do playtest — 2026-10-19

Projeto: `RTS01-Corrigido` / Unity `6000.6.3f1`

## Veredito

**PARCIALMENTE PASS — os contratos e parte do runtime real passam; a execução consolidada desta rodada ficou bloqueada pelo Licensing Client.**

Nenhuma cena, prefab, ScriptableObject, mesh, material ou script de gameplay foi alterado. O novo harness `FinalPlaytestCoverageHarness` ficou compilável e foi reconhecido pelo Unity, mas não chegou a emitir marcador porque a inicialização parou em `LicenseClient-User`.

## Evidência existente

| Frente | Resultado verificável | Evidência e limite |
|---|---|---|
| UI integrada | **PASS funcional** | `TestResults/ui-integrated-playmode-final.log`: `UI_INTEGRATED_PLAYMODE_PASS`. Cobre Canvas, `GraphicRaycaster`, `EventSystem`, `UIManager`, widgets/eventos; não fecha revisão humana de pixels. |
| Eras | **PASS de contrato integrado** | `TestResults/final-integration-playmode.log`: `FINAL_INTEGRATION_PLAYMODE_PASS`, incluindo avanço até Imperial. UI/feedback audiovisual de age-up continuam fora deste marcador. |
| Save/load | **PASS V2 de contrato** | O mesmo marcador integrado cobre round-trip; os testes EditMode históricos passaram `402/402`. Não é save/load após uma sessão humana completa. |
| NavMesh | **PASS no runtime de produção exercitado** | `TestResults/live-production-siege-harness-final2.log` registrou villager, spearman, combat mage e catapult com `navmesh=true`; o smoke real também terminou `PLAYMODE_SMOKE_PASSED`. |
| Combate lógico | **PASS de contratos; runtime parcial** | `CombatSiegePlayModeHarness`/EditMode cobrem alcance, cooldown, dano, cura e friendly-fire lógico. A produção viva recente materializou mage, mas não executou ataque mage/paladin/crossbow em partida completa. |
| Cerco estrutural | **PASS parcial no runtime real** | O harness vivo registrou `LIVE_PRODUCTION_SIEGE_PASS` após quatro impactos e destruição de `BuildingManager` adversário. Trajetória/efeitos visuais e todos os tipos de unidade ainda não são cobertura completa. |
| Produção viva | **PASS parcial** | Villager, spearman, combat mage e catapult foram instanciados com owner/seleção/NavMesh. Paladin/crossbow e combate completo de cada unidade continuam sem PASS. |

## Harness adicionado

`Assets/Editor/FinalIntegration/FinalPlaytestCoverageHarness.cs` verifica, em composição descartável `Core + GameScene + Map1`, UI real, eras, SaveMigrationV2, triangulação/NavMesh e contratos de combate/cerco. Ele marca explicitamente como bloqueados a produção/combate/cerco não exercitados por ele e o playtest humano; não fabrica objetos de gameplay para obter PASS.

## Bloqueio desta rodada

- `Tools/Validate-EditorFree.ps1 -SkipReleasePreflight`: **PASS** — 5 cenas, 43 contratos, 11 entradas jogáveis e 1.699 assets indexados.
- `git diff --check` nos arquivos da correção/harness: **PASS**; somente avisos normais de LF/CRLF foram reportados.
- `Tools/Validate-Project.ps1 -Build`: **BLOCKED** antes do Test Runner — `unit-tests` excedeu 300 s durante reconexões do `LicenseClient-User`.
- Execução do novo harness: **BLOCKED** antes do método — `Licensing initialization failed after 74.81s`; nenhum `FINAL_PLAYTEST_COVERAGE_PASS` foi contado.

## Warnings auditados

O build histórico reporta `Succeeded`, `0 erros`, `14 warnings`. O log de compilação do harness detalha obsolescências de busca (`FindObjectsByType`/`FindFirstObjectByType`) e variáveis locais não usadas. Foram corrigidas somente as ocorrências seguras em smoke tests, harnesses de UI/NavMesh e testes de contrato: busca sem ordenação foi migrada para `FindObjectsInactive.Exclude`/`FindAnyObjectByType`, e o teste deixou de tocar no alias obsoleto. Checks de `CharacterRigging`/`AdvancedAudit`, `Projector`, `AssetDatabase.ExportPackage`, showcase visual e aliases de produção foram preservados por risco de comportamento visual/empacotamento/compatibilidade.

A contagem pós-correção não foi certificada por uma nova compilação Unity devido ao bloqueio de licenciamento; portanto não declarar `0 warnings` ainda.
