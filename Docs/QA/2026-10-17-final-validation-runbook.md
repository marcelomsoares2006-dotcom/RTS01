# Runbook de validação final — 2026-10-17

Projeto: `RTS01-Corrigido`  
Unity esperado: `6000.6.3f1`  
Escopo: validação final sem alterar código/ativos de produção.

## Resultado da preparação

Os gates editor-free foram executados em modo somente leitura:

- `Tools/Validate-EditorFree.ps1`: PASS — 5 cenas de build, 43 contratos, 11 entradas jogáveis e 1.635 assets indexados. O preflight de release também passou: 48 pacotes bloqueados e 374 arquivos `.meta` de scripts.
- `Tools/Validate-FoundationAssets.ps1 -ReportOnly`: PASS — os nove tipos de fundação estão em estado `model-authored` e prontos para revisão no Unity.
- `node Tools/ValidateMilitaryUnitVisualAssets.mjs`: PASS — `archer` (104 vértices/152 triângulos), `horseman` (176/268) e `battering_ram` (104/160), dentro dos budgets.

Não existe `Tools/Validate-MilitaryUnitVisualAssets.ps1`. O validador visual existente é o `.mjs` acima. Ele valida fontes OBJ/MTL e métricas; não substitui a validação de importação, prefab e runtime no Unity.

O worktree já estava sujo antes desta preparação, com alterações documentais/auditorias e arquivos `.blend1`/`.meta` não rastreados. Não fazer `reset`, `checkout`, limpeza ou remoção para obter uma árvore limpa.

## Sequência segura

### 1. Registrar o baseline e confirmar ausência de bloqueios

Executar somente consultas antes de abrir qualquer batch:

```powershell
$names = @('Unity','Unity.Licensing.Client','UnityHub','UnityCrashHandler64','AssetImportWorker')
Get-Process -Name $names -ErrorAction SilentlyContinue |
  Select-Object ProcessName, Id, Responding, StartTime, Path

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match '^(Unity|UnityHub|Unity.Licensing.Client|UnityCrashHandler|AssetImportWorker)' } |
  Select-Object Name, ProcessId, ParentProcessId, ExecutablePath, CommandLine

Test-Path -LiteralPath '.\Temp\UnityLockfile'
Test-Path -LiteralPath '.\Library\UnityLockfile'
git status --short
```

Nesta preparação não havia processos Unity/Hub/Licensing/AssetImportWorker e os dois lockfiles não existiam. `Library\Bee`, `Library\PackageManager` e `Logs` existiam, o que é normal e não prova que o Editor esteja aberto.

Bloqueadores a tratar antes do batch:

- `Unity.exe` aberto neste projeto: `Validate-Project.ps1` tenta identificar o `projectPath` e recusa continuar.
- Qualquer `Unity.exe` quando a linha de comando não puder ser inspecionada: o fallback do script também recusa continuar por segurança.
- `AssetImportWorker`, `UnityCrashHandler64` ou um segundo batch Unity concorrente: podem disputar `Library`, importação e logs.
- `Temp\UnityLockfile` ou `Library\UnityLockfile`: indicam sessão Unity ainda ativa ou encerramento incompleto.
- `Unity.Licensing.Client.exe` ativo mas não inicializado/autenticado: pode fazer o batch sair antes do Test Runner.
- `UnityHub.exe` não costuma manter o lock do projeto sozinho, mas pode iniciar outro Editor ou disputar a sessão de login; não iniciar ações concorrentes durante o batch.

Não encerrar nenhum desses processos automaticamente nesta frente. Se houver bloqueio, registrar nome, PID, caminho do projeto e horário e aguardar a sessão responsável terminar ou pedir autorização explícita para intervenção.

### 2. Repetir os gates sem Unity

Na raiz do projeto, em uma janela PowerShell nova:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-EditorFree.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-FoundationAssets.ps1 -ReportOnly
node .\Tools\ValidateMilitaryUnitVisualAssets.mjs
```

`Validate-EditorFree.ps1` chama `Validate-Release.ps1` sem `-WithUnity` e também chama `Tools/ValidateMilitaryUnitAssets.mjs`, que verifica prefabs, `CharacterData`, GUIDs e bindings de animação das três unidades de produção. O `.mjs` visual verifica separadamente OBJ/MTL, contagens, materiais e budgets.

Se o gate de fundações passar em `-ReportOnly`, repetir sem esse parâmetro para transformar qualquer issue em falha:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-FoundationAssets.ps1
```

### 3. Licenciamento — somente se necessário e com autorização

`Tools/Repair-UnityLicensingClient.ps1` não é um check somente leitura: nas linhas 13–16 ele procura `Unity.Licensing.Client` e executa `Stop-Process -Force` em cada instância encontrada; depois inicia o cliente novamente e apenas confirma que o processo voltou. Ele não confirma por si só que a conta está autenticada.

Portanto, sob a política desta frente (não matar processos), não executar esse reparo automaticamente. Primeiro abrir/confirmar a conta no Unity Hub, quando necessário, e fechar o Editor do projeto. Só usar o reparador após autorização explícita para reiniciar o cliente, sem outros processos Unity concorrentes:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Repair-UnityLicensingClient.ps1 -WaitSeconds 8
```

Após o reparo, confirmar manualmente no Hub que a licença está válida e repetir a consulta da etapa 1. Se o cliente já estiver correto, não reiniciá-lo.

### 4. Unity batch final

Com o projeto fechado no Editor, sem lockfile e com licenciamento válido:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tools\Validate-Project.ps1 -Build
```

O script executa, nesta ordem:

1. todos os testes EditMode e exige resultado `Passed`, sem falhas, skips ou inconclusivos;
2. `EraImperialSmokeTests.Run` para os cenários Play Mode;
3. `EraImperialValidation.BuildWindows` quando `-Build` é usado.

Ele escreve logs/resultados em `TestResults` e pode atualizar artefatos gerados pelo Unity, como `Library`, `Temp` e `Builds/Windows`. Isso não deve ser confundido com alteração deliberada de código/ativos de produção.

Limites implementados pelo script: 300 s para cada etapa padrão e 1.200 s para o build. Em timeout, o próprio script tenta `Stop-Process` no processo Unity que ele criou (linhas 42–45 de `Validate-Project.ps1`). Não emitir um `Stop-Process` manual; se a regra de não encerrar processos também se aplicar a esse fallback interno, não iniciar o batch final sem antes alterar/aprovar essa política.

Não usar `-Migrate` nesta validação final: migração é uma operação separada e não é necessária para o gate já preparado.

### 5. Conferência pós-batch

Somente depois do término, consultar:

```powershell
$names = @('Unity','Unity.Licensing.Client','UnityHub','UnityCrashHandler64','AssetImportWorker')
Get-Process -Name $names -ErrorAction SilentlyContinue |
  Select-Object ProcessName, Id, Responding, StartTime, Path

Get-Item -LiteralPath '.\TestResults\unit-tests.xml', '.\TestResults\playmode-smoke.txt', '.\TestResults\windows-build.txt' -ErrorAction SilentlyContinue |
  Select-Object FullName, Length, LastWriteTime

git status --short
```

Aceitar o resultado somente com evidência dos três estágios, ausência de erro no log do Unity e comparação do `git status` com o baseline registrado na etapa 1. Preservar os resultados e não remover logs, builds ou caches nesta frente.

## Observações de segurança

- Nenhum comando destrutivo foi executado na preparação deste documento.
- Nenhum processo foi encerrado.
- Não alterar scripts, prefabs, FBX/OBJ/MTL, cenas ou dados de produção para fazer um gate passar.
- Falha de licenciamento, lock ou timeout deve ser registrada como bloqueio operacional; não deve ser mascarada por limpeza de `Library`, remoção de lockfiles ou reinício forçado de processos.
