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
- Revisão automática dos screenshots Unity não encontrou pixels magenta nos três cenários de jogo nem na comparação das muralhas.
- Launcher standalone corrigido para iniciar `EraImperial.exe` relativo ao diretório de build; smoke de 8 s passou `PASS=4 FAIL=0`.
- Com aquecimento de 5 s, o smoke de 30 s passou com mínimo 59,35 FPS e média 60,00 FPS.
- O harness final de cobertura corrigiu o fixture V2 (`gameUid`) e compilou; ele continua bloqueando apenas a parte explicitamente humana, que não pode ser comprovada em batch.
- O histórico de build somente leitura foi movido para `Library/BuildHistory/_readonly-backup-20261019` como backup recuperável. A recompilação seguinte removeu o warning de permissão: o build ficou em `Succeeded`, 0 erros e 14 warnings.
- Os 14 warnings restantes foram isolados ao aviso informativo do pacote `com.unity.pipeline`: `RuntimePipelineConfig` não existe e, por isso, o Pipeline Runtime permanece desabilitado no Player. Não é usado pelo jogo e não foi ativado artificialmente.

## Bloqueios reproduzidos

- O soak de 600 s do executável recompilado concluiu com JSON válido em `TestResults/StandalonePerformance/rebuilt-600-20261019b`: duração 600,01 s, média 60,00 FPS, mínimo 10,60 FPS. A duração passou, mas o mínimo ainda está abaixo da meta de 30 FPS.
- O soak longo de 600 s ainda deve ser repetido com o launcher corrigido; a execução manual por `cmd` já produziu JSON válido.
- PBR das muralhas permanece candidato: a captura Unity mostra produção/candidato lado a lado, mas o manifesto exige revisão das imagens antes de qualquer promoção.
- Playtest humano visual/manual continua pendente por definição: UI, pixels, controles físicos e revisão de UV/seams/LOD precisam de uma sessão interativa.
- O soak recompilado registrou um único frame abaixo de 30 FPS no startup (10,60 FPS, por volta de 3,8 s); os demais 35.818 frames ficaram acima de 30 FPS. Isso não fecha a meta de mínimo estrito, mas caracteriza o problema como hitch isolado de inicialização.

## Próximo ciclo autorizado

1. Fazer a sessão interativa no Unity licenciado para confirmar UI, controles, UVs, seams e transições LOD.
2. Avaliar as imagens PBR das muralhas e promover somente se a inspeção humana aprovar; manter candidatos caso contrário.
3. Decidir se o Pipeline Runtime será necessário; se não for, manter o aviso documentado em vez de habilitar um serviço sem uso.
4. Se a meta de mínimo estrito de 30 FPS for obrigatória, eliminar o hitch de startup e repetir o soak de 600 s.
5. Reexecutar o playtest humano completo e publicar apenas o build aprovado. O commit funcional já está no GitHub (`7efcaf8`).
