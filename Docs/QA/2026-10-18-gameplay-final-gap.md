# Auditoria de integração de gameplay — gaps finais

Data: 2026-10-18
Projeto: `RTS01-Corrigido` / Unity `6000.6.3f1`
Escopo: auditoria read-only de integração. Nenhuma cena, prefab, ScriptableObject, mesh, material ou script de gameplay foi alterado.

## Veredito

**BLOCKED — produção real e ciclo jogável completo não comprovados.**

Existem contratos, assets e componentes de runtime para parte do roster, mas a evidência disponível não fecha o caminho `edifício operacional -> produção -> instanciação -> navegação/colisor -> combate/projétil -> UI -> cerco estrutural -> save/load`.

Não há PASS de produção real de villager/spearman nem de mage/paladin/crossbow/catapult em uma partida.

## Harness executado nesta rodada

Foi tentado:

```text
CombatSiegePlayModeHarness.Run
```

Comando: Unity 6000.6.3f1, `-batchmode -nographics`, projeto atual. Resultado: **INCONCLUSIVO antes do harness**; o processo parou durante a inicialização do Licensing Client, com `LicenseClient-User` recusado, `Licensing is not yet initialized` e sem marcador `COMBAT_SIEGE_CYCLE4_*`. Log: `%TEMP%\EraImperial-combat-siege-audit-20261018.log`.

O próprio código do harness é uma evidência de escopo, não de integração: `Assets/Editor/FinalIntegration/CombatSiegePlayModeHarness.cs` declara que não cria prefabs, colliders, projéteis GameObject, alvos `BuildingManager`, UI ou animações e adiciona estes gates como `BLOCKED`.

## Matriz de gaps

| Frente | Estado verificável | Falta para produção real |
|---|---|---|
| Villager | **BLOCKED** | O catálogo usa `villager`, mas o asset runtime é `Worker.asset`/código `worker`; o alias é resolvido somente em `BuildingProductionRuntimeDriver.OnProductCompleted` (`Assets/Scripts/Units/Buildings/BuildingProductionRuntimeDriver.cs:143-169`). Falta executar em `Building` operacional dentro de cena, com `GameManager.instance`, `DataHandler.EconomyRuntime`, callback, instanciação, registro em `Unit.UNITS_BY_OWNER` e spawn NavMesh observados. |
| Spearman | **BLOCKED** | Há `Spearman.asset` e prefab referenciado, com `BoxCollider`, `Rigidbody` e `NavMeshAgent` no prefab, mas o harness `ProductionRuntimeCycle6Harness` só testa fila/adaptador e explicitamente bloqueia o `BuildingProductionRuntimeDriver` e a materialização (`Assets/Editor/FinalIntegration/ProductionRuntimeCycle6Harness.cs:152-170`). Falta a mesma prova viva do villager, incluindo movimento e seleção. |
| Mage | **BLOCKED** | `CombatMage.prefab` possui `ProjectileAttackController` e contrato, mas `AdvancedProjectile` só prova deslocamento por `Transform` e callback; não há execução em partida que confirme spawn, impacto, dano, morte, efeitos e retorno de cooldown. O prefab também serializa `healthbar: {fileID: 0}` (`Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab:283-291`). |
| Crossbow | **BLOCKED** | Existe `CrossbowBolt.prefab`, mas o arquivo é visual/instanciável sem `Collider`, `Rigidbody`, script de projétil ou componente físico próprio; o dano depende do callback de `AdvancedProjectile`. Falta comprovar produção pelo Archery Range, alvo inimigo vivo, impacto/combate e feedback. |
| Paladin | **BLOCKED** | `PaladinMeleeAttackController` e `PaladinHealBehaviour` estão no prefab, e o contrato de cura tem testes determinísticos; falta produção pelo edifício, seleção de aliado, pulso em runtime, cura observável, cooldown, morte/feedback e UI. O prefab também não tem referência de healthbar (`healthbar: {fileID: 0}`). |
| Catapult | **BLOCKED** | `CatapultCombatController` implementa arco, impacto e dano radial apenas contra `UnitManager` encontrados por `AdvancedCombat.UnitsInRadius`. Falta produção pelo Siege Workshop, alvo `BuildingManager`, dano estrutural, destruição/estado destruído, efeitos/fragmentos, trajetória em cena e confirmação de que dano radial não ignora o alvo estrutural. |
| Projéteis | **BLOCKED** | Mage/crossbow/catapult têm prefabs visuais e `AdvancedProjectile`, mas a detecção é distância/tempo em script (`Assets/Scripts/Units/AdvancedBridge/Runtime/AdvancedProjectile.cs:73-124`), não colisão física. `CatapultBoulder.prefab` só apresenta `TrailRenderer`; `CrossbowBolt.prefab` não declara collider. Falta decidir e validar o contrato físico: layers/máscara, trigger ou sweep, tunneling, impacto em unidade/edifício, owner/friendly-fire e limpeza após miss. |
| Colliders | **PARTIAL / não aprovado** | Unidades avançadas têm collider de corpo no prefab, mas não há harness que valide `UNIT_MASK`, matriz de Physics layers, overlap/oclusão, tamanho de hitbox ou contato de projétil. O harness de combate declara “collider e camada UNIT_MASK não são exercitados”. |
| Cerco | **BLOCKED** | Há `SiegeAttackProfile.ResolveImpact` determinístico e contrato de catapulta, mas o alvo testado é `SiegeTarget`/unidade lógica. Falta circuito com `BuildingManager`, HP estrutural, destruição, efeitos, bloqueio/ocupação de espaço, vitória/objetivo e persistência após save/load. |
| UI | **PARTIAL / PASS funcional limitado** | `TestResults/ui-integrated-playmode.log` registra `UI_INTEGRATED_PLAYMODE_PASS` e `Docs/QA/2026-10-18-ui-integrated-playmode.md` confirma eventos de Canvas/UIManager em cena carregada. Isso não prova UI de cada unidade avançada, projétil, dano estrutural, cooldown, cura, fila dos quatro produtores, estados de morte nem pixels/captura; o próprio documento diz que batch sem gráficos não materializa pixels. |

## O que já está demonstrado, sem promover a integração

- `Assets/Resources/ScriptableObjects/Units/Characters/Worker.asset`, `Spearman.asset`, `CombatMage.asset`, `PaladinHealer.asset`, `Crossbow.asset` e `Catapult.asset` existem e referenciam prefabs.
- Os contratos determinísticos de mage, paladin, crossbow e catapult são exercitados no código de `CombatSiegePlayModeHarness`.
- `FinalIntegrationPlayModeHarness` valida progressão/contrato de produção lógica de catapulta, impacto lógico e save/load, mas seu comentário identifica que é runner sem cena/prefab e sua “produção” não materializa unidade viva (`Assets/Editor/FinalIntegration/FinalIntegrationPlayModeHarness.cs:13`, `:68-123`).
- Os logs históricos não devem ser reinterpretados como prova maior que seu escopo: `Docs/QA/2026-10-18-final-playtest-audit.md` já classifica produção viva de villager/spearman, combate de mage/paladin/crossbow, cerco físico/visual e catapulta visual como `BLOCKED`.

## Gaps desbloqueadores, em ordem

1. Rodar em sessão Unity licenciada e livre um harness descartável que carregue `Core + MainMenu + GameScene + Map1`, encontre os edifícios operacionais e produza villager e spearman, verificando instanciação, dono, população, `Unit.UNITS_BY_OWNER`, NavMesh e seleção.
2. Estender a matriz para Barracks/Archery Range/Siege Workshop: mage, paladin, crossbow e catapult, com custo/era/fila/prefab e estado vivo.
3. Definir e testar colisão de projéteis: layer/máscara, collider ou sweep, alvos válidos, friendly-fire, miss, lifetime e impacto em `UnitManager` e `BuildingManager`.
4. Ligar catapulta a HP estrutural real: dano direto/radial, destruição, efeito, bloqueio de navegação e restauração após save/load.
5. Acrescentar QA de UI por evento: produção, seleção, HP, dano, cura, cooldown, projétil, impacto e destruição; executar captura gráfica em resolução alvo e revisar pixels.
6. Repetir regressão depois da integração e registrar exit code, logs, captura, FPS/p95 e ausência de erros; qualquer dependência ausente deve permanecer `BLOCKED`.

## Integridade

Esta auditoria criou somente este relatório. Não foram alterados prefabs/cenas nem promovidos PASSs por inferência.
