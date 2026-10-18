# Auditoria final de performance — probe standalone

Data: 2026-10-18  
Projeto: `RTS01-Corrigido`  
Unity: `6000.6.3f1`  
Executável: `Builds/Windows/EraImperial.exe`  
SHA-256 do executável: `4C93A4BB060A12B9044AB2103835DD1565AEC9AA8381372FA84FBBC14A19B989`

## Resultado

**SHORT PROBE: PASS com ressalvas de métrica.** O player standalone concluiu os três perfis curtos com código de saída 0, durante 12,00–12,01 s após 3 s de aquecimento, em 1280×720 janela, teto de 60 FPS reaplicado pelo probe.

**BENCHMARK LONGO: INCOMPLETE.** A meta de 600 s não foi concluída; a auditoria longa existente não gerou uma execução completa válida. Isto não é promovido a PASS.

## Limites usados

| Métrica | Limite operacional | Interpretação |
|---|---:|---|
| FPS mínimo | >= 30,00 | nenhum frame abaixo deste piso na janela aprovada |
| FPS médio | >= 55,00 | média próxima do alvo de 60 FPS |
| FPS p95/p99 | informativo; referência >= 55,00 | estabilidade da cauda; não substitui o mínimo |
| Frame budget de 60 FPS | <= 16,67 ms | referência equivalente, não diretamente emitida pelo probe |
| Duração longa | 600 s | necessária para fechar o benchmark longo; não concluída |

## Execuções curtas

| Perfil | Qualidade | Duração | Frames | Mínimo FPS | Média FPS | p95 FPS | p99 FPS | Máximo FPS | Estado pelos limites |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|
| `quality-very-low` | Very Low | 12,01 s | 304 | 3,88 | 46,13 | 60,00 | 60,01 | 60,69 | **BLOCKED**: mínimo e média abaixo dos limites |
| `quality-medium` | Medium | 12,00 s | 472 | 7,95 | 56,35 | 60,01 | 60,08 | 60,59 | **BLOCKED**: mínimo abaixo do limite; média passa |
| `quality-high` | High | 12,01 s | 539 | 17,94 | 59,92 | 60,01 | 60,02 | 60,08 | **BLOCKED**: mínimo abaixo do limite; média passa |

Os mínimos baixos são outliers observados na janela curta; p95/p99 próximos de 60 FPS mostram que a maior parte das amostras ficou limitada pelo teto. Não foram descartados frames lentos nem substituídos por média aparada.

## Evidências preservadas

- `TestResults/PerformanceAudit/2026-10-18/retry-very-low/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/retry-very-low/long-fps-samples.csv`
- `TestResults/PerformanceAudit/2026-10-18/retry-medium/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/retry-medium/long-fps-samples.csv`
- `TestResults/PerformanceAudit/2026-10-18/retry-high/era-imperial-standalone-performance.json`
- `TestResults/PerformanceAudit/2026-10-18/retry-high/long-fps-samples.csv`
- `TestResults/PerformanceAudit/2026-10-18/run-summary.json` — primeira orquestração interrompida por timeout; mantida para rastreabilidade.
- `Docs/QA/2026-10-18-long-fps-audit.md` — evidência do benchmark de 600 s incompleto.
- `TestResults/LongFps/legacy-rx580-high/standalone-runtime.log`
- `TestResults/LongFps/legacy-rx580-high-retry/standalone-runtime.log`

Limitação de formato: o probe escreve `samplesPath` com barras invertidas não escapadas, portanto os três JSONs individuais são preservados como saída bruta, mas não são JSON estrito parseável. Os CSVs são a fonte tabular dos samples; os números acima são os valores emitidos pelo probe.

## Controle de escopo e processos

- Nenhuma cena, prefab, asset, script de gameplay ou configuração de gameplay foi alterado.
- Foram iniciados somente os players desta auditoria. Os PIDs criados e encerrados normalmente foram `63124` (Very Low), `56548` (Medium) e `62920` (High); nenhum processo pré-existente foi encerrado.
- O timeout da primeira orquestração encerrou somente os PIDs que ela própria criou; seus diretórios incompletos permanecem preservados.
- Não há aprovação de benchmark longo, estabilidade sustentada por 600 s ou desempenho em hardware-alvo a partir desta execução curta.

## Conclusão

O standalone inicia e termina corretamente nos três presets curtos, mas a auditoria final de performance **não está aprovada** pelos limites de frame mínimo e pelo requisito de duração longa. O próximo fechamento exige uma execução válida de 600 s, preservando o mesmo perfil, resolução, teto de 60 FPS e amostras sem interferência de outros processos.
