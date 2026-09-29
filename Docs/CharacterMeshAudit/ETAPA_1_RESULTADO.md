# Etapa 1 — Heraldic Knight no Unity 6000.6.3f1: resultado

Execução por Claude Code em 28/09/2026, das 20:45 às 21:05 (horário local).
Blender 5.2.1 LTS e Unity 6000.6.3f1 (Built-in Render Pipeline, Direct3D11, Radeon RX 580).
O ZIP de origem foi apenas lido: nenhum arquivo de `Tools/CharacterRigging/Inputs` foi alterado.
Nenhum commit foi feito.

**Resultado: as três verificações passaram.** As limitações visuais observadas estão listadas no fim.

## Arquivos entregues

| Caminho | Conteúdo |
|---|---|
| `Tools/CharacterRigging/BuildHeraldicKnight.py` | Script Blender reproduzível: extrai cópias dos GLBs, remove auxiliares, converte o rig de cm para m, corrige o material e exporta os FBX. |
| `Tools/CharacterRigging/VerifyHeraldicKnightFbx.py` | Verificação 1: reimporta os FBX no Blender e compara com os GLBs originais. |
| `Tools/CharacterRigging/Working/HeraldicKnight/HeraldicKnight.blend` | Arquivo editável (textura empacotada), fora de Assets. Rig + malha + quatro actions com fake user. |
| `Tools/CharacterRigging/Working/HeraldicKnight/source/*.glb` | Cópias extraídas do ZIP. |
| `Tools/CharacterRigging/Working/HeraldicKnight/build-report.json`, `fbx-roundtrip.txt` | Relatórios do build e da verificação 1. |
| `Assets/CharacterRigging/HeraldicKnight/Models/HeraldicKnight.fbx` | Modelo-base (malha + rig em bind pose), Avatar Humanoid `HeraldicKnightAvatar`. |
| `Assets/CharacterRigging/HeraldicKnight/Models/HeraldicKnight@{Walking,Running,Attack,Triple_Combo_Attack}.fbx` | Um clipe por arquivo; Avatar copiado do modelo-base. |
| `Assets/CharacterRigging/HeraldicKnight/Textures/HeraldicKnight_BaseColor.png` | Textura 2048² extraída do GLB. |
| `Assets/CharacterRigging/HeraldicKnight/Materials/HeraldicKnight_Mat.mat` | Standard, metallic 0, smoothness 0,18, sem emissão. Mais dois materiais só da cena demo (chão, marcador). |
| `Assets/CharacterRigging/HeraldicKnight/Animation/HeraldicKnight.controller` | Animator Controller com quatro estados. |
| `Assets/CharacterRigging/HeraldicKnight/Prefabs/HeraldicKnight.prefab` | Prefab (variante do FBX) com Animator, Avatar, controller e `applyRootMotion = false`. |
| `Assets/CharacterRigging/HeraldicKnight/Scenes/HeraldicKnight_Demo.unity` | **Cena de demonstração independente**, fora das Build Settings. |
| `Assets/CharacterRigging/HeraldicKnight/Scripts/HeraldicKnightDemo.cs` | Controle da demo: ciclo automático, teclas 1–4, Espaço liga/desliga o ciclo. |
| `Assets/CharacterRigging/HeraldicKnight/Editor/HeraldicKnightSetup.cs` | Configuração reproduzível do import, controller, prefab e cena + auditoria (verificação 2). Menu *Tools > Character Rigging > Heraldic Knight*. |
| `Assets/CharacterRigging/HeraldicKnight/Editor/HeraldicKnightPlayModeCheck.cs` | Verificação 3 (Play Mode com capturas). Também permite dar Play na cena demo (ver abaixo). |
| `Docs/CharacterMeshAudit/Etapa1/` | Cópia das evidências: 8 capturas do Unity, relatórios das três verificações e o build-report. |

## Como reproduzir

Na raiz do projeto, com o Unity fechado para este projeto:

```
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/BuildHeraldicKnight.py
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python-exit-code 1 --python Tools/CharacterRigging/VerifyHeraldicKnightFbx.py
Unity.exe -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath <projeto> -logFile TestResults\heraldic-build.log -executeMethod HeraldicKnightSetup.BatchBuildAndAudit
Unity.exe -batchmode -acceptSoftwareTermsForThisRunOnly -projectPath <projeto> -logFile TestResults\heraldic-playmode.log -executeMethod HeraldicKnightPlayModeCheck.Run
```

`Unity.exe` = `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe`. Os dois comandos Unity
terminam com código 0 em caso de sucesso (sem `-quit`; o próprio script encerra o Editor).
Rodar o build do Unity recria controller, prefab e cena demo.

## Decisões técnicas

- **Rig aproveitado, não recriado.** Os quatro GLBs têm esqueleto e malha de bind idênticos (checado
  no script: matrizes de repouso < 1e-4, vértices < 1e-4). Usa-se um rig e as quatro actions.
- **Auxiliares excluídos.** O importador glTF cria `Icosphere` como forma de exibição dos ossos; é
  removido (e as custom shapes limpas) antes da exportação.
- **Escala.** O rig vinha em centímetros com escala de objeto 0,01. A escala foi aplicada e todas as
  curvas de posição dos ossos multiplicadas por 0,01. Prova: posição mundial de todos os vértices
  em quatro amostras de cada clipe, antes × depois, diferença máxima 0,016 mm. Resultado: armature
  com escala 1, personagem com 1,70 m em bind pose, pés em y ≈ 0.
- **Orientação.** Exportado com forward −Z / up Y; no Unity o cavaleiro olha para +Z.
- **Material.** O glTF usa a mesma textura em base color e emissive (emissive factor 1) e não define
  metallic, que pelo padrão glTF vale 1,0. Importado assim, o personagem brilharia sem sombreamento ou
  ficaria escuro/metálico. A textura já tem sombreamento pintado. Por isso: Standard, metallic 0,
  smoothness 0,18, emissão desligada. O FBX mantém o nome `HeraldicKnight_Mat` e o import faz *remap*
  para o material do projeto.
- **Clipes com malha.** Um primeiro teste exportou os clipes só com armature. O Unity colapsou o nó
  raiz e rejeitou o Avatar copiado (`Rig Error: Copied Avatar Rig Configuration mis-match ... Parent for
  'mixamorig:Hips' differs`). A malha foi incluída nos FBX de clipe para a hierarquia ficar idêntica
  (≈1,2 MB por arquivo; o import desses arquivos não cria material).
- **Blender 5 / action slots.** Trocar a action de um rig não reatribui o *slot*. Na primeira versão,
  só o Walking foi exportado com movimento. O script atribui o slot explicitamente e confere se
  cada action realmente anima o rig.
- **Humanoid.** O mapeamento automático do Unity é comparado com a tabela esperada de 22 ossos
  (Spine→Spine, Spine1→Chest, Spine2→UpperChest, …). O script aplicaria o mapeamento explícito se
  houvesse divergência. Neste caso, o mapeamento automático coincidiu. `headfront` fica sem mapeamento
  (osso auxiliar do rosto). Não há dedos no rig.
- **Controller.** Estados Walking (padrão, loop), Running (loop), Attack e TripleCombo (sem loop).
  Triggers `Walk`, `Run`, `Attack` e `TripleCombo` partem de Any State (0,2 s). Os ataques voltam para
  Walking ao atingir 94% da duração (transição de 0,25 s). Não existe Idle no ZIP; nenhum foi inventado.
  Walking é o estado de repouso/retorno.
- **Deslocamento da raiz.** Walking, Running e Attack são praticamente *in place*. Triple Combo avança
  ~1,9 m no clipe original (avgSpeed Unity 0,42 m/s em Z). Rotação e altura ficam incorporadas à pose.
  O deslocamento XZ fica como root motion (disponível para gameplay futuro) e é descartado com
  `applyRootMotion = false`. Na demo, a raiz ficou parada (0,000 m); o quadril se afastou no máximo 0,075 m.
- **Play na cena demo.** `EraImperialProjectSetup` força Play a começar em `Core.unity`. Sem editar esse
  arquivo, `HeraldicKnightPlayModeCheck` limpa `playModeStartScene` somente quando a cena ativa é a demo
  e restaura Core ao voltar ao Edit Mode. A verificação 3 confirma que o Play começou na cena demo.
- **EventManager na demo.** O `DebugConsole` do jogo é injetado em toda cena por
  `RuntimeInitializeOnLoadMethod` e exige um `EventManager`. Sem ele, o primeiro Play registrou
  dois erros no Console. A cena demo inclui um objeto com `EventManager`. O código do jogo não foi alterado.
- **Trigger pendente.** O primeiro Play revelou que `Walk`, disparado enquanto o personagem já
  caminha, ficava pendente e desviava a próxima ação. `HeraldicKnightDemo.Select` agora reseta os
  triggers antes de disparar outro.

## Verificação 1 — FBX round-trip (Blender): PASSOU

`Tools/CharacterRigging/Working/HeraldicKnight/fbx-roundtrip.txt`

- Base: 1 malha ligada a 1 armature, 23 ossos, 15.937 vértices, 19.609 triângulos (idênticos ao GLB),
  até 4 influências, soma dos pesos 1,0000, altura 1,700 m e escala mundial do rig 1.
- Cada clipe: duração do FBX = duração glTF (Walking 1,0833 s; Running 0,7083 s; Attack 2,8333 s;
  Triple Combo 4,3750 s, a 24 fps). A malha FBX, acionada pelo esqueleto do FBX do clipe, foi comparada
  vértice a vértice com o GLB original em quatro quadros por clipe. Desvio máximo: 0,009 / 0,015 /
  0,011 / 0,010 mm. Movimento entre amostras: 0,68 a 2,17 m. O rest do clipe é igual ao rest base.

## Verificação 2 — Importação Unity: PASSOU

`TestResults/heraldic-import-audit.txt` (cópia em `Docs/CharacterMeshAudit/Etapa1/`)

- Scripts compilados, sem falha de compilação. O projeto continua emitindo avisos CS0618/UAC anteriores.
- Textura 2048², sRGB e compressão BC7. Material Standard suportado, com textura, metallic 0 e sem emissão.
- `HeraldicKnightAvatar`: `isValid = True` e `isHuman = True`. Mapeamento de 22 ossos igual à tabela. Nenhum `Rig Error`
  nem aviso de pose no log final.
- 1 SkinnedMeshRenderer, 23 ossos, 19.609 triângulos e 15.956 vértices. O Unity divide vértices nas costuras
  de UV/normal. Máximo de 4 influências e nenhum vértice sem peso; material via remap.
- 4 AnimationClips Humanoid (`HK_Walking`, `HK_Running`, `HK_Attack`, `HK_TripleCombo`), com as
  durações esperadas e loop apenas em Walking/Running.
- Controller com os quatro estados e Walking padrão. O prefab tem Avatar e controller, com root motion desligado.

## Verificação 3 — Execução visual no Unity: PASSOU

`TestResults/heraldic-playmode.txt` e capturas `TestResults/heraldic-*.png`, também copiadas para
`Docs/CharacterMeshAudit/Etapa1/`. As imagens foram renderizadas pela câmera do Unity em Play Mode, não pelo Blender.

- Play iniciado na cena `HeraldicKnight_Demo` com Avatar humano ativo em runtime.
- Cada estado foi atingido pelo Animator. A deformação foi medida por `BakeMesh` em dois instantes:
  deslocamento máximo de vértice de 0,88 m (Walking), 1,27 m (Running), 1,58 m (Attack) e 1,40 m (Triple Combo).
- Walking e Running continuam em loop. Attack e Triple Combo terminam uma vez e voltam sozinhos para Walking.
- Capturas frontal e traseira de cada ação têm 0% de pixels magenta. A câmera enquadra o personagem
  inteiro e o rótulo 3D identifica a ação.
- 0 erros no Console. Único aviso: "Unity is not designed to run with Administrator privileges"
  (ambiente; UAC/registro não foram alterados).
- A validação existente do jogo (`Tools/Validate-Project.ps1`) foi executada depois (concluída às 21:02):
  3/3 testes unitários e 3/3 cenários Play Mode (Map1, Map2, save/reload) passaram, com 0 erros de runtime.
  As cenas de produção não foram modificadas.

## Limitações e observações visuais

1. **Escudo deforma com o braço.** Escudo e espada fazem parte da malha única e usam pesos dos ossos
   do braço/mão, sem osso rígido de acessório. Em Attack e Triple Combo, o escudo visivelmente dobra e
   achata (`heraldic-3-Attack-front.png`, `heraldic-4-TripleCombo-front.png` e `-rear.png`).
   O round-trip mostra desvio de 0,015 mm em relação ao GLB, portanto isso já existe nos pesos do Meshy
   e não foi introduzido pela exportação. Correção futura: pesos rígidos do escudo em LeftHand/LeftForeArm
   ou osso/socket de acessório.
2. **Interseções visíveis.** Em Attack, o escudo erguido encosta e atravessa a região da cabeça e do ombro
   esquerdo. Em avanços e chutes, o tabardo verde estica entre as pernas. A bainha da espada atravessa a
   borda do tabardo, na vista traseira. A capa/tabardo não tem ossos próprios, então segue as pernas.
   A inspeção foi feita em dois ângulos e em um instante por ação, não quadro a quadro.
3. **Sem Idle.** O pacote não traz Idle. Walking é o estado padrão e de retorno.
4. **Pose de referência em A.** O Avatar é válido e humano sem aviso de T-pose. O reaproveitamento dos
   clipes em outros rigs Humanoid depende dessa validação, mas ainda não foi testado com outro personagem.
5. **Root motion.** O avanço do Triple Combo está no clipe e fica desativado na demo. O gameplay deverá
   decidir entre aplicar root motion ou mover pela NavMesh.
6. **Material de face única.** O glTF era `doubleSided`; o Standard descarta faces de trás. Não houve
   buracos nas vistas frontal e traseira capturadas, mas não foram checados todos os ângulos.
7. **Amostragem.** Os clipes foram amostrados a 24 fps. O original tem ~30 chaves/s, com a mesma duração.
   O Unity aplica *Keyframe Reduction*. Não foi observado tremor, mas não houve comparação quadro a quadro no Unity.
8. **Automação.** O Play Mode foi executado pelo Editor em `-batchmode`, com GPU real. As teclas 1–4
   não foram pressionadas pelo teste. Ele chama o mesmo `HeraldicKnightDemo.Select` usado pelas teclas.
9. **Efeitos colaterais do Unity.** Ao importar, o Editor criou `.meta` para ativos que ainda não tinham
   `Assets/CharacterRigging/Prototypes/*` e `Assets/CharacterRigging/Stage2/*`, além de atualizar os `.csproj`.
   `TestResults/playmode-smoke.txt`, `playmode.log` e `unit-tests.*` foram regravados pela nova validação,
   que também passou.
10. **Execução paralela observada.** Entre 20:58 e 20:59, enquanto esta etapa rodava, outro processo criou
   arquivos da etapa 2: `Tools/CharacterRigging/BuildStage2Characters.py`,
   `Assets/CharacterRigging/Stage2/`, `Docs/CharacterMeshAudit/Stage2/`. Processos `codex.exe` estavam
   ativos e novos objetos apareceram em `.git/objects`. Esses arquivos não foram criados, alterados nem
   validados por Claude Code. A etapa 2 **não** foi iniciada por esta execução.

## Estado

Etapa 1 concluída. A etapa 2 aguarda autorização do usuário.
