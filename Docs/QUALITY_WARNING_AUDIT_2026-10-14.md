# Auditoria de qualidade e build — 2026-10-14

## Escopo

Auditoria restrita à infraestrutura de validação: `Tools/Validate-Project.ps1`,
`Tools/Validate-Release.ps1`, Build Settings, EditMode, PlayMode e o resumo de
warnings do build. Não foram alterados contratos de unidades, mecânicas ou
assets visuais.

## Resultado dos gates

| Gate | Resultado | Evidência/critério |
|---|---:|---|
| Release editor-free | PASS | 48 pacotes travados, 5 cenas habilitadas, 369 metadados de script, 11 entradas de catálogo e 11 referências BuildingData |
| Foundation editor-free | PASS | 9/9 fundações em estado `model-authored` e repositório pronto para revisão Unity |
| Unidades militares editor-free | PASS | Arqueiro, cavaleiro e aríete: GUIDs, prefab, animações e CharacterData válidos |
| EditMode | PASS | 390/390; 0 falhas, 0 ignorados, 0 inconclusivos |
| PlayMode smoke | PASS em execução direta | 3 cenários, navegação, save/reload, capturas sem pixels magenta; a execução direta também registrou o worker movendo 12,52 m |
| Windows build | PASS | `Result: Succeeded`, 0 erros, 20 warnings no artefato mais recente |

## Warnings

O valor de warnings é tratado como diagnóstico, não como condição de sucesso.
O build anterior registrava 33; o artefato mais recente registra 20. Os avisos
observados são principalmente:

- APIs Unity 6 obsoletas (`FindObjectOfType`, `FindObjectsOfType`,
  `FindObjectsByType` com ordenação antiga e `Projector` em código de editor);
- `UAC0023`/`UAC1015` em serialização legada (`BinaryFormatter` e dicionário);
- aviso do shader `Nature/Soft Occlusion` para árvores.

Esses itens são reais e permanecem visíveis. Não foram suprimidos, convertidos
em sucesso artificial ou alterados neste ciclo, pois a correção exigiria mexer
em runtime/serialização/renderização fora do escopo seguro solicitado.

## Correção de infraestrutura aplicada

`Assets/Editor/EraImperialSmokeTests.cs` agora instancia o worker de teste já
no ponto amostrado do NavMesh, usando somente a posição do prefab em memória e
restaurando-a imediatamente. Antes, o construtor instanciava o agente na origem
fora do NavMesh e produzia `Failed to create agent because it is not close
enough to the NavMesh`, abortando a validação antes do movimento. Nenhum asset
é salvo ou alterado por esse setup.

## Build Settings auditado

As cinco cenas estão habilitadas e na ordem esperada:

1. `Assets/Scenes/Core.unity`
2. `Assets/Scenes/MainMenu.unity`
3. `Assets/Scenes/GameScene.unity`
4. `Assets/Scenes/Maps/Map1.unity`
5. `Assets/Scenes/Maps/Map2.unity`

## Limitação operacional encontrada

Em algumas execuções, o Unity batch deixou um processo filho de importação ativo
após o retorno do processo principal. Isso faz a próxima chamada do validator
recusar corretamente o projeto como “aberto”. A limpeza agressiva por nome de
processo foi testada e revertida porque poderia encerrar uma execução legítima;
o runner continua encerrando apenas o processo que ele próprio iniciou quando
há timeout. A pendência é de ciclo de vida do executor, não do jogo, e deve ser
tratada separadamente com identificação por PID/parent process antes de novo
endurecimento.

## Próximo roteiro

1. Isolar a limpeza de processos batch por PID pai sem interromper smoke em andamento.
2. Triar warnings de API obsoleta e serialização em uma frente própria, com testes de regressão antes de qualquer migração.
3. Corrigir o aviso de shader das árvores somente após auditoria visual/performance.
4. Repetir `Validate-Release.ps1 -WithUnity -Build` em sessão sem batch residual e anexar o log final.
