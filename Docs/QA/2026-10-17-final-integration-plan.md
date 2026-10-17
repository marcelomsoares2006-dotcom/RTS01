# Plano e relatório de integração final — 2026-10-17

## Escopo

Auditoria e implementação de testes isolados para a cadeia:

`produção militar -> unidade avançada -> combate/cerco -> save/load -> Ancient/Classical/Medieval/Imperial`.

Restrições respeitadas:

- nenhuma alteração em scripts de gameplay, prefabs, FBX ou cenas persistentes;
- código novo somente em `Assets/Tests/Editor/FinalIntegration` e `Assets/Editor/FinalIntegration`;
- este documento é o único artefato adicional em `Docs/QA`;
- dados de teste são criados em memória ou lidos de `Resources`; saves temporários são removidos ao final.

## Auditoria prévia

Foram localizados e considerados os harnesses e testes existentes de produção, fila/save, replay de construção, progressão de eras, combate, unidades avançadas e cerco, além do smoke existente em `Assets/Editor/EraImperialSmokeTests.cs`. A matriz final foi mantida separada para não duplicar o smoke de cena nem depender de prefabs, NavMesh, FBX ou UI.

## Implementação

Arquivos adicionados:

- `Assets/Tests/Editor/FinalIntegration/FinalIntegrationFixtures.cs`
- `Assets/Tests/Editor/FinalIntegration/FinalIntegrationEditModeTests.cs`
- `Assets/Editor/FinalIntegration/FinalIntegrationPlayModeHarness.cs`

### EditMode

`FinalIntegrationEditModeTests` contém quatro casos isolados:

1. `MilitaryCatalog_DeclaresRuntimeReadyProducersAndAdvancedOrSiegeProducts`
   verifica o catálogo real de `barracks`, `archery_range`, `stable` e `siege_workshop`, incluindo produtos avançados/cerco e contratos de era.
2. `MilitaryProduction_ResolvesQueuesAndCompletesAdvancedAndSiegeProducts`
   resolve entradas do catálogo e executa filas para `combat_mage`, `paladin_healer`, `crossbow` e `catapult`.
3. `AdvancedUnits_ExposeAllEras_AndCombatAndSiegeResolveFromProducedCodes`
   valida Mage, Paladin, Catapult e Crossbow nas quatro eras, ataque/projétil, armadura e impacto direto/radial de cerco sem atingir aliado.
4. `IntegratedMilitarySiege_SaveLoadRestoresQueueReplayAndImperialEra`
   captura/restaura fila, replay, produção de catapulta e avanço até Imperial através de `SaveFileStore`.

### PlayMode

`FinalIntegrationPlayModeHarness.Run` cria uma cena vazia descartável, entra em PlayMode, executa avanço das três transições até Imperial, produção de catapulta, impacto de cerco e save/load do payload de replay, e encerra o processo. Não instancia prefabs ou assets de gameplay.

## Execução e evidência

### EditMode — executado

Execução real em Unity `6000.6.3f1`, em 17/10/2026:

- processo: exit code `0`;
- resultado: `Passed`;
- `PASSED=4`, `FAILED=0`, `SKIPPED=0`, `INCONCLUSIVE=0`;
- quatro testes da matriz finalizaram como `Passed`;
- evidência XML: `%TEMP%\EraImperial-FinalIntegration-EditMode-test3.xml`;
- log: `%TEMP%\EraImperial-FinalIntegration-EditMode-test3.log`.

O XML e o log permaneceram em `%TEMP%` para não gravar artefatos fora dos caminhos autorizados.

### PlayMode — executado, mas bloqueado para aceitação limpa

O runner foi executado em Unity e o log registrou o marcador da cadeia:

`FINAL_INTEGRATION_PLAYMODE_PASS: production -> advanced unit -> siege -> save/load -> imperial`

Porém, a inicialização automática existente de `DebugConsole` em uma cena vazia produziu um `NullReferenceException` preexistente em:

- `Assets/Scripts/Managers/EventManager.cs:48`;
- `Assets/Scripts/DebugConsole/DebugConsole.cs:271` e `:35`.

O processo terminou com exit code `0` porque o cenário isolado alcançou o marcador e encerrou o Editor, mas a execução não é limpa. Portanto, este relatório **não declara PASS de PlayMode**: o status é `EXECUTADO / BLOQUEADO (erro runtime preexistente)`. Corrigir esse erro exigiria alterar gameplay/infraestrutura fora do escopo autorizado.

Log PlayMode: `%TEMP%\EraImperial-FinalIntegration-PlayMode.log`.

## Blockers e decisão

- O bloqueio inicial de licenciamento foi resolvido para permitir a execução, usando o Licensing Client já instalado. Não há processo Unity ou Licensing Client pendente ao finalizar.
- O blocker atual é o `NullReferenceException` de `DebugConsole/EventManager` durante o bootstrap de PlayMode.
- A matriz EditMode está executada e aprovada pelos resultados registrados.
- A matriz PlayMode foi executada, mas permanece inconclusiva/bloqueada para aceitação devido ao erro runtime; o marcador sozinho não foi tratado como PASS.
- Não há declaração de PASS global/release. Para fechar a aceitação completa, é necessário corrigir o erro preexistente fora destes diretórios e repetir o PlayMode.

## Reprodução

EditMode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe' -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath 'C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido' -runTests -testPlatform EditMode -testFilter FinalIntegrationEditModeTests -testResults "$env:TEMP\EraImperial-FinalIntegration-EditMode.xml" -logFile "$env:TEMP\EraImperial-FinalIntegration-EditMode.log"
```

PlayMode harness:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe' -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath 'C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido' -executeMethod FinalIntegrationPlayModeHarness.Run -logFile "$env:TEMP\EraImperial-FinalIntegration-PlayMode.log"
```

Ao avaliar PlayMode, o log deve ser inspecionado também quanto a `Exception`/`Error`; o marcador de cadeia não substitui essa verificação.
