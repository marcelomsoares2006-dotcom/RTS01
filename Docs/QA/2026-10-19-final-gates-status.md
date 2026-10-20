# Era Imperial — status dos gates finais (2026-10-19)

## Confirmado nesta rodada

- Validação editor-free aprovada: Unity `6000.6.3f1`, 5 cenas de build, 43 contratos de construção, 11 entradas jogáveis e 1700 assets indexados.
- Otimização de produção integrada em `BuildingProductionRuntimeDriver` e fila: buffer de produtos concluídos reutilizado, evitando alocações por frame.
- `StandalonePerformanceProbe` passou a emitir heartbeat a cada 60 segundos para reduzir I/O e picos artificiais durante o soak.
- Auditoria estática das muralhas aprovada como candidata; a captura Unity continua obrigatória antes da promoção.
- Último soak válido: 600,02 s, média 60,07 FPS, mínimo 20,69 FPS. Esse resultado é anterior à última otimização e não deve ser usado como validação final.

## Resultados posteriores

- Licenciamento recuperado: o cliente oficial resolveu os detalhes de entitlement e o Unity executou sem o timeout de `LicenseClient-User`.
- EditMode: `402/402` testes aprovados.
- PlayMode: smoke aprovado nos três cenários, sem erros runtime.
- Build Windows recompilado em `2026-10-19 21:43:34`, com resultado `Succeeded`, 0 erros e 14 warnings. O executável antigo foi preservado como `Builds/Windows/EraImperial.previous-20261019.exe`.
- Harness real de produção/cerco aprovado: villager, spearman, combat mage e catapulta instanciados com NavMesh/seleção; quatro disparos reduziram o alvo `360 -> 260 -> 160 -> 60`.
- Captura Unity PBR das muralhas aprovada como `PASS_RUNTIME_CANDIDATE_COMPARISON_ONLY`; as imagens estão em `TestResults/WallPbrVisualQA-2026-10-19`. Nenhuma promoção foi feita automaticamente.
- O harness final de cobertura corrigiu o fixture V2 (`gameUid`) e compilou; ele continua bloqueando apenas a parte explicitamente humana, que não pode ser comprovada em batch.

## Bloqueios reproduzidos

- O soak de 600 s do executável recompilado concluiu com JSON válido em `TestResults/StandalonePerformance/rebuilt-600-20261019b`: duração 600,01 s, média 60,00 FPS, mínimo 10,60 FPS. A duração passou, mas o mínimo ainda está abaixo da meta de 30 FPS.
- O launcher intermediário ainda retorna 127; a execução mantida por `cmd` é o caminho válido neste host.
- PBR das muralhas permanece candidato: a captura Unity mostra produção/candidato lado a lado, mas o manifesto exige revisão das imagens antes de qualquer promoção.
- Playtest humano visual/manual continua pendente por definição: UI, pixels, controles físicos e revisão de UV/seams/LOD precisam de uma sessão interativa.

## Próximo ciclo autorizado

1. Recuperar o IPC/licença pelo Unity Hub e confirmar uma sessão normal, sem administrador.
2. Reexecutar EditMode, PlayMode, captura PBR/LOD e build Windows.
3. Reexecutar o soak de 600 s com o código atual e exigir mínimo de 30 FPS.
4. Fazer playtest visual/manual de UI, eras, produção, combate, cerco, destruição, save/load e NavMesh.
5. Promover apenas os assets aprovados; então tentar novamente o push.
