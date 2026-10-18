# Rodada de otimização de performance — probe standalone

Data: 2026-10-18  
Projeto: `RTS01-Corrigido`  
Unity: `6000.6.3f1`  
Escopo: investigar outliers de startup, qualidade, VSync, shader compilation e scene load sem alterar gameplay.

## Veredito

**INCOMPLETE / BLOCKED**.

- **PASS parcial:** os três perfis curtos observados no EXE existente terminaram com código 0, sem erro fatal no log, e ficaram em ~60 FPS após o aquecimento.
- **INCOMPLETE:** o benchmark de 600 s foi interrompido a pedido em `459,3 s`; não gerou JSON/CSV final. O log bruto foi preservado.
- **BLOCKED:** o rebuild que validaria a nova telemetria do probe não foi concluído: o Unity batch registrou `Licensing is not yet initialized`/timeout. O EXE continua com timestamp de 2026-09-28, portanto não atribuo os perfis curtos à instrumentação nova.

## Causa segura identificada

Os outliers estão concentrados no início da execução, não em uma queda sustentada:

| Perfil observado no EXE existente | Duração | Frames sustentados | Mínimo bruto | Média | p95 | p99 | Leitura |
|---|---:|---:|---:|---:|---:|---:|---|
| default, VSync 0 | 12,00 s | 538 | 18,03 FPS | 59,92 | 60,01 | 60,01 | outlier inicial; regime sustentado próximo de 60 |
| Very Low, VSync 0 | 12,00 s | 539 | 30,46 FPS | 59,88 | 60,01 | 60,01 | sem queda sustentada |
| High, VSync 0 | 12,00 s | 540 | 29,80 FPS | 59,94 | 60,01 | 60,01 | sem queda sustentada |

O log mostra `frames=0` no primeiro relatório após a instalação do probe e, depois, médias próximas de 60 FPS. Isso é compatível com startup/primeiro frame, carregamento da cena e/ou compilação/ativação inicial de shaders. O player não oferece, por esta API do probe, uma atribuição causal direta entre esses eventos; portanto shader compilation e scene load ficam como hipóteses correlacionadas, não como causa comprovada.

Qualidade e VSync não são causas seguras da queda sustentada: o preset `High` reativa VSync em `ProjectSettings/QualitySettings.asset`, mas o probe reaplica `vSyncCount=0` e `Application.targetFrameRate=60` depois da seleção. A comparação curta adicional `High + VSync 1` também ficou em 59,93 FPS de média, p95 60,00 e p99 60,01. O teste é apenas diagnóstico e não altera a configuração global do projeto.

## Benchmark longo interrompido

Comando lógico: 1280×720 janela, `High`, `-era-audit-vsync=0`, alvo 60 FPS, aquecimento 3 s, duração solicitada 600 s.

| Campo | Valor bruto |
|---|---:|
| PID criado pela auditoria | 21796 |
| Duração observada no último log | 459,3 s |
| Frames observados no último log | 27.379 |
| Média agregada do último relatório periódico | 60,0 FPS |
| Erros/crashes encontrados no log | nenhum |
| Estado | INCOMPLETE — interrompido de forma limpa/isolada após tentativa de fechamento da janela; PID confirmado encerrado |

O player não chegou a `Finish()`, então não há `era-imperial-standalone-performance.json` nem `long-fps-samples.csv` final para esta execução. O arquivo de log é a amostra bruta preservada.

## Mudança reversível no probe

`Assets/Scripts/Tools/StandalonePerformanceProbe.cs` recebeu somente instrumentação de auditoria:

- opção diagnóstica `-era-audit-vsync=0/1`;
- registro dos primeiros frames e fase `warmup`/`sustained`;
- cena ativa, VSync e target FPS no CSV/JSON;
- escaping correto de caminhos no JSON;
- guarda contra finalização duplicada.

Nenhuma cena, prefab, asset, sistema de gameplay ou `QualitySettings.asset` foi alterado. Como o rebuild foi bloqueado pelo licenciamento, a instrumentação nova ainda precisa de uma execução em player recompilado para ser aprovada.

## Evidências

- `TestResults/PerformanceAudit/2026-10-18/codex-baseline/default/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/codex-baseline/very-low/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/codex-baseline/high/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/codex-instrumented-short/` — JSONs e CSVs dos três perfis diagnósticos executados no EXE existente; os campos da instrumentação nova não aparecem porque o rebuild não entrou no player.
- `TestResults/PerformanceAudit/2026-10-18/codex-600s-existing-build/standalone.log` — log bruto até 459,3 s.
- `TestResults/codex-performance-build.log` — tentativa de rebuild, incluindo o bloqueio de licenciamento.

## Arquivos alterados nesta rodada

- `Assets/Scripts/Tools/StandalonePerformanceProbe.cs`
- `Docs/QA/2026-10-18-performance-optimization-round.md`

Os arquivos de evidência em `TestResults/` são saídas da auditoria e foram preservados; não houve alteração em gameplay ou em processos externos.

## Próximo fechamento

Com uma sessão Unity licenciada, recompilar o player, executar um perfil curto para confirmar `startup-fps-samples.csv`/JSON instrumentados e repetir os 600 s sem encerrar o processo antes do marcador final. Só então o benchmark longo poderá receber `PASS`.
