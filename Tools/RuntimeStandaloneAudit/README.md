# Auditoria Runtime/Standalone

Esta pasta é isolada para testes de runtime e do player Windows. Não altera cenas, prefabs, assets nem a lógica das unidades avançadas.

Execute com o projeto fechado:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\RuntimeStandaloneAudit\Invoke-RuntimeStandaloneAudit.ps1
```

Para um soak de 600 s, o launcher calcula automaticamente uma janela de
`600 + warm-up + 30 s`; não é necessário configurar um timeout manual:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\RuntimeStandaloneAudit\Invoke-RuntimeStandaloneAudit.ps1 -BenchmarkSeconds 600 -BenchmarkWarmupSeconds 5
```

Um valor positivo em `-StandaloneTimeoutSeconds` continua disponível como
override explícito. O timeout fixo de 45 s é usado apenas como mínimo para
smokes curtos.

O script consome as evidências existentes, tenta iniciar `Builds/Windows/EraImperial.exe` em modo headless e grava:

- `TestResults/runtime-standalone-audit.json`
- `Docs/Runtime/2026-10-14-runtime-standalone-audit.md`
- `TestResults/standalone-runtime.log`

Timeout, licença, processo já aberto e falta de interação visual são classificados como `BLOCKED`, nunca como aprovação.
