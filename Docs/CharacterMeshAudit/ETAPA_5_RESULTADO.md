# Etapa 5 — variantes de armadura e montagem

Preparação Blender/FBX iniciada em 29/09/2026. Foram criadas duas cópias leves
de fontes independentes, sem alterar os originais:

| Variante | Fonte | Triângulos exportados | FBX reimportado |
|---|---|---:|---:|
| ArmoredKnight | Meshy_AI_Armored_Knight | 21.999 | 21.985 |
| DarkKnight | Meshy_AI__villain_Dark_Knigh | 22.000 | 21.999 |

`BuildStage5Variants.py` reutiliza o pipeline humano FK da etapa 2, com
amostragem de braços robusta para armaduras largas. `VerifyStage5Variants.py`
reimportou os FBXs e aprovou 23 ossos, até quatro pesos normalizados, alturas
próximas de 1,8 m e deformação finita de 0,438 m. Os hashes dos originais foram
conferidos. Cópias e resultados estão em `Working/Stage5/`,
`Assets/CharacterRigging/Stage5/` e `Docs/CharacterMeshAudit/Stage5/`.

`BuildStage5MountPreview.py` montou visualmente um ArmoredKnight sobre o cavalo
independente em `Working/Stage5/MountedPrototype.blend`; captura
`Stage5/MountedPrototype.png`. Os mapas de cor foram religados para a captura.
Também exportou `Assets/CharacterRigging/Stage5/Mounted/ArmoredKnight_MountedIdle.fbx`:
pose sentada mantida por 24 quadros. `VerifyStage5MountedIdle.py` reimportou o
clipe e aprovou 23 ossos, pesos e coordenadas finitas; evidência em
`Stage5/mounted-idle-validation.json`. É uma pose de repouso, não locomoção
montada. Faltam IK, rédeas dinâmicas, socket final de sela e ataque em cavalo.
A proporção e a pose ainda pedem revisão.

`Stage345AssetSetup.cs` importou as variantes no Unity: Avatars Humanoid válidos,
23 ossos e materiais texturizados. Em Play Mode, a amostragem direta dos clipes
FBX deformou ArmoredKnight/DarkKnight em 0,4302/0,4309 m; veja
`Stage3/unity-runtime.txt` e a captura `Stage3/unity-review.png`. O prefab
`Stage5/MountedFitPrototype.prefab` usa dois rigs separados. O cavaleiro montado
é uma instância Generic do FBX `MountedIdle`, ligada ao Spine do cavalo; o rig
Humanoid de ArmoredKnight a pé continua intacto. O Animator do cavaleiro toca
`MountedIdle`, enquanto o do cavalo fica desativado neste protótipo estático.
A cena `Stage5/MountedReview.unity` foi validada em Play Mode: controller em
0,522 do clipe, quadris alinhados ao ponto de sela (erro 0,000 m), pernas
flexionadas, dez capturas de câmera sem rosa e zero erros de execução.
Evidências: `Stage5/unity-mounted.txt` e `Stage5/MountedUnity-{00..09}.png`.

As capturas permitiram corrigir um encaixe inicialmente suspenso e depois
deslocado para trás; o resultado atual foi revisado de frente, lados,
traseira e ângulos altos/baixos. Os controllers individuais das variantes
Humanoid também foram confirmados em Play Mode: avançam a PoseCheck a 0,333
do clipe e deformam as malhas. Ainda faltam sincronizar o cavalo animado,
locomoção e ataques montados, rédeas dinâmicas, IK e transição a pé/montado.
Não considerar o montado pronto para jogo.
