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

