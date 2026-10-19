# Unity visual/runtime validation — 2026-10-18

## Resultado

- Unity 6000.6.3f1 iniciou em batch e o log confirmou licença inicializada e entitlement `UnityProStuXXXX`.
- O primeiro `AAARenderPipelineAudit.Capture` não foi aprovado: a chamada isolada não carregou cena/câmera e terminou com `Nenhuma Camera.main disponível`.
- O showcase reversível foi reconstruído e capturado com sucesso em quatro vistas:
  - `TestResults/CityArchitectureShowcase_Overview.png`
  - `TestResults/CityArchitectureShowcase_Mid.png`
  - `TestResults/CityArchitectureShowcase_Close.png`
  - `TestResults/TownCenter_Showcase_D3D11.png`
- A inspeção visual dessas imagens encontrou ausência de magenta nas vistas capturadas, mas também confirmou que o showcase é procedural/visual-only e ainda não demonstra qualidade AAA ou superioridade ao Age of Empires IV. Não promove materiais, LODs ou prefabs de produção.
- O harness de produção encontrou um erro de compilação em `CountUnits`; a função foi corrigida e a compilação seguinte não repetiu esse erro.
- A execução PlayMode real foi iniciada duas vezes, mas não produziu `LIVE_PRODUCTION_*`: com `-quit`, o editor saiu antes da transição assíncrona para PlayMode; sem `-quit`, o processo ficou preso no pipeline IL Post Processor. O processo batch criado para o teste foi encerrado de forma controlada. Produção real de villager/spearman continua **não comprovada**.

## Decisão

Nenhum candidato foi promovido. Permanecem pendentes a validação PlayMode real, colliders/runtime, LOD transition, UV/seams, projéteis/combate/cerco, benchmark de 600 s e revisão PBR dentro da partida.

## Correções posteriores

- `UIManager.Start` agora aguarda com aviso quando os recursos do jogador ainda não foram inicializados, evitando `NullReferenceException` durante a carga.
- `BuildingPlacer.SpawnBuilding` agora trata colocação rejeitada antes de acessar `_placedBuilding`, evitando o segundo `NullReferenceException` observado.
- A nova execução batch ficou novamente bloqueada na inicialização/licenciamento (`LicenseClient-User`) antes de concluir a compilação. Essas duas correções ainda precisam de confirmação no PlayMode licenciado.
- Uma tentativa posterior via `-hubIPC` estabilizou a licença e chegou ao PlayMode, mas terminou em `LIVE_PRODUCTION_PLAYMODE_BLOCKED` após 45 s. O log mostrou referências `CharacterData.prefab` incompatíveis no carregamento de Worker/Spearman e o reparador oficial foi ampliado para incluir Worker; sua execução seguinte ficou presa no IL Post Processor antes de salvar/verificar.
- Também foram adicionados guards de criação automática do `EventManager` e fallback seguro para `GameData.gameUid`, evitando os dois erros de inicialização observados. Ainda falta confirmação de compilação/importação limpa e nova execução PlayMode.

## Rodada 2026-10-18 — catálogo reparado

- `CharacterCatalogPrefabRepair.Run` foi ampliado para os 12 `CharacterData` do catálogo. A execução `TestResults/character-catalog-repair-all.log` registrou `Assigned` e `Verified` para cada entrada, eliminando os avisos anteriores de referência serializada incompatível.
- O harness recebeu o marcador `LIVE_PRODUCTION_READINESS`, separando GameManager, economia, catálogo, Spawnpoints, NavMeshSurface e vértices de NavMesh para localizar o próximo bloqueio sem mascarar falhas.
- Duas tentativas adicionais (`live-production-harness-diagnostic*.log`) não chegaram à cena: o Unity 6000.6.3f1 voltou a perder/reconectar o canal `LicenseClient-User` antes de executar o método. Portanto, produção real, UI, colliders, combate e cerco continuam sem PASS.
- As correções foram registradas no commit `32abbc3` e enviadas ao branch remoto `codex/integracao-github-20261014`. Documentos de auditoria e backups `.blend1` antigos permaneceram fora do commit.

## PlayMode licenciado — progresso confirmado

- O Unity Hub/cliente foi reiniciado elevado e o entitlement foi resolvido; os testes seguintes conectaram ao canal `LicenseClient-User`.
- `live-production-harness-unitmanager-guards.log`: **PASS** para produção real de villager, spearman e combat mage, com owner, NavMesh e seleção verificados.
- `ui-integrated-playmode-final.log`: **PASS** para Canvas, UIManager, widgets de recursos e eventos reais da partida.
- `final-integration-playmode.log`: **PASS** para produção, avanço até Imperial, catapulta/cerco lógico e save/load.
- Correções runtime: prazo rearmado após reload do domínio, minimap icon opcional e `UnitManager` tolerante a `fov`, `selectionCircle` e áudio ausentes. Ainda faltam build Windows, benchmark de 600 s, FPS, validação visual manual PBR/LOD/UV e combate/cerco visual com GameObjects reais.

