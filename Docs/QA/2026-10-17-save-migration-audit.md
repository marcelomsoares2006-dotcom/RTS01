# Auditoria do save/load legado e plano de migração — 2026-10-17

Projeto: `RTS01-Corrigido`  
Unity esperado: `6000.6.3f1`  
Escopo: auditoria somente leitura do caminho `BinaryFormatter`, seus contratos atuais e dos testes necessários para uma migração segura. Nenhum código ou asset de produção foi alterado por esta auditoria.

## Conclusão executiva

O save/load funcional básico está coberto: o smoke PlayMode retido registra 3/3 cenários, incluindo reload de Map1, e os testes atuais cobrem o envelope, fila de produção, replay/era e regras de construção. Isso não fecha a migração.

O risco de release permanece **alto** por quatro motivos:

1. `SaveFileStore` possui envelope e recuperação por `.bak`, mas o payload novo e o legado continuam sendo serializados/desserializados por `BinaryFormatter`.
2. O desserializador não usa binder allowlist; qualquer arquivo que consiga chegar ao caminho de load é uma entrada não confiável para um mecanismo conhecido por executar lógica durante a desserialização.
3. O envelope `RTSAVE` versão 1 não é um schema de save: ele apenas embrulha um grafo dependente de nomes de tipos, assemblies e campos.
4. O filtro de `BinarySerializable` não inclui os contratos `construction` e `maintenance` de `GamePlayerUnitData`. `DataHandler` captura esses objetos, mas o `GetObjectData` atual os ignora; portanto não se pode afirmar que um save legado preserva construção avançada ou manutenção em andamento.

Recomendação: migrar em duas vias, mantendo um leitor legado temporário e controlado somente para conversão, e introduzir um formato explícito versionado para novas gravações. Não substituir o `BinaryFormatter` diretamente no runtime antes de existir um corpus de fixtures, validação semântica e rollback comprovado.

## Fluxo auditado

### Save/load de partida

1. `DataHandler.LoadGameData()` carrega os `GameParameters`, incluindo `Games/<gameUid>/PlayerParameters.data`, e depois chama `GameData.Load()` (`Assets/Scripts/Tools/DataHandler.cs:54-80`).
2. `GameData.Load/Save` delega para `BinarySerializable.Load/Save` (`Assets/Scripts/GameData.cs:135-145`).
3. `SaveFileStore.Load` aceita o envelope com magic `RTSAVE` ou tenta ler o arquivo sem envelope como legado (`Assets/Scripts/Tools/SaveFileStore.cs:53-103`).
4. Em ambos os casos, o payload termina em `new BinaryFormatter().Deserialize(...)`; a gravação também chama `BinaryFormatter.Serialize(...)` (`Assets/Scripts/Tools/SaveFileStore.cs:113-124`).
5. `GameData` é salvo em `Application.persistentDataPath/<Data_Dev|Data>/Games/<gameUid>/GameData.data` (`Assets/Scripts/GameData.cs:118-126`).

### Parâmetros de jogador

`BinarySerializableScriptableObject` usa o mesmo `SaveFileStore` para os arquivos `.data` de parâmetros (`Assets/Scripts/Tools/BinarySerializableScriptableObject.cs:32-92`). A migração precisa cobrir tanto `GameData.data` quanto `PlayerParameters.data` e os demais parâmetros salvos na pasta do jogo; migrar apenas o arquivo principal deixaria o carregamento híbrido.

## Achados e risco

| ID | Achado | Evidência | Risco |
|---|---|---|---|
| BF-01 | `BinaryFormatter` continua no caminho de gravação e leitura, inclusive para arquivos embrulhados na versão 1. | `SaveFileStore.cs:25-40`, `:113-124` | **Alto — segurança e compatibilidade.** O envelope não remove execução durante a desserialização nem a dependência de tipos CLR. |
| BF-02 | Não há `SerializationBinder`, allowlist de tipos, checksum ou autenticação. | `SaveFileStore.cs:71-110`, `:120-124` | **Alto — segurança/corrupção.** Um arquivo alterado pode provocar tipos inesperados, exceções ou custo de memória antes de o resultado ser convertido para `T`. `as T` não torna a desserialização segura. |
| BF-03 | A versão 1 é apenas de transporte: magic, inteiro de versão, tamanho e payload. O payload continua dependente de nomes de tipos, assemblies e campos. | `SaveFileStore.cs:13-15`, `:89-101` | **Alto — migração falsa.** Incrementar `FormatVersion` sem um DTO/schema não cria compatibilidade entre versões do jogo. |
| BF-04 | O comprimento do payload é limitado, mas não há verificação explícita de leitura exata nem de bytes sobrando; não há teste para truncamento, cabeçalho inválido, versão desconhecida ou corrupção. | `SaveFileStore.cs:91-95` | **Médio/alto — integridade.** Arquivos incompletos ou adulterados podem falhar somente dentro do desserializador e cair no caminho genérico de “não carregou”. |
| BF-05 | O fallback para `.bak` é testado, mas o arquivo corrompido/ilegível acaba retornando `null`. `DataHandler` trata `GameData == null` como partida sem save. | `SaveFileStore.cs:59-68`; `DataHandler.cs:108-115` | **Alto — perda silenciosa de progresso.** Ausência de save e save corrompido não têm resultado operacional distinto para o jogo. |
| BF-06 | `GamePlayerUnitData.construction` e `.maintenance` existem e são capturados/restaurados, mas não estão na allowlist de `BinarySerializableData`. | `GameData.cs:22-26`; `DataHandler.cs:225-235`, `:319-335`; `BinarySerializableData.cs:9-27` | **Alto — perda funcional.** `BinarySerializable.GetObjectData` ignora esses campos porque `CanSerialize` retorna falso. As regras unitárias passam, mas não provam round-trip binário desses contratos. |
| BF-07 | O desserializador customizado consulta cada chave/campo atual por nome e cada array por `__count`, sem defaults tolerantes para chaves ausentes. | `BinarySerializable.cs:20-55` | **Alto — compatibilidade legada.** Um save anterior à introdução de um campo pode falhar com `SerializationException`, em vez de receber um default controlado. Renomear tipo/campo ou mudar assembly também pode quebrar o grafo. |
| BF-08 | Os recursos e a produção usam arrays com ordem implícita (`Globals.GAME_RESOURCE_KEYS`). | `GameData.cs:14`, `:39`; `DataHandler.cs:133-152`, `:269-276` | **Médio — evolução de schema.** Adicionar/reordenar um recurso sem migração explícita pode deslocar valores. O formato novo deve usar códigos nomeados ou validar a versão da ordem. |
| BF-09 | A entrada de ScriptableObjects usa `Dictionary<string, object>` e reflexão sobre campos atuais. | `BinarySerializableScriptableObject.cs:46-50`, `:71-92`; `BinarySerializableData.cs:29-45` | **Médio/alto — superfície adicional.** A migração precisa tratar parâmetros e dicionários, não somente o grafo de `GameData`, e deve rejeitar chaves/tipos desconhecidos de forma determinística. |
| BF-10 | A cobertura atual comprova contratos isolados, não uma matriz de fixtures legados e nem todos os estados atuais. | `Assets/Tests/Editor/SaveFileStoreTests.cs`; `Assets/Tests/Editor/DataHandlerProductionQueueTests.cs`; `TestResults/unit-tests.xml` | **Médio — evidência insuficiente.** Não há fixture de arquivo cru anterior ao envelope, de save com campos ausentes, de construção/manutenção persistidas ou de corrupção controlada. |

### Observação sobre BF-06

Este é um achado do caminho atual, não uma recomendação para alterar o código nesta tarefa. A lista contém `BuildingProductionQueueSnapshot`, `BuildingProductionOrderSnapshot` e os tipos de replay, mas não contém `BuildingConstructionSaveSnapshot` nem `BuildingMaintenanceRuntimeSnapshot`. Por isso:

- o teste `DataHandlerProductionQueueTests.GameDataBinaryRoundTripCarriesProductionQueueSnapshot` é válido para a fila, mas não cobre construção/manutenção;
- `DataHandler` ainda mantém os escalares legados `constructionHP`, `buildingLifecycle` e `constructorIds`, o que explica por que parte do fluxo básico pode passar;
- a migração deve distinguir “campo ausente no save antigo” de “campo capturado em memória, mas omitido pelo escritor atual”. Não é seguro inventar `RuntimeReady` para preencher essa lacuna.

## Contratos que a migração deve preservar

O formato novo deve ser uma representação explícita de estado, sem prefab, `BuildingData`, `GameObject`, `Transform` ou referência de cena. Deve cobrir:

- identidade da partida e versão do schema;
- jogadores, recursos, unidades, posição, vida, nível, dano e alcance;
- para construções, os escalares legados e, quando presentes, `BuildingConstructionSaveSnapshot`, incluindo código, proprietário, progresso, lifecycle, trabalhadores, reservas e estágio de conteúdo;
- `BuildingMaintenanceRuntimeSnapshot`, incluindo saúde, lifecycle, operação e progresso, quando realmente disponível;
- `BuildingProductionQueueSnapshot`, seus pedidos, custos, tempo, população, era e tecnologias;
- economia (`stockpile`, população, capacidade e coletores);
- tecnologias desbloqueadas e `ConstructionReplayCommand`/`ConstructionReplaySnapshot`;
- parâmetros de jogador, nome e cor;
- posição da câmera.

As regras já existentes devem continuar sendo a autoridade depois da conversão, não ser duplicadas de modo divergente:

- `BuildingConstructionSaveRules.Validate/CanRestoreRuntime` para construção;
- `BuildingProductionSaveRules.Restore` e a validação de catálogo/era/tecnologia para fila;
- `ConstructionReplayRuntime.Restore` para replay/era;
- validações adicionais de faixa, finitude, enum, identidade, contagem de arrays e recursos antes de tocar o runtime.

Para um save antigo que não possui o snapshot aninhado, a conversão deve manter `construction == null` e conservar os escalares legados. Ela não deve marcar conteúdo como `RuntimeReady` sem resolver e validar o asset atual. Da mesma forma, uma fila ou manutenção ausente deve permanecer ausente, com diagnóstico explícito, em vez de ser fabricada.

## Migração segura proposta

### Fase 0 — inventário e fixtures

Antes de mudar o runtime:

1. Inventariar, por instalação e por versão, `GameData.data`, `PlayerParameters.data` e demais `.data`; registrar tamanho, hash SHA-256, envelope/raw, data e resultado de leitura.
2. Copiar os arquivos para uma área de migração; nunca converter no lugar e nunca apagar o original.
3. Montar um corpus mínimo: raw legado sem envelope, envelope v1, save com replay/era, save com fila não vazia, save antigo sem campos novos, arquivo truncado, cabeçalho inválido, versão desconhecida, payload superdimensionado e backup interrompido.
4. Congelar uma representação semântica de cada fixture. A comparação deve ignorar bytes/ordem de serialização e verificar campos e invariantes.

### Fase 1 — leitor legado controlado

O leitor legado deve existir somente como ponte de conversão e ser removível após a janela de compatibilidade:

- reconhecer raw e envelope v1, com limite de tamanho, leitura exata e classificação de erro;
- executar em ferramenta/processo isolado quando possível; no mínimo usar binder allowlist com os tipos e assemblies necessários ao corpus, recusando qualquer tipo não catalogado;
- desserializar para o grafo legado conhecido apenas uma vez e imediatamente mapear para DTOs explícitos; nunca expor o grafo arbitrário ao restante do jogo;
- separar `missing`, `corrupt`, `unsupported`, `converted-with-warning` e `converted`;
- produzir log por arquivo com hash de entrada, versão detectada, avisos, hash do documento convertido e caminho do backup;
- abortar aquele arquivo sem mutar o save original se qualquer campo obrigatório, enum, tamanho, dono, código ou referência necessária for inválido.

### Fase 2 — formato explícito v2

Recomendação: documento UTF-8 com DTOs explícitos e campos estáveis, dentro de um envelope próprio. JSON é adequado como formato de referência legível; o serializador concreto deve suportar listas, enums, nullable e validação estrita. `JsonUtility` não deve ser escolhido automaticamente, porque os contratos atuais usam listas, dicionário de parâmetros e estado aninhado que exigem controle de schema.

O envelope v2 deve conter, no mínimo:

```text
magic = RTSAVE
formatVersion = 2
schemaVersion = 2
payloadLength
payloadHash
payload = documento DTO explícito
```

O documento deve registrar `gameVersion`, `schemaVersion`, origem da conversão e, se necessário, a revisão do catálogo. O hash detecta truncamento/corrupção acidental; não deve ser tratado como autenticação contra um atacante que pode reescrever o arquivo.

Durante a transição:

- o escritor novo grava somente v2;
- o leitor tenta v2, depois v1/raw somente pelo caminho legado controlado;
- a conversão escreve um novo arquivo atômico, preservando o original como `.legacy`/backup com hash;
- falha de conversão não transforma um save corrompido em “nova partida”;
- a promoção para v2 só ocorre depois de validar o DTO inteiro e de confirmar que o reload semântico reproduz o estado.

### Fase 3 — projeção nos contratos atuais

O conversor deve projetar para os contratos existentes, sem modificar gameplay nesta etapa:

1. Copiar os escalares de unidade e preservar a ordem atual somente quando a versão legada a identificar; no v2, preferir recursos nomeados por código.
2. Preservar os campos aninhados válidos. Para saves antigos sem eles, manter o fallback escalar documentado.
3. Validar construção, catálogo, owner e estágio antes da restauração; não criar prefab, mesh ou `BuildingData` a partir do arquivo.
4. Validar produção contra catálogo, era, tecnologias, custos e população antes de substituir uma fila.
5. Restaurar replay/era e somente depois reconstruir dependências de cena.
6. Manter parâmetros de jogador no mesmo identificador de partida; não misturar arquivos de `Data_Dev` e `Data`.

### Fase 4 — cutover e remoção

Após a matriz de compatibilidade passar, o runtime pode deixar de produzir BinaryFormatter. O leitor v1/raw deve permanecer por uma janela de release definida, em modo somente leitura/conversão. Só depois de uma política explícita de retenção e backup ele poderá ser removido. Nenhuma limpeza automática de `.legacy` deve ocorrer como parte do primeiro cutover.

## Testes necessários e critérios de saída

### Corpus e compatibilidade

- Carregar raw legado anterior ao envelope, envelope v1 e v2; verificar o mesmo fingerprint semântico.
- Carregar fixtures que não possuem `construction`, `maintenance`, `productionQueue`, replay ou tecnologias e confirmar defaults controlados, sem exceção.
- Carregar arquivos com campos extras, campos desconhecidos, tipo desconhecido, assembly alterado, enum inválido, arrays nulos, arrays longos e contagens incompatíveis.
- Repetir conversão duas vezes e exigir idempotência do documento v2 e dos hashes semânticos.
- Rodar a matriz no Editor e no player Windows, em locale diferente, com os mesmos contratos e caminhos de persistência.

### Round-trip de contratos

- `GameData` completo com vários jogadores, recursos, unidades e câmera.
- Construção em blueprint, under construction e operacional, com trabalhadores e reserva; verificar tanto snapshot quanto fallback escalar.
- Manutenção idle, repair e demolition em andamento, incluindo custo reservado e progresso.
- Fila de produção não vazia, custo/população, era e tecnologia; provar que custo adulterado é rejeitado antes de mutar a fila.
- Economia com stockpile, população/capacidade e coletores.
- Progressão Ancient → Classical → Medieval → Imperial, tecnologias e replay; reload após cada marco.
- `PlayerParameters` com nomes, cores e todos os campos selecionados.

### Segurança e integridade

- Binder/allowlist rejeitando cada tipo fora do corpus antes de expor o objeto ao gameplay.
- Arquivo truncado no cabeçalho, no tamanho e no payload; hash divergente; bytes sobrando; versão desconhecida; payload acima do limite.
- Arquivo principal corrompido com `.bak` válido e `.bak` também inválido; o resultado deve distinguir recuperação de ausência de save.
- Falha injetada entre flush, rename e substituição; confirmar que sempre existe um arquivo recuperável e que nenhum arquivo parcial vira save ativo.
- Limites de memória/tempo para o tamanho máximo aceito, sem usar fixtures maliciosas permanentes no repositório.

### Runtime integrado

- Nova partida Map1 e Map2, salvar, sair, recarregar e comparar fingerprint de unidades/recursos/tecnologias.
- Partida com construção em progresso, manutenção em progresso e fila militar não vazia, usando todos os providers reais, salvar/recarregar e confirmar reanexação.
- Recarregar um save legado convertido sem inventar assets, sem duplicar reservas/população e sem alterar a ordem do replay.
- Executar `Tools/Validate-EditorFree.ps1`, todos os EditMode, smoke PlayMode e build/player Windows após a implementação da migração; os resultados atuais não devem ser reutilizados como prova de uma mudança futura.

### Critérios de saída

A migração só deve ser declarada aprovada quando:

- todas as fixtures suportadas forem convertidas sem perda semântica não documentada;
- arquivos inválidos forem classificados e não tratados como partida nova;
- nenhum tipo fora da allowlist for desserializado pelo leitor legado;
- novos saves forem v2, verificáveis e atomicamente gravados;
- o arquivo legado original permanecer recuperável;
- construção, manutenção, produção, economia e replay tiverem round-trip integrado comprovado;
- o fallback legado puder ser removido sem quebrar a matriz de versões suportadas.

## Evidência existente e limites desta auditoria

- `TestResults/unit-tests.xml` registra `SaveFileStoreTests` 2/2, `DataHandlerProductionQueueTests` 3/3, `AdvancedEraTechnologySaveLoadTests` 1/1, contratos de construção 5/5 e binding de manutenção 4/4.
- `TestResults/playmode-smoke.txt` registra 3/3 cenários e reload de Map1 sem erros runtime.
- Esses testes são evidência de comportamento atual específico, não de compatibilidade histórica, segurança de desserialização, construção/manutenção no grafo binário ou migração v1 → v2.
- Nenhum save pessoal foi lido, convertido, copiado ou apagado nesta auditoria.
- O worktree já continha alterações não relacionadas; elas foram preservadas.

## Referências principais

- `Assets/Scripts/Tools/SaveFileStore.cs`
- `Assets/Scripts/Tools/BinarySerializable.cs`
- `Assets/Scripts/Tools/BinarySerializableData.cs`
- `Assets/Scripts/Tools/BinarySerializableScriptableObject.cs`
- `Assets/Scripts/GameData.cs`
- `Assets/Scripts/Tools/DataHandler.cs`
- `Assets/Tests/Editor/SaveFileStoreTests.cs`
- `Assets/Tests/Editor/DataHandlerProductionQueueTests.cs`
- `Docs/RELEASE_READINESS.md`
- `Docs/QA/2026-10-17-final-gap-audit.md`
