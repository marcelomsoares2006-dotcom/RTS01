# Rechecagem de validação Unity — 2026-10-15

## Resultado

- `Validate-FoundationAssets.ps1 -ReportOnly`: **PASS** — as nove fundações continuam com modelos autorais prontos para revisão Unity.
- `ValidateMilitaryUnitAssets.mjs`: **PASS** — arqueiro, cavaleiro e aríete.
- `ValidateMilitaryUnitVisualAssets.mjs`: **PASS** — contagens de vértices, triângulos e materiais válidas.
- `git diff --check`: **PASS**.
- `Tools/Validate-Project.ps1 -Build`: **BLOQUEADO**, não reprovado por código. O Unity batch foi iniciado, perdeu a conexão IPC com `LicenseClient-User` e excedeu o limite de 300 s antes de concluir o EditMode.

## Evidência

O log `TestResults/unit-tests.log` registra:

```text
[Licensing::IpcConnector] Connection to channel LicenseClient-User refused
[Licensing::Module] Successfully launched the LicensingClient
```

Como o Test Runner não chegou a produzir `unit-tests.xml` novo, os resultados Unity anteriores permanecem apenas como baseline histórico; esta rodada não os substitui.

## Próximo passo obrigatório

1. Abrir o Unity Hub com a conta licenciada e confirmar visualmente que a licença de estudante está ativa.
2. Fechar qualquer instância do Editor antes de executar `Tools/Validate-Project.ps1 -Build`.
3. Reexecutar EditMode, PlayMode e build Windows numa sessão limpa.
4. Só depois registrar o resultado final de gameplay, FPS e revisão visual/UV.

Não foram encerrados processos à força nem alterados assets por especulação.
