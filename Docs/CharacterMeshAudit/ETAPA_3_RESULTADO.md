# Etapa 3 — cavalo e cervo

Preparação Blender/FBX e importação Unity concluídas em 29/09/2026.

| Modelo | Triângulos originais | Cópia exportada | FBX reimportado | Rig |
|---|---:|---:|---:|---:|
| Cavalo separado | 1.001.521 | 30.000 | 29.842 | 19 ossos |
| Red Deer Stag | 190.339 | 17.999 | 17.999 | 19 ossos |

Fontes: `Inputs/Meshy_AI_knight_horse_0928223644_texture.blend` e
`Inputs/Meshy_AI_Red_Deer_Stag_0928222254_texture.blend`. Os hashes SHA-256
antes/depois coincidem. Cópias editáveis em `Working/Stage3/`, FBXs e texturas
em `Assets/CharacterRigging/Stage3/`. Fontes intactas.

O pipeline `BuildStage3Quadrupeds.py` criou pesos espaciais FK em rigs quadrúpedes,
UV e mapas preservados, e PoseCheck. `VerifyStage3Quadrupeds.py` reimportou os FBXs:
um rig e corpo com UV por modelo, até quatro pesos normalizados por vértice,
alturas 1,655/1,800 m, deformação amostrada 0,117/0,122 m, coordenadas finitas.
158 faces redundantes do cavalo foram descartadas na reimportação (0,53%).
Relatórios: `Stage3/build-report.json` e `Stage3/fbx-validation.json`.
Capturas: `Stage3/{Horse,RedDeer}-{rest,bend}.png`.

O cavalo ainda requer revisão artística de rédeas, crina e arreios; o cervo exige
inspeção de todas as galhadas e patas em mais poses. A atribuição rígida à cabeça
das galhadas é uma regra espacial, não uma segmentação geométrica confirmada.
PoseCheck é somente um teste de articulação; não existe caminhada/corrida final.

`Assets/Editor/Stage345AssetSetup.cs` importou ambos com Avatar Generic válido,
19 ossos e materiais texturizados. A cena `Stage345Review.unity` foi testada em
Play Mode por `Stage345RuntimeCheck`: os clipes FBX têm 200 curvas cada e a
amostragem direta deformou cavalo/cervo em 0,1152/0,1201 m, sem vértices
inválidos. `Stage3/unity-runtime.txt` e `Stage3/unity-review.png` guardam a
evidência. A captura não contém rosa. Uma verificação adicional confirmou que
os controllers de cavalo/cervo avançam a PoseCheck a 0,333 do clipe e deformam
as malhas em 0,1152/0,1201 m. Caminhada/corrida final e revisão artística
permanecem pendentes.
