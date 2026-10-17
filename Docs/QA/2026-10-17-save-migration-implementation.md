# Implementação da migração de save V2 — 2026-10-17

## Escopo

Esta entrega adiciona `Assets/Scripts/Tools/SaveMigrationV2` e os testes EditMode em `Assets/Tests/Editor/SaveMigrationV2`. `SaveFileStore`, `DataHandler`, prefabs e as demais áreas do projeto não foram alterados.

O V2 é uma ponte segura e opt-in: o runtime atual continua lendo e escrevendo o formato existente até uma etapa posterior de cutover. A API nova permite ler V2, ler um save raw/v1 compatível em memória e converter para um arquivo V2 separado.

## Formato V2

O arquivo contém:

```text
magic RTSAVE + versão 2
envelopeVersion = 2
schemaVersion = 2
payloadLength
SHA-256(payload)
payload UTF-8 JSON com DTOs explícitos
```

O payload não contém `GameObject`, prefab, `Transform`, `ScriptableObject` ou `Dictionary<string, object>`. Recursos são gravados por nome estável; construção, manutenção, fila, economia e replay têm DTOs próprios. O carregador valida limites, enums, valores finitos, população, saúde, custos e contagens antes de projetar para `GameData`.

As novas gravações usam `JsonUtility` e `BinaryWriter` apenas para o envelope. Não há chamada nova a `BinaryFormatter` na escrita.

## Compatibilidade e fallback

`SaveMigrationV2Store.Load(path)` tenta V2 primeiro. Se o arquivo não possui a assinatura V2, `SaveMigrationV2LegacyReader` chama o caminho legado existente (`BinarySerializable.Load`) para aceitar o raw anterior ao envelope e o envelope v1. O resultado é imediatamente convertido para o DTO V2 em memória; o restante da API não recebe um grafo arbitrário do desserializador.

Um V2 com assinatura válida, mas truncado, com hash divergente, versão desconhecida ou schema inválido não cai no legado. O erro é classificado como `Corrupt`, `Unsupported` ou `Invalid`, e o carregador tenta `.bak` quando existir. Ausência de arquivo continua sendo `Missing`, distinta de corrupção.

`MigrateLegacyToV2(legacyPath, destinationPath)` exige caminhos diferentes. Assim o arquivo legado permanece intacto e recuperável; a conversão não apaga nem substitui o original.

Uma segunda gravação v2 move a versão anterior para `<arquivo>.bak` e mantém
esse backup após a substituição. Se o arquivo ativo estiver truncado ou com
hash inválido, `Load` tenta esse backup antes de retornar a falha.

## Testes EditMode

- round-trip V2 com recursos nomeados, economia, construção, fila e replay;
- rejeição de adulteração de hash sem fallback indevido para BinaryFormatter;
- leitura de arquivo legado e conversão para destino separado sem alterar os bytes originais;
- recuperação de `.bak` após falha do arquivo principal.

Os testes existentes de `SaveFileStore` e `DataHandler` continuam fora do escopo e preservam a cobertura do formato atual. A integração de `DataHandler`/`GameData.Load` com o novo leitor deve ser uma etapa de cutover posterior, depois de validar fixtures reais e parâmetros de jogador; esta entrega não muda o comportamento atual do jogo.

Nesta sessão a execução batch direcionada foi tentada, mas o Unity abortou
antes da compilação porque outra instância já mantinha este projeto aberto. O
processo não foi encerrado e nenhum resultado anterior foi reutilizado como
prova desta mudança.

## Uso controlado

```csharp
SaveMigrationV2Store.Save(path, gameData);
SaveMigrationV2ReadResult result = SaveMigrationV2Store.Load(path);
SaveMigrationV2ReadResult migrated =
    SaveMigrationV2Store.MigrateLegacyToV2(legacyPath, v2Path);
```

O fallback legado deve permanecer temporário e removível. Não remover os arquivos originais nem promover V2 para o runtime principal sem uma matriz de fixtures raw/v1, parâmetros de jogador, construção/manutenção, PlayMode e player Windows.
