# Auditoria longa de FPS — INCOMPLETE

Data: 2026-10-18  
Projeto: RTS01-Corrigido  
Unity: 6000.6.3f1  
Hardware-alvo: AMD Radeon RX 580 8 GB (registro compartilhado do projeto)  
Perfil solicitado: legacy-rx580-high  
Preset temporário: High  
Resolução: 1280×720, janela, DirectX 11  
Meta: 600 s (10 minutos), FPS mínimo sustentado >= 30

## Status

**INCOMPLETE/BLOCKED — a execução não alcançou 600 segundos e não gerou o marcador STANDALONE_PERFORMANCE_PASSED.**

O processo do benchmark foi encerrado somente pelo PID criado para a auditoria, conforme solicitado. Nenhum processo do Unity Editor foi encerrado nesta finalização.

## Dados disponíveis

| Execução | Duração observada | Estado | FPS disponível |
|---|---:|---|---|
| Primeira tentativa (legacy-rx580-high) | 370,8 s | INVÁLIDA | Não usar: a troca para High restaurou o VSync e o probe mediu até aproximadamente 12.304 FPS artificiais. |
| Repetição com cap corrigido (legacy-rx580-high-retry) | 278,1 s no log preservado | INCOMPLETE | Mínimo 60,00; média 60,00; p95 60,00; p99 60,00; máximo 60,00 nas amostras agregadas disponíveis. |

Os percentis acima são somente da janela parcial agregada registrada no log; não representam uma auditoria completa de 10 minutos. Não há CSV final nem JSON final porque o probe só grava esses arquivos ao atingir o tempo configurado.

## Limites e interpretação

- A meta de 600 s não foi comprovada.
- A meta de FPS mínimo >= 30 foi observada na janela parcial válida, mas não pode ser declarada aprovada para 10 minutos.
- A primeira tentativa foi descartada por instrumentação incorreta e permanece preservada para rastreabilidade.
- O log bruto da tentativa inválida está em TestResults/LongFps/legacy-rx580-high/standalone-runtime.log.
- O log bruto da repetição está em TestResults/LongFps/legacy-rx580-high-retry/standalone-runtime.log.
- Nenhum gameplay, cena, prefab ou asset de produção foi alterado; as alterações foram restritas à instrumentação do probe e a este relatório.
- Nenhum commit foi criado.

## Próximo passo

Reexecutar o mesmo comando por 600 s, com o player recompilado e o cap de 60 FPS reaplicado após o preset High, até gerar era-imperial-standalone-performance.json e long-fps-samples.csv.
