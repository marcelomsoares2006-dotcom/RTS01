# Release readiness — Era Imperial / RTS01-Corrigido

Auditoria realizada em 2026-10-12. Escopo: release/desempenho/saves, sem alteração no runtime compartilhado, sem commit e sem push.

## Resultado objetivo

- [x] Unity encontrado em `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`.
- [x] Build Settings contém cinco cenas habilitadas, nesta ordem: `Core`, `MainMenu`, `GameScene`, `Map1` e `Map2`.
- [x] Testes EditMode: **6/6 aprovados, 0 falhas** (`TestResults/unit-tests.xml`).
- [x] PlayMode oficial: **aprovado** (`Tools/Validate-Project.ps1`).
- [x] Build Windows: **sucedida, 0 erros, 22 avisos** (`TestResults/windows-build.txt`).
- [x] Build gerada em `Builds/Windows/EraImperial.exe`.
- [x] `Builds/` está ignorado pelo Git; o executável não entra no commit por acidente.
- [ ] Teste manual do executável fora do Editor ainda não foi considerado concluído nesta auditoria.

## Auditoria do validador

`Tools/Validate-Project.ps1` foi executado com `-Build` e passou. O fluxo atual:

1. impede a execução se detectar o projeto aberto no Unity;
2. executa EditMode e exige pelo menos três testes;
3. executa o smoke test de PlayMode;
4. opcionalmente gera a build Windows.

Limitações registradas, sem alteração no script nesta rodada:

- o caminho padrão do Unity é fixo para `6000.6.3f1`;
- o critério de unidade é mínimo (`>= 3`), não um número esperado de testes;
- não há verificação automática de tamanho de assets, Git LFS, `BinaryFormatter` ou teste de abertura do player;
- não há medição de FPS/memória com carga de unidades.

## Build Settings e desempenho

As cinco cenas de produção estão habilitadas. Não há cena exclusiva de unidade avançada no Build Settings; isso é esperado para os testes atuais, que usam prefabs/contratos dentro das cenas existentes.

Principais arquivos rastreados grandes encontrados:

| Item | Tamanho aproximado |
|---|---:|
| três brushes Terrain Tools | 32,02 MB cada |
| `construction_site.wav` | 30,28 MB |
| `Tools/CharacterRigging/DriveImports/MagicShield.zip` | 22,94 MB |
| `Archer.glb` | 15,51 MB |
| `PaladinHealer.glb` | 15,02 MB |
| amostra GLB de documentação | 16,43 MB |

Não há arquivos atualmente registrados pelo Git LFS (`git lfs ls-files` vazio). Há também dependências/entradas grandes ignoradas pelo Git, como `Tools/deps/vcpkg_installed`, `Inputs` e `ThirdParty`; elas não devem ser adicionadas ao commit.

Pendências de desempenho para release:

- medir FPS, memória e draw calls com uma partida real e várias unidades;
- confirmar LOD por distância para cavalo e unidades montadas;
- revisar os 22 avisos da build e classificar quais são aceitáveis;
- executar o player Windows fora do Editor em uma máquina/usuário padrão.

## Saves e BinaryFormatter

O projeto ainda usa `System.Runtime.Serialization.Formatters.Binary.BinaryFormatter` em:

- `Assets/Scripts/Tools/BinarySerializable.cs`;
- `Assets/Scripts/Tools/BinarySerializableScriptableObject.cs`.

Impactos para release:

- API obsoleta e removida/restrita em versões modernas do .NET/Unity;
- formato binário frágil a mudanças de tipos/campos;
- desserialização de dados não confiáveis não é segura;
- saves ausentes retornam valores padrão, o que explica os avisos `GameData.data not found` em ambiente novo.

Decisão desta rodada: **não migrar o runtime sem um conversor e teste de compatibilidade**. A migração segura deve ser uma etapa própria, com leitura controlada dos saves legados, formato versionado (por exemplo JSON/binarizado próprio), validação de campos e backup automático antes da conversão.

## Estratégia segura para destravar o push de `0c60d01`

O estado local permanece preservado: branch `master` está 12 commits à frente de `origin/master`. Não executar `reset --hard`, `rebase` ou `filter-repo` no clone de trabalho.

Plano recomendado:

1. criar uma cópia/clone de transporte separado a partir do commit local `0c60d01`;
2. conferir `git fsck --full`, `git status` e a lista de arquivos grandes nessa cópia;
3. habilitar Git LFS apenas para assets que permanecerão no histórico remoto (GLB/FBX/áudio/TIF), com `git lfs track` e um commit de configuração;
4. se o histórico anterior já contiver blobs grandes, usar `git lfs migrate import --everything` **somente na cópia de transporte**, nunca no clone de trabalho;
5. fazer `git push --force-with-lease origin master` a partir da cópia, após conferir que o remoto ainda aponta para o commit esperado;
6. manter o clone de trabalho e o commit `0c60d01` como backup até o GitHub confirmar o novo histórico.

Alternativa de menor risco operacional: publicar primeiro uma branch `release/0c60d01` ou um novo repositório limpo contendo o snapshot, validando o clone remoto antes de substituir `master`. O problema observado é de pacote/histórico grande, não de perda dos arquivos locais.

## Estado final desta auditoria

- Runtime compartilhado: **não alterado**.
- Configuração de produção: **não alterada**.
- Documentação adicionada: este arquivo.
- Commit/push: **não executados**, conforme solicitado.
- Arquivos não rastreados preexistentes foram preservados: `Docs/AdvancedUnits/audit/`, `Docs/CharacterMeshAudit/Mounted/` e `Tools/CharacterRigging/AuditMountedReadOnly.ps1`.

