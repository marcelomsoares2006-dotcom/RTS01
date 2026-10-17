# Matriz PlayMode final — produção militar, eras e cerco

Data: 2026-10-17  
Projeto: `RTS01-Corrigido`  
Unity esperado: `6000.6.3f1`  
Harness reutilizado: `Assets/Editor/FinalIntegration/FinalIntegrationPlayModeHarness.cs`

## Resultado executivo

**Aceitação PlayMode: BLOCKED.** A execução válida chegou a inicializar o Unity, mas não conseguiu obter uma sessão estável do Licensing Client. O harness não alcançou o método de cenário e não produziu marcador `FINAL_INTEGRATION_PLAYMODE_PASS` nem `FINAL_INTEGRATION_PLAYMODE_FAIL`.

Como o bloqueio ocorreu antes do runtime do cenário, os itens funcionais abaixo são `BLOCKED`, e não `FAIL`. Não há evidência nesta rodada para declarar PASS ou FAIL de gameplay.

## Matriz

| ID | Cenário | Cadeia exercitada pelo harness | Resultado | Evidência |
|---|---|---|---|---|
| PM-01 | Produção militar | Carregar catálogo, resolver `siege_workshop/catapult`, enfileirar e concluir a produção | **BLOCKED** | Unity bloqueado na inicialização do licenciamento antes de executar `RunScenario()` |
| PM-02 | Avanço das quatro eras | `Ancient -> Classical -> Medieval -> Imperial` e sincronização de `AdvancedEraState` | **BLOCKED** | Nenhum marcador de avanço foi emitido; o processo permaneceu aguardando/reconectando ao Licensing Client |
| PM-03 | Cerco | Disparo Imperial de catapulta, impacto direto e dano radial | **BLOCKED** | Nenhum marcador de cerco foi emitido; o runtime do harness não iniciou |
| PM-04 | Integração final/save-load | Persistir e restaurar era Imperial, unidade produzida e replay | **BLOCKED** | Harness não alcançou a etapa de save/load |

## Execução

Foi feita uma tentativa preliminar com argumentos mal citados pelo PowerShell; ela foi descartada porque o Unity recebeu um `projectPath` concatenado incorretamente e saiu antes de abrir o projeto. A execução válida foi repetida com o caminho devidamente citado:

```text
Unity.exe -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath "C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido" -executeMethod FinalIntegrationPlayModeHarness.Run -logFile "%TEMP%\EraImperial-final-playmode-20261017.log"
```

Na execução válida:

- Unity PID: `18824`;
- início observado: `2026-10-17 18:53:12` BRT;
- Unity abriu o projeto corretamente;
- a inicialização atingiu o carregamento de assemblies, mas não entrou no cenário PlayMode;
- o log bruto foi mantido temporariamente em `%TEMP%\EraImperial-final-playmode-20261017.log`; as linhas relevantes estão preservadas em `TestResults/final-playtest-matrix.txt`;
- não foram executados reparos do Licensing Client, encerramentos forçados ou alterações de scripts de gameplay.

## Critério de fechamento

Esta matriz só pode ser aceita como PASS quando uma nova execução do mesmo harness registrar `FINAL_INTEGRATION_PLAYMODE_PASS` e o log não contiver falhas de licenciamento, exceções ou erros de runtime. O bloqueio atual requer sessão/licença Unity estável; depois disso, repetir a execução e substituir os status desta matriz pelos resultados observados.

## Escopo de alterações

Nesta rodada foram escritos somente:

- `Docs/QA/2026-10-17-final-playtest-matrix.md`;
- `TestResults/final-playtest-matrix.txt`.

Nenhum script de gameplay, prefab, cena, FBX, configuração de produção ou harness foi editado.
