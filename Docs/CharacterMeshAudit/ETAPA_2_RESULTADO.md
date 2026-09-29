# Etapa 2 — Farmhand e Spearman

28/09/2026: malhas, rigs e FBXs preparados. **Validação Unity pendente; etapa não concluída.**

| Modelo | Original (triângulos) | Cópia leve | Reimportação FBX |
|---|---:|---:|---:|
| Farmhand | 185.021 | 14.999 | 14.980 |
| Spearman, com acessórios | 467.594 | 19.999 | 19.976 |

Reduções de aproximadamente 91,9% e 95,7%. Os originais foram preservados e seus
hashes SHA-256 conferidos. Corpos com cerca de 1,80 m; cada rig tem raiz e 22 ossos
deformadores nomeados como Mixamo. Até quatro pesos normalizados por vértice;
nenhum vértice final sem peso. Lança e escudo independentes ligados rigidamente a
RightHand e LeftHand. As texturas foram preservadas em cópias.

## Entrega

- `Tools/CharacterRigging/BuildStage2Characters.py`: pipeline reproduzível.
- `Tools/CharacterRigging/VerifyStage2Characters.py`: verificação independente dos FBXs.
- `Tools/CharacterRigging/Working/Stage2/{Farmhand,Spearman}.blend`: editáveis.
- `Assets/CharacterRigging/Stage2/{Farmhand,Spearman}/Models/`: FBXs.
- `Assets/CharacterRigging/Stage2/{Farmhand,Spearman}/Textures/`: texturas.
- `Assets/CharacterRigging/Stage2/Scripts/Stage2RigDemo.cs`: interface de demonstração.
- `Assets/Editor/Stage2CharacterPreparation.cs`: setup e testes Unity preparados.
- `Stage2/build-report.json`, `Stage2/fbx-validation.json`: medidas/hashes.
- `Stage2/*-rest.png` e `Stage2/*-bend.png`: capturas Blender conferidas.

## Verificações feitas

1. Criação das malhas leves e rigs no Blender, seguida de inspeção das poses.
   O cálculo direto de pesos do Farmhand falhou. Os pesos foram calculados numa
   cópia volumétrica temporária e interpolados para a malha com UV preservada.
   Pequenos fragmentos sem pesos da cópia auxiliar foram excluídos da interpolação;
   a malha final tem todos os vértices ponderados.
2. Reimportação FBX: ossos, pesos, parentesco, altura e deformação aprovados.
   Movimento máximo amostrado de aproximadamente 0,423 m nos dois modelos.
   O importador descartou 19/23 triângulos redundantes/degenerados (0,13%/0,12%);
   o teste registra essa diferença e rejeita perdas superiores a 0,5%.
3. Compilação C# isolada contra módulos reais do Unity 6000.6.3f1 usando Roslyn.
   Isso verifica tipos/sintaxe, mas não comprova importação ou execução no Editor.

## Limitações

Rig FK de preparação, sem IK nem dedos articulados. `PoseCheck` é um diagnóstico
de articulações, não animação final de caminhar, trabalhar ou atacar. As mãos estão
abertas e avental/tabardo ainda exigem revisão com movimentos mais amplos.
O setup Unity usa base colour e normal quando disponível; o mapa empacotado
original do lanceiro foi conservado, sem conversão completa de metallic/smoothness.
LODs e desempenho coletivo pertencem à etapa 4.

## Bloqueio e retomada

O Editor lançado no ambiente restrito não conectou ao canal `LicenseClient-User`
e não inicializou a licença (`TestResults/stage2-unity.log`). Somente o Editor de
teste criado pelo Codex, PID 23936, foi encerrado.

A revisão automática rejeitou iniciar o Unity fora do ambiente restrito porque
executaria scripts com privilégios de administrador, capazes de acessar arquivos
e configurações fora do projeto. Foi solicitada autorização explícita ao usuário;
nenhum caminho alternativo foi usado para contornar a rejeição.

Em uma sessão Unity aberta como usuário padrão, o menu preparado é:
`Tools > Character Rigging > Stage 2 > Build and Validate`.
Ele deve criar materiais, Avatar Humanoid, controllers, prefabs e a cena
`Assets/CharacterRigging/Stage2/Scenes/Stage2RigDemo.unity`, então testar Play Mode.
Os resultados esperados são `Stage2/unity-import.txt`, `Stage2/unity-playmode.txt`
e `Stage2/unity-{rest,bend}.png`. Não haviam sido gerados ao registrar este estado.

Concluir somente após confirmar Avatar válido, poses e acessórios, texturas,
Console sem erros e capturas reais do Unity. Corrigir qualquer falha encontrada.
A revisão da etapa 1 está em `REVISAO_CLAUDE_ETAPA_1.md`. Não iniciar a etapa 3
antes de terminar esta validação e receber instrução para avançar.
