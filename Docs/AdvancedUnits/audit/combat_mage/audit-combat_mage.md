# Auditoria independente: Mago de combate (`combat_mage`)

Data: 2026-10-06. Auditor independente (nao construiu a unidade). Unity 6000.6.3f1. Somente leitura nos arquivos da unidade e nos compartilhados.

## Veredito: PRONTA COM RESSALVAS

A unidade funciona no jogo real (Map1 e Map2): 401 verificacoes proprias (263 EditMode + 138 Play Mode), 0 falhas, 0 erros de console, 0 pixels magenta.
Os pontos abaixo sao visuais ou de ritmo, nenhum bloqueia o uso. O mais relevante e o corpo que afunda no terreno na animacao de morte.

### Defeitos e ressalvas
| # | Severidade | Defeito |
|---|---|---|
| D1 | importante | **Corpo morto afunda no chao.** Ao fim da animacao do prefab `CombatMageDeath` (`Dying` do Mixamo), o corpo fica quase todo abaixo do terreno; so aparecem fragmentos (botas/ombro). Ver `render-Map1-end-play-death.png` e `render-Map2-end-play-death.png`. Falta compensar o offset vertical do quadril (root Y) no clipe Death ou no prefab de morte. O prefab dura 6 s. |
| D2 | menor-importante | **LOD2 (3 093 tri) tem lacunas visiveis na roupa**: o fundo aparece por tras do manto e das botas (`render-LOD2-front.png`). LOD1 tem so rachaduras finas (`render-LOD1-front.png`). LOD2 so aparece no zoom maximo da camera (limiar 0,10). O relatorio do construtor descreve como "pequenos pontos claros", o que subestima. |
| D3 | resolvido | Reverificado apos as mudancas da camada compartilhada (`CharacterData.attackRate` = 0,25 s de poll; `CheckUnitInRange` le `Unit.AttackRange` ao vivo): pelo `CharacterBT`, com a era trocada DEPOIS do spawn, a cadencia seguiu o cooldown da era: Ancient 2,43..2,47 s (Map1) e 2,45..2,58 s (Map2); Imperial 1,88..1,92 s nos dois mapas, com dano por tiro 18 e 40. A folga de 0,05..0,15 s sobre o cooldown vem do poll de 0,25 s. |
| D4 | menor | A orbe/nucleo do projetil renderiza como esfera branca estourada (`render-Map2-attack-bolt.png`, `render-fx-bolt-side.png`): le-se como bola luminosa, mas perde o azul arcano e fica pouco legivel contra terreno claro. O impacto (anel azul + centelhas) esta bom. |
| D5 | menor | O cajado nao pertence a nenhum LOD do `LODGroup` (renderers do cajado e orbe nao constam nas listas de LOD); fica sempre visivel, sem custo relevante porque o ultimo LOD nunca e culled. Cajado com malha e materiais de cor solida, sem UV nem textura (aceitavel, mas sem detalhe de madeira). |
| D6 | menor | `Tools/CharacterRigging/Inputs/CombatMage/ref1.url.txt` guarda uma **URL CloudFront assinada** (Expires/Signature/Key-Pair-Id) da imagem de referencia. Nao e chave de API (nenhuma chave foi encontrada em arquivo algum), expira em 2026-10-08, mas e um token temporario em arquivo; vale remover antes de publicar o repositorio. |
| D7 | menor | A mao nao tem dedos rigados: o cajado atravessa a palma (aparece nos renders de cast e idle). Walk/Run: o cajado inclina bastante. Ja listado pelo construtor; confirmado. |
| D8 | menor | O CharacterData nao tem `sprite` (icone de UI) nem sons de selecao/movimento (campos vazios; consistente com o estado das demais unidades novas). Sem barra de vida (`healthbar` nulo), igual ao Soldier. |
| D9 | menor | Warnings do compilador atribuiveis a unidade: `CombatMageAudit.cs(293)` CS0618 (`FindObjectsSortMode` obsoleto) e `CombatMagePlayModeCheck.cs(44)` CS0414 (campo `sawBoltInFlight` sem uso). Nenhum erro. |

## Checklist
| # | Item | Resultado | Evidencia |
|---|---|---|---|
| 1 | Malha existe; origem e licenca coerentes; sem segredo | **PASS** (obs. D6) | FBX 3,3 MB + GLB; entradas Meshy (`combatmage_rigged.glb`, `walking.glb`, `running.glb`, `ref1.png`, 3 tarefas SUCCEEDED) e 3 clipes Mixamo existem; `LICENSE.md` cita Meshy, Mixamo e cajado procedural. Varredura de texto por `msy_`, `Bearer`, `sk-`, `MESHY_API_KEY=` nos arquivos da unidade: nenhum achado. Limite: o plano pago da Meshy nao e verificavel por arquivo. |
| 2 | Rig humanoide, Avatar, clipes no tipo certo | **PASS** | Corpo `Human`, `Avatar.isValid && isHuman`, 22 ossos humanoides mapeados (todos os obrigatorios). 6 FBX de clipe (`Idle` 8,40 s, `Walk` 1,04, `Run` 0,63, `Cast` 1,77, `Hit` 1,03, `Death` 5,77) importados como Humanoid, `isHumanMotion`, avatar do corpo. |
| 3 | Pesos normalizados, <=4 influencias, sem vertice sem peso, todos os LODs | **PASS** | Unity (`GetAllBoneWeights`) e Blender independente lendo o FBX (`blender_weights.py`): LOD0/1/2 com 30 939 / 9 281 / 3 093 tri; 0 vertices sem peso, max 4 influencias, soma 1,0000 (desvio 0). 24 ossos, bindposes = ossos. |
| 4 | UVs, materiais, shaders suportados, sem magenta | **PASS** | UVs finitas em todas as malhas do corpo. Todos os materiais de wrapper, visual, morte, bolt, impacto e cast com shader `isSupported` (nenhum `InternalErrorShader`); material do corpo com textura atribuida. 66 capturas (editor e jogo) com 0,0000 % de magenta. |
| 5 | LODs estritamente decrescentes, LODGroup coerente | **PASS** | FBX e wrapper: 30 939 > 9 281 > 3 093; limiares 0,170 / 0,100 / 0,000; LOD0 <= 31 k. Ver D2/D5. |
| 6 | Componentes do wrapper | **PASS** | `CharacterManager`, `NavMeshAgent` (raio 0,4, altura 1,9, vel 4,2), `BoxCollider` 0,8x1,9x0,8 (corpo 1,99 m de altura), `AdvancedUnitEraBinding` (contrato CombatMage), `AdvancedUnitAnimatorDriver`, `ProjectileAttackController` (contrato, animator, spawn=CastPoint, releaseDelay 0,767), `AdvancedUnitDeathEffect` (prefab `CombatMageDeath`, 6 s), todos os componentes de raiz do Soldier, layer/tag iguais. 0 scripts Missing, 0 referencias quebradas. |
| 7 | Sockets | **PASS** | `StaffMount` filho de `RightHand`; `CastPoint` filho do cajado, junto a ponta (a 0,6..2,2 m da mao). Em jogo, na liberacao o CastPoint fica 1,22 m a frente do tronco e 1,35 m de altura (trajeto: recua a -0,60 durante a carga e avanca ate +1,22). Origem do projetil (40,15; 31,37; 57,13) a frente do mago, no nivel do cajado. |
| 8 | Animacoes e controller | **PASS** | Controller: estados `Locomotion` (blend por `Speed`: Idle/Walk/Run), `Cast`, `Hit`; transicoes `Fire`->Cast, `Damaged`->Hit, retorno por exit time. Parametros `Speed`, `Fire`, `Damaged`, `Running`, `Attacking`, `Hit` declarados com os tipos certos; `Running/Attacking/Hit` do BT nao alteram o estado. Simulado: Speed 0 -> Idle, 2,0 -> Walk/Run, 4,2 -> Run, `Fire` -> Cast -> Idle, `Damaged` -> Hit -> Idle. Clipes do contrato = clipes usados; Death toca sozinho no controller de morte. Evento `CombatMageRelease` @0,767 tem receptor. Loops corretos. Em jogo o Animator tocou Run (Speed 4,2), Cast, Hit e Idle. |
| 9 | Projetil separado | **PASS** | `CombatMageBolt.prefab` real (`AdvancedProjectile`, orbe, particulas, rastro, sem colisor), contrato `Validate()` ok, `EvaluateVisuals` pronto, tipo Magic, reto, vel 16, vida 4 s (alcanca 64 m). Impacto real com particulas, termina < 4 s. Em jogo: projetil em voo, impacto instanciado, 0 projeteis presos depois (ver D4). |
| 10 | Contrato e CharacterData | **PASS** | `Validate().IsValid`; `IsVisualReady`; prefab/malhas/efeitos/clipes/projetil apontam para os assets certos; `buildSettingsApproved=false`. 4 tiers com vida, dano e alcance crescentes e cooldown decrescente. Exatamente 1 `CharacterData` com `code=combat_mage`, apontando ao mesmo wrapper, valores Ancient (22/18/8; attackRate 0,25 = poll do BT, cooldown real 2,4 no controlador), custo Gold 70 + Stone 30. Nada no Build Settings. |
| 11 | Evolucao nas 4 eras | **PASS** | Map1 e Map2: Ancient 22/18/8, Classical 28/24/9, Medieval 34/31/10, Imperial 42/40/11 (vida max/dano/alcance de `Unit` = contrato) e cooldown 2,4/2,2/2,0/1,8. Comportamento: alvo a 10,5 m nao e atacado no Ancient (alcance 8 + tamanho) e e atingido no Imperial (alcance 11), com dano 40. Cadencia via BT segue o cooldown da era mesmo trocando a era apos o spawn (ver D3). |
| 12 | Comportamento em jogo (Map1 e Map2) | **PASS** | Movimento no NavMesh (8,0 m, Speed 4,2); alvo fora do alcance nao dispara; ao atacar: conjuracao inicia, nenhum dano nem projetil nos primeiros 0,25 s, projetil sai em 0,79 s, voa, dano 18 so na chegada (+0,29 s) e so no alvo (aliado ao lado intacto); atacar aliado nao causa dano; alvo destruido em voo e alvo nulo sem erro; alvo com pouca vida morre; recarga: com tentativas a cada 0,05 s os intervalos foram 2,35..2,7 s (Ancient) e 1,75..2,1 s (Imperial); ataque autonomo pelo BT com a cadencia da era (ver D3); reacao `Hit` e volta ao Idle; morte instancia o prefab de morte na posicao, toca `Death`, remove apos 6 s (ver D1). 0 erros de console. |
| 13 | Renders proprios | **PASS** com D1/D2/D4 | Ver lista de imagens e descricao abaixo. 0 % magenta em todas. |
| 14 | Compilacao, referencias, duplicatas, `git diff --check` | **PASS** | 0 erros do compilador na unidade (so os 2 warnings de D9). 100 % dos GUIDs dos prefabs/controllers/materiais/assets da unidade resolvem; 0 GUIDs de `.meta` duplicados; nenhum tipo `CombatMage*` duplicado; 1 so `CombatMage.prefab`. `git diff --no-index --check` limpo em todos os `.cs/.py/.ps1/.md` da unidade e `git diff --check` limpo em `CombatMage.asset` e `CombatMageBolt.asset`. |
| 15 | Escopo | **PASS** com observacao | A unidade criou/alterou apenas: `Assets/CharacterRigging/CombatMage/**`, `Resources/Prefabs/Units/Characters/CombatMage.prefab`, `Resources/ScriptableObjects/Units/Characters/CombatMage.asset`, `Assets/Data/AdvancedUnits/CombatMage.asset` e `Projectiles/CombatMageBolt.asset` (via Bind), `Assets/Tests/Editor/AdvancedUnits/Visual/CombatMageVisualTests.cs`, `Tools/CharacterRigging/{Build-CombatMage.ps1,BuildCombatMage*.py,RenderCombatMage*.py}`, `Docs/CharacterMeshAudit/CombatMage/`. Nenhum arquivo de Worker/Soldier/Spearman/HeraldicKnight/DarkKnight/Archer alterado por ela; Build Settings e `ProjectSettings` intactos. Observacao: o `git status` mostra edicoes em arquivos compartilhados (`Unit.cs`, `UnitManager.cs`, `DebugConsole.cs`, testes `BridgeAndCatalogTests.cs`, `GeneratedContractAssetsTests.cs`, `VisualReadinessTests.cs`, `Tools/Validate-Project.ps1`) e a ponte `AdvancedBridge`; nao sao do Mago (provavelmente do coordenador), e nao foram tocados por mim. |

## Resultados numericos
- EditMode (`edit-checks.txt`): **263 PASS, 0 FAIL**.
- Play Mode, Map1 + Map2 (`play-checks.txt`): **138 PASS, 0 FAIL, 0 erros de console**, 23 avisos (todos da base do jogo ou do harness: 14x "Failed to create agent because it is not close enough to the NavMesh" ao instanciar antes do Warp, shader das arvores, arquivos de parametros default, aviso de Administrador do Unity).
- Blender independente: 3 malhas, 0 vertices sem peso, max 4 influencias, soma dos pesos 1.
- Taxa de quadros no batch: 62,9 fps (Map1) e 22,1 fps (Map2); as esperas do teste usam tempo de jogo.
- Durante a auditoria o proprio auditor corrigiu defeitos do seu script (nome de estado `Locomotion`, medicoes em tempo de jogo, IA propria do mago e dos alvos desligadas nos testes manuais). Nas execucoes preliminares houve falhas do auditor, descartadas e nao atribuiveis ao Mago.

## Comandos executados
- `powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name cmindep-edit -UnityArgs '-executeMethod CombatMageIndependentAudit.RunEdit'`
- `... -Name cmindep-play -TimeoutSeconds 3000 -UnityArgs '-executeMethod CombatMageIndependentAudit.RunPlay'` (o `-ExecutionPolicy Bypass` foi necessario porque a politica do sistema bloqueia `.ps1`; so nesse processo)
- `blender -b --factory-startup --python blender_weights.py` (importa o FBX e confere pesos/UVs)
- `git status`, `git diff --check`, `git diff --no-index --check /dev/null <arquivo>`; varredura de segredos com `grep`.
- Codigo do auditor: `Assets/Editor/AdvancedAudit/CombatMageIndependentAudit.cs`. Logs: `TestResults/cmindep-edit.log`, `TestResults/cmindep-play.log`.

## Imagens (esta pasta, `Docs/AdvancedUnits/audit/combat_mage/`)
- LODs 0/1/2 de frente/costas/lado: `render-LOD{0,1,2}-{front,back,side}.png` (pose de repouso do prefab; o cajado nao esta alinhado a mao nessa pose, o que e esperado fora do Animator).
- Em jogo (Map1 e Map2): `render-Map{1,2}-play-idle-{front,back,side}.png`, `render-Map{1,2}-play-after-hit-idle-*.png`, `render-Map{1,2}-play-pose-Walk-Run-*.png`, `...-play-pose-Cast-{charge,release}-*.png`, `...-play-pose-Hit-*.png`, `render-Map{1,2}-attack-{charge,bolt,impact}.png`, `render-Map{1,2}-play-death.png`, `render-Map{1,2}-end-play-death.png`.
- Efeitos isolados: `render-fx-bolt-{side,front}.png`, `render-fx-impact-{015,050}.png`.
- `editmode-discarded/`: renders do editor com pose nao aplicada (Animator.Update fora do Play Mode nao move os transforms); mantidos so por registro, nao usados como evidencia.

## Descricao visual (abri e examinei as imagens)
- Corpo: velho mago de barba curta, manto indigo ate a canela com bordado, ombreira de couro, cinto com bolsas, botas. Fiel a referencia; texturas nitidas em LOD0/LOD1; sem esticamento nos ombros e cotovelos nos poses de cast/walk/hit. De frente, costas e lado a silhueta e legivel.
- Cajado marrom/bronze com orbe azul-ciano e garras douradas; em Idle fica na vertical ao lado do corpo, bem posicionado. No Cast: carga com a mao livre erguida e o cajado inclinado para tras, liberacao com o cajado apontando ao alvo, orbe brilhando e particulas azuis. Os dedos nao existem (mao espalmada, cajado atravessa a palma).
- Em jogo o manto fica muito escuro de costas para o sol (iluminacao do mapa).
- Projetil: esfera branca com halo e centelhas (D4). Impacto: clarao, anel azul e centelhas; legivel.
- Morte: em 1,5 s ainda de pe; no fim o corpo esta quase todo sob o terreno (D1).

## Limites da auditoria
- Pacote de plano Meshy e uso de Mixamo conforme `LICENSE.md` nao sao verificaveis por arquivo.
- Nao reexecutei a suite NUnit completa (`EraImperial.AdvancedUnits.Tests`); o relatorio do construtor cita 1 falha em teste compartilhado que ja foi alterado no `git status` (nao verificado por mim).
- Nao testei producao em edificio, selecao/UI nem balanceamento; FPS in-game nao medido.

## Reverificacao apos mudancas compartilhadas (aviso do coordenador)
Reexecutados EditMode e Play Mode (Map1 e Map2) depois de: (1) `attackRate` de `CharacterData` = `AttackPollSeconds` 0,25 s; (2) `CheckUnitInRange` com alcance ao vivo; (3) anel de time corrigido e `CombatMageSetup.BatchBuild` reexecutado (CharacterData e prefab reconstruidos). Resultado: 263/263 e 138/138, 0 erros de console. O item 10 do checklist passou a verificar `attackRate == AttackPollSeconds` em vez de `== cooldown`. Acrescentei o teste de cadencia via BT com troca de era apos o spawn (D3). O anel de time nao foi auditado visualmente em separado (nas capturas do personagem ele nao aparece).
