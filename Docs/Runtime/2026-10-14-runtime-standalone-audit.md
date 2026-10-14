# Auditoria independente de runtime/standalone

Data: 2026-10-14T20:39:38.5198440-03:00  
Unity: `6000.6.3f1`  
Escopo: partida limpa, produÃ§Ã£o/contratos, save/load, replay, seleÃ§Ã£o, NavMesh, combate/cerco e player Windows.

## Resultado

- PASS: 2
- FAIL: 1
- BLOCKED/indeterminado: 1

| ID | Ãrea | Estado | EvidÃªncia | Detalhe |
|---|---|---|---|---|
| editmode | contratos e smoke de dados | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\unit-tests.xml` | EditMode: 390/390 aprovados. |
| playmode | partida, navegaÃ§Ã£o, render e save/load | FAIL | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\playmode-smoke.txt` | A execuÃ§Ã£o mais recente do smoke nÃ£o contÃ©m o marcador final limpo; execuÃ§Ãµes anteriores nÃ£o sÃ£o reutilizadas. |
| build | player Windows | PASS | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\windows-build.txt` | Build Windows reporta Succeeded e zero erros. |
| standalone-start | execuÃ§Ã£o fora do Editor | BLOCKED | `C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido\TestResults\standalone-runtime.log` | Player nÃ£o encerrou em 45 s com -quit; nÃ£o Ã© considerado smoke aprovado. |

## CritÃ©rio de nÃ£o-falso-positivo

O player sÃ³ Ã© marcado como PASS se encerrar com cÃ³digo 0 e sem padrÃµes de erro fatal no log. Timeout, licenÃ§a, instÃ¢ncia aberta ou player que permanece executando sÃ£o BLOCKED, nunca PASS.

## PendÃªncias observadas

- A cobertura automatizada de contratos cobre produÃ§Ã£o, replay, save/load, seleÃ§Ã£o, NavMesh e combate/cerco em EditMode/PlayMode; ela nÃ£o substitui um playtest humano de interface.
- O smoke PlayMode precisa concluir o marcador final dos trÃªs cenÃ¡rios para fechar a evidÃªncia de reload; uma execuÃ§Ã£o interrompida no cenÃ¡rio 3 permanece nÃ£o aprovada.
- O player Windows precisa ser executado em uma sessÃ£o limpa fora do Editor; `-batchmode -nographics -quit` Ã© apenas um smoke de inicializaÃ§Ã£o, nÃ£o prova interaÃ§Ã£o visual.
