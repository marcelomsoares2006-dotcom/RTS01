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

## Validação Unity

Após autorização explícita do usuário, o Editor 6000.6.3f1 executou a preparação
e o Play Mode. Farmhand e Spearman tiveram Avatar Humanoid válido, 23 ossos e
texturas importadas (`Stage2/unity-import.txt`). A deformação medida foi de
0,0597/0,0438 m, sem erros de execução (`Stage2/unity-playmode.txt`,
`STAGE2_PLAYMODE_PASSED`). As capturas reais `Stage2/unity-{rest,bend}.png`
foram inspecionadas: sem rosa; acessórios visíveis. Os três testes unitários e
o smoke test do jogo também passaram na nova execução de `Tools/Validate-Project.ps1`.

Ainda faltam animações finais e revisão artística ampla de acessórios/roupas;
esta aprovação se refere à integração técnica e à PoseCheck.
