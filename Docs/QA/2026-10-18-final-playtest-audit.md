# Auditoria final de playtest automatizado — 2026-10-18

Projeto: `RTS01-Corrigido`  
Escopo: somente harnesses e testes. Nenhum gameplay, prefab, cena ou asset de produção foi alterado. Nenhum commit foi criado.

## Resultado geral

**Status: BLOCKED — auditoria parcial aprovada, integração jogável completa ainda não comprovada.**

O resultado não pode ser declarado como conclusão do jogo porque os harnesses existentes distinguem corretamente contratos determinísticos de uma partida real. A execução não inventa PASS quando falta uma dependência de cena, prefab, interação ou produção viva.

## Matriz executada

| Frente | Resultado | Evidência | Limitação |
|---|---|---|---|
| Integração final: produção de catapulta, avanço até Imperial, cerco lógico e save/load V2 | **PASS** | `TestResults/final-playtest-integrated.log` registrou `FINAL_INTEGRATION_PLAYMODE_PASS: production -> advanced unit -> siege -> save/load -> imperial` e saiu com código 0 | A “produção” deste harness é a fila/contrato da catapulta; não é materialização de uma unidade em um `Building` de uma partida real. |
| Produção viva de villager no Town Center | **BLOCKED** | `Docs/QA/2026-10-17-production-runtime-cycle6.md` | O harness não possui `Building` operacional, `GameManager.instance`, economia runtime e driver ligados a uma cena jogável; não deve fabricar essas dependências. |
| Produção viva de spearman no Barracks | **BLOCKED** | `Docs/QA/2026-10-17-production-runtime-cycle6.md` | Mesmo bloqueio de materialização e registro em `Unit.UNITS_BY_OWNER`. |
| Avanço de eras e round-trip V2 | **PASS** no contrato integrado; **BLOCKED** para UI de age-up | `TestResults/final-playtest-integrated.log`; `Docs/QA/2026-10-17-era-progression-cycle3.md` | O comando visual real, feedback e interação humana não são exercitados pelo harness isolado. |
| Mago de combate | **BLOCKED** para produção/combate de partida | `Docs/QA/2026-10-17-combat-siege-cycle4.md` e auditorias visuais históricas | Contrato de dano/alcance/cooldown passa, mas não há produção via edifício e combate integrado com unidade instanciada nesta auditoria. |
| Paladino | **BLOCKED** para produção/combate de partida | `Docs/QA/2026-10-17-combat-siege-cycle4.md` | Cura determinística passa; produção e pulso em partida real não foram comprovados. |
| Balestra | **BLOCKED** para produção/combate de partida | `Docs/QA/2026-10-17-combat-siege-cycle4.md` | Contrato de projétil e dano passa; prefab produzido e disparo em partida real não foram comprovados. |
| Catapulta | **PASS** no ciclo integrado lógico; **BLOCKED** como produção visual real | `TestResults/final-playtest-integrated.log`; `Docs/QA/2026-10-17-siege-visual-cycle6.md` | Ainda falta instanciar prefab de produção, revisar trajetória/efeito visual e destruir uma construção real em cena. |
| Cerco físico/visual | **BLOCKED** | `Docs/QA/2026-10-17-siege-visual-cycle6.md` | A fixture física isolada não é uma sessão com prefab, `BuildingManager`, partículas, animação, câmera e fragmentos de produção. |
| UI integrada à partida | **PASS funcional** | `Docs/QA/2026-10-18-ui-integrated-playmode.md`; `TestResults/ui-integrated-playmode.log` registrou `UI_INTEGRATED_PLAYMODE_PASS` | O batch sem gráficos não materializa captura de pixels; revisão visual manual permanece separada. |
| Save/load | **PASS** em V2 no ciclo integrado | `TestResults/final-playtest-integrated.log` | Ainda não é prova de save/load após uma partida completa com unidades vivas, combate e cerco reais. |

## Execução desta rodada

Foi iniciada a fila serial do Unity para:

1. `FinalIntegrationPlayModeHarness.Run` — concluído com **PASS**.
2. `EraProgressionPlayModeHarness.Run`.
3. `CombatSiegePlayModeHarness.Run`.
4. `SiegeVisualCycle6Harness.Run`.
5. `UIIntegratedPlayModeAudit.Run`.
6. `ProductionRuntimeCycle6Harness.Run`.

Após o primeiro método, a fila encontrou outra instância Unity já aberta no mesmo projeto (processos `Unity.exe` com `unit-tests.log`/`aaa-unity-readonly.log`). Essa instância não foi criada por esta auditoria e não foi encerrada. Os demais métodos não foram declarados como PASS por causa disso; os resultados anteriores documentados foram usados somente como evidência histórica, com suas limitações preservadas.

## Critério de conclusão ainda não atingido

Para sair de **BLOCKED**, é necessário executar em uma sessão Unity livre e licenciada um harness que:

- carregue `Core + Map1 + GameScene`;
- produza villager e spearman em edifícios operacionais, registrando as unidades vivas;
- produza e coloque mago, paladino, balestra e catapulta;
- execute era, combate, projéteis, cura, dano estrutural, destruição e feedback visual;
- faça save/load depois desses eventos e confirme o estado restaurado;
- capture UI e cena sem material rosa;
- registre FPS mínimo e média na sessão completa.

Se qualquer dependência não puder ser criada apenas no escopo descartável do harness, o resultado deverá continuar **BLOCKED**, sem alterar gameplay, prefabs ou cenas para mascarar a ausência de integração.

## Integridade do escopo

- Nenhum prefab, cena, ScriptableObject, mesh, material ou script de gameplay foi alterado nesta auditoria.
- Nenhuma alteração foi commitada ou enviada ao GitHub.
- Alterações antigas e backups existentes no working tree foram preservados.
