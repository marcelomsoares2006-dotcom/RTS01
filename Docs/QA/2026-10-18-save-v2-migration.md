# Migração segura dos saves para V2

Data: 2026-10-18  
Escopo: `SaveFileStore`/`BinaryFormatter` e fluxo de `GameData`

## Implementado

- `GameData.Save` agora grava exclusivamente através de `SaveMigrationV2Store`.
- `GameData.Load` agora lê o envelope V2, recupera o `.bak` quando necessário e mantém leitura legada somente através de `SaveMigrationV2LegacyReader`.
- Um save legado não é sobrescrito durante a leitura. Ele é convertido apenas em memória; a próxima gravação do jogo produz V2.
- O envelope V2 continua usando JSON explícito, hash SHA-256, gravação temporária e recuperação por backup.
- Nenhuma cena, prefab, asset visual ou save existente foi apagado ou alterado.

## Testes adicionados/verificados

- `GameDataSaveAndLoadUsesV2WithoutLegacyWriter`: round-trip pelo API público de `GameData`, confirma envelope V2 e ausência de arquivo `.legacy` criado.
- `V2WritesExplicitEnvelopeAndRoundTripsNamedState`: round-trip do documento completo, incluindo produção, construção, economia e replay.
- `LegacyReadIsCompatibleAndMigrationKeepsOriginalSeparate`: leitura legada e conversão para destino separado preservando o original.
- `RawLegacyPayloadIsAcceptedByTheSameIsolatedFallback`: compatibilidade com payload legado sem envelope.
- `V2RejectsTruncationAndHashTamperingWithoutLegacyFallback`: integridade do envelope V2.
- `V2FallsBackToBakWhenPrimaryIsInterrupted`: recuperação do backup.

## Validação executada

- `Tools/Validate-EditorFree.ps1 -SkipReleasePreflight`: passou.
- A suíte Unity EditMode foi executada após a alteração: **401/401 testes passaram**, 0 falhas, 0 ignorados. Nenhum processo foi encerrado.
- O processo batch permaneceu ativo após escrever o resultado; não foi encerrado automaticamente para respeitar a regra de não finalizar processos do usuário.
- A última evidência Unity disponível antes desta alteração registra build concluído com `Errors: 0` e `Warnings: 24`; a recompilação após a alteração deve ser executada com o Unity fechado.

## Warnings restantes conhecidos

Os avisos não pertencem ao novo escritor V2:

1. `BinaryFormatter` permanece somente no `SaveFileStore` genérico e nas pontes legadas de leitura. Ele ainda atende dados antigos e `BinarySerializableScriptableObject`; não é usado por `GameData.Save`.
2. APIs Unity obsoletas em ferramentas/auditorias de personagens e editor (`FindObjectsByType` com ordenação, `FindFirstObjectByType`, `Projector`, `AssetDatabase.ExportPackage`).
3. Variáveis não utilizadas em auditorias editoriais.
4. Alias obsoleto `BuildingEconomyContentProjection.assetStage` mantido para compatibilidade.
5. Avisos de licenciamento/token do Unity batch podem aparecer no log, mas não são warnings de serialização.

No log da execução EditMode desta alteração não houve warning de compilação nem erro de compilação. A última evidência de build Windows disponível registra `Errors: 0` e `Warnings: 24`; esses avisos são de outras áreas do projeto e devem ser recontados em um build completo posterior.
