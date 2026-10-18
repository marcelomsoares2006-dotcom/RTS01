# Auditoria independente de runtime/standalone

Data: 2026-10-18T00:20:04.9659780-03:00  
Unity: `6000.6.3f1`  
Escopo: partida limpa, produÃ§Ã£o/contratos, save/load, replay, seleÃ§Ã£o, NavMesh, combate/cerco e player Windows.

## Resultado

- PASS: 4
- FAIL: 0
- BLOCKED/indeterminado: 0

| ID | Ãrea | Estado | EvidÃªncia | Detalhe |
|---|---|---|---|---|
| editmode | contratos e smoke de dados | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\unit-tests.xml` | EditMode: 401/401 aprovados. |
| playmode | partida, navegaÃ§Ã£o, render e save/load | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\playmode-smoke.txt` | Smoke PlayMode concluiu os trÃªs cenÃ¡rios sem erro runtime. |
| build | player Windows | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\windows-build.txt` | Build Windows reporta Succeeded e zero erros. |
| standalone-start | execuÃ§Ã£o fora do Editor | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\standalone-runtime.log` | Player grÃ¡fico encerrou pelo probe com cÃ³digo 0; FPS: {"durationSeconds":8.01,"warmupSeconds":3.00,"frames":300,"fpsMin":34.83,"fpsAverage":59.87,"fpsMax":60.01}. |

## CritÃ©rio de nÃ£o-falso-positivo

O player sÃ³ Ã© marcado como PASS se encerrar com cÃ³digo 0 e sem padrÃµes de erro fatal no log. Timeout, licenÃ§a, instÃ¢ncia aberta ou player que permanece executando sÃ£o BLOCKED, nunca PASS.

## PendÃªncias observadas

- A cobertura automatizada de contratos cobre produÃ§Ã£o, replay, save/load, seleÃ§Ã£o, NavMesh e combate/cerco em EditMode/PlayMode; ela nÃ£o substitui um playtest humano de interface.
- O smoke PlayMode precisa concluir o marcador final dos trÃªs cenÃ¡rios para fechar a evidÃªncia de reload; uma execuÃ§Ã£o interrompida no cenÃ¡rio 3 permanece nÃ£o aprovada.
- O player Windows Ã© executado fora do Editor em 1280x720 por 8 s; o probe limita a 60 FPS, grava mÃ©dia/mÃ­nimo/mÃ¡ximo e encerra o processo. Isso Ã© uma mediÃ§Ã£o automatizada, nÃ£o substitui playtest humano de interface.
