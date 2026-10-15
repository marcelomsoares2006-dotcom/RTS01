# Auditoria independente de runtime/standalone

Data: 2026-10-14T22:08:29.1886374-03:00  
Unity: `6000.6.3f1`  
Escopo: partida limpa, produção/contratos, save/load, replay, seleção, NavMesh, combate/cerco e player Windows.

## Resultado

- PASS: 4
- FAIL: 0
- BLOCKED/indeterminado: 0

| ID | Área | Estado | Evidência | Detalhe |
|---|---|---|---|---|
| editmode | contratos e smoke de dados | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\unit-tests.xml` | EditMode: 390/390 aprovados. |
| playmode | partida, navegação, render e save/load | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\playmode-smoke.txt` | Smoke PlayMode concluiu os três cenários sem erro runtime. |
| build | player Windows | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\windows-build.txt` | Build Windows reporta Succeeded e zero erros. |
| standalone-start | execução fora do Editor | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\standalone-runtime.log` | Player gráfico encerrou pelo probe com código 0; FPS: {"durationSeconds":8.01,"frames":316,"fpsMin":13.94,"fpsAverage":59.75,"fpsMax":60.02}. |

## Critério de não-falso-positivo

O player só é marcado como PASS se encerrar com código 0 e sem padrões de erro fatal no log. Timeout, licença, instância aberta ou player que permanece executando são BLOCKED, nunca PASS.

## Pendências observadas

- A cobertura automatizada de contratos cobre produção, replay, save/load, seleção, NavMesh e combate/cerco em EditMode/PlayMode; ela não substitui um playtest humano de interface.
- O smoke PlayMode precisa concluir o marcador final dos três cenários para fechar a evidência de reload; uma execução interrompida no cenário 3 permanece não aprovada.
- O player Windows é executado fora do Editor em 1280x720 por 8 s; o probe limita a 60 FPS, grava média/mínimo/máximo e encerra o processo. Isso é uma medição automatizada, não substitui playtest humano de interface.
