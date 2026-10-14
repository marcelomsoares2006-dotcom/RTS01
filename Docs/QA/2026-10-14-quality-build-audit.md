# Auditoria de qualidade e build — 2026-10-14

## Escopo

Esta rodada auditou somente a infraestrutura de validação, o build, os testes
EditMode/PlayMode e o Build Settings. Não foram alterados assets visuais,
contratos de unidades ou mecânicas de jogo.

## Critérios de aceite

- `Tools/Validate-Release.ps1` deve validar versão do Unity, manifest/lock,
  GUIDs, metadados de scripts, cenas habilitadas e catálogo de construções.
- `Tools/Validate-Project.ps1` deve exigir EditMode passado sem falhas,
  ignorados ou inconclusivos; executar o smoke PlayMode; e, com `-Build`,
  exigir build Windows concluído.
- Gates editor-free de fundações e unidades militares devem terminar com código
  zero.
- Avisos são contabilizados por código e não tratados como erros. Nenhum aviso
  é removido por supressão global nesta rodada.

## Resultados

| Verificação | Resultado | Evidência |
|---|---:|---|
| Release preflight | PASS | 48 pacotes travados, 5 cenas, 369 metadados, 11 entradas de catálogo |
| Fundações editor-free | PASS | 9/9 estados `model-authored` |
| Unidades militares editor-free | PASS | arqueiro, cavaleiro e aríete |
| Validação visual editor-free | PASS | 104/152, 176/268 e 104/160 vértices/triângulos conforme catálogo |
| EditMode | PASS | 390/390, 0 falhas, 0 ignorados, 0 inconclusivos |
| PlayMode smoke | PASS registrado | 3 cenários, navegação, pausa/retorno, save reload e 0 pixels magenta |
| Build Windows | PASS registrado | `Succeeded`, 0 erros |

Os resultados Unity anteriores permanecem válidos em `TestResults/unit-tests.xml`,
`TestResults/playmode-smoke.txt` e `TestResults/windows-build.txt`. Uma nova
execução Unity foi deliberadamente recusada porque o editor deste projeto está
aberto; nenhum processo foi encerrado e nenhum arquivo foi sobrescrito.

## Avisos do build

Contagem atual do log de build: **33 avisos**.

| Grupo | Ocorrências | Natureza | Decisão |
|---|---:|---|---|
| `CS0618` | 16 | APIs Unity obsoletas (`FindObjectOfType*`) e alias de compatibilidade | Registrar para migração futura; não alterar mecânicas nesta rodada |
| `UAC0023` | 8 | `BinaryFormatter` no formato legado de save/load | Risco de migração de formato; não trocar serialização sem plano de compatibilidade |
| `UAC1015` | 2 | dicionário não serializado por Unity | Estrutura é usada pelo save legado; não adicionar atributo sem confirmar contrato |
| `TREE_SHADER` | 12 | repetição do aviso Nature/Soft Occlusion em árvores | Aviso de asset/renderização, fora do escopo de infraestrutura de validação |

As repetições do build explicam a diferença entre linhas de log e avisos
semânticos: os mesmos avisos de compilação aparecem em mais de uma fase do
build, e o aviso de árvore é emitido por múltiplas cenas/instâncias.

## Correção aplicada

`Tools/Validate-Project.ps1` não aborta mais com uma exceção bruta de
`Get-CimInstance` quando o Windows bloqueia a leitura de `CommandLine` para um
usuário padrão. Ele mantém a verificação precisa quando disponível e usa um
fallback conservador: se houver qualquer `Unity.exe` não inspecionável, a
validação para com instrução clara para fechar os editores ou executar elevada.
Assim, a mudança não mascara uma instância aberta nem mata processos.

## O que ainda falta

1. Fechar o editor Unity e repetir `Tools/Validate-Project.ps1 -Build` em uma
   sessão limpa, arquivando os novos logs.
2. Fazer teste interativo do player standalone: menu, partida nova, produção,
   construção/demolição/reparo, combate/cerco, save/load e saída.
3. Planejar migração dos 8 avisos `UAC0023` sem quebrar saves existentes.
4. Planejar migração dos 16 `CS0618` com testes de regressão de ordem de
   descoberta e inicialização.
5. Corrigir ou aceitar formalmente os 12 avisos de shader após inspeção visual
   perto/longe; isso pertence à frente visual, não a este validador.

## Próxima rodada recomendada

Executar em três frentes independentes: (a) validação Unity em sessão fechada
e teste standalone; (b) desenho de migração de save/API com testes de
compatibilidade; (c) auditoria visual dos shaders das árvores. Ao final,
repetir este relatório e somente então decidir se os avisos são resolvidos ou
aceitos como dívida técnica documentada.
