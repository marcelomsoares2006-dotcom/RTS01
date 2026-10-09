# PaladinHealer (paladin_healer) — resultado (2026-10-06)

Estado: **COMPLETA** (visual e integração reais; nenhum placeholder). Nada foi commitado nem adicionado ao Build Settings.

## Conferência da entrega

| Item pedido | Estado | Evidência |
|---|---|---|
| Corpo: FBX com rig, 3 LODs decrescentes, pesos normalizados, Avatar Humanoid, materiais Standard | **OK** | 31.000 / 9.300 / 3.100 triângulos, 24 ossos (22 mapeados), até 4 influências, pesos somando 1, 1,85 m, textura 2K (base + normal + metálico/suavidade reempacotado). `build-report.json`, `unity-audit.txt`, `sheet_unity_lods.png`, `Unity-LOD*-{front,back,side}.png`. Comparação com `Inputs/PaladinHealer/ref1.png`: armadura de placas, tabardo branco com cruz dourada e cinto mantidos (a referência não tem arma/escudo; as peças são separadas). |
| Clipes Idle, Walk, Run, Attack, Heal, Hit, Death | **OK** | Idle/Attack/Hit/Death: Mixamo retargetados (`Sword And Shield Idle`, `Slash`, `Standing React Large From Left`, `Dying`); Walk/Run: nativos da Meshy; **Heal criado em Blender** (1,23 s: ergue o braço da espada, inclina o tronco, volta à pose do Idle). Folhas: `poses_*.png` (Blender) e `sheet_unity_poses.png` (Unity). |
| Espada e escudo separados, nos soquetes | **OK** | `PaladinHealer_Sword.fbx` (610 tris; aço, ouro, couro) e `PaladinHealer_Shield.fbx` (1.716 tris; aço, ouro, couro, madeira, linho; cruz dourada em relevo). Espada filha de `WeaponMount` (mão direita), escudo filho de `ShieldMount` (antebraço esquerdo), `HealAuraCenter` a 1,2 m (peito). `Unity-closeup-Idle.png`. |
| Efeito de cura em área | **OK** | `Resources/Prefabs/Effects/PaladinHealAura.prefab`: 2 anéis dourados expandindo + disco de luz (script `PaladinHealAuraEffect`) + partículas ascendentes; texturas geradas proceduralmente; **1 unidade de diâmetro em XZ** (verificado: 1,000 x 1,000). Em jogo o `PaladinHealBehaviour` o escala para (8,1,8) na era Ancient (verificado). `sheet_unity_heal.png`, `playmode-heal*.png`. |
| Prefab visual + wrapper jogável | **OK** | `PaladinHealer.prefab` (BoxCollider 0,9x2x0,9, NavMeshAgent 4 m/s, anel de time próprio) com `AdvancedUnitEraBinding`, `AdvancedUnitAnimatorDriver`, `PaladinHealBehaviour`, `AdvancedUnitDeathEffect`. Controller: locomoção (Idle/Walk/Run por `Speed`), `Hit` -> Attack, `Damaged` -> HitReact, camada **Heal** só com tronco/braços/cabeça (máscara), parâmetros padrão. Prefab de morte `PaladinHealerDeath` com controller que toca só `Death`. LODs calibrados como no Arqueiro (0,17 / 0,10 / 0). |
| Contrato e catálogo | **OK** | `BindPrefab`, `BindVisualReference`, `BindAnimation`, `BindSocket`; `visual.animations` = Idle, Walk, Run, Attack, Heal, Hit, Death; `RegisterCharacterData` criou `Resources/ScriptableObjects/Units/Characters/PaladinHealer.asset` (`paladin_healer`, valores Ancient) porque contrato válido e visual 100 % real. |
| Setup idempotente, Audit, PlayModeCheck, NUnit | **OK** | `PaladinHealerSetup.BatchBuild` (executado várias vezes sobre o estado anterior, sem erro), `PaladinHealerAudit.Run`, `PaladinHealerPlayModeCheck.Run`, `PaladinHealerVisualTests` (13 testes). |

## Validações

- **Auditoria Unity** (`PaladinHealerAudit.Run`): **212 PASS, 0 FAIL** — licença documentada, Avatar, 7 clipes humanoides, malhas/pesos/UV/LODs, materiais sem shader com erro (visual, morte, efeito, wrapper), soquetes e peças, controller, deformação de todos os clipes (`BakeMesh`, p99 das arestas < x2,0), efeito, wrapper, contrato x assets, progressão nas 4 eras, catálogo, anel de time visível de cima, e 0 pixels magenta em todas as capturas.
- **Progressão (contrato)**: vida 70/90/115/145, dano 16/20/25/31, cura 6/9/13/18, raio 4/4,5/5/6.
- **Play Mode no jogo real** (Core -> Map1, `playmode.txt`, 62 PASS, 0 erros no Console): o paladino vem de `Globals.CHARACTER_DATA`, anda 9,2 m (Speed 3,98); cercado por 2 aliados feridos dentro do raio (2,0 e 3,6 m), 1 aliado fora (5,0 m) e 1 inimigo de outro dono ferido dentro do raio (2,1 m). Resultado: **cura só os aliados no raio** (+6 por pulso, total 36), **o inimigo nunca é curado**, o aliado fora do raio não é curado na era Ancient; **cooldown** respeitado (pulsos a ~3 s); **limite por alvo** respeitado (exatamente +18 em 10 s); o paladino não se cura; efeito instanciado e escalado (8,1,8); a camada Heal do Animator toca. Na era Imperial (`AdvancedEraState.Set`): vida 145, dano 31 e o aliado a 5 m passa a ser curado (+18). Ataque corpo a corpo pelo behavior tree: 2 golpes de 31 (= `AttackDamage`), estados Locomotion -> Attack. LOD em jogo: zoom 8 -> LOD0, 18 -> LOD1, 26 -> LOD2. Morte: `PaladinHealerDeath(Clone)` toca `Death` sozinho. Capturas `playmode-*.png` com 0 pixels magenta.
- **NUnit EditMode** (`EraImperial.AdvancedUnits.Tests`): **102/102 aprovados** (89 das etapas/unidades anteriores + 13 novos do paladino), `nunit-advancedunits.xml`.

## Como reconstruir e validar

```
Tools\CharacterRigging\Build-PaladinHealer.ps1                       (Blender + Unity pela fila)
powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name paladin-audit    -UnityArgs '-quit -executeMethod PaladinHealerAudit.Run'
powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name paladin-playmode -UnityArgs '-executeMethod PaladinHealerPlayModeCheck.Run'
powershell -NoProfile -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name paladin-tests    -UnityArgs '-runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests -testResults TestResults\paladin-tests.xml'
```
(Se a política de execução do PowerShell bloquear o script da fila, usar `-ExecutionPolicy Bypass` só no processo.)

## Limitações conhecidas

- O rig Meshy não tem dedos: a espada segue o eixo da mão e a mão aberta atravessa o punho (a lâmina fica no prolongamento do antebraço, inclinada 20 graus). A orientação da palma vem do retarget por direção dos membros, então o giro do pulso nos clipes Mixamo pode diferir do original.
- O escudo é fixo ao antebraço no quadro 1 do Idle (de pé, voltado para a frente); em golpes amplos ele gira com o braço e fica de lado/atrás.
- O Heal é uma camada com máscara: as pernas continuam na locomoção; em repouso a pose-base é a do Idle Mixamo (postura de combate).
- LOD2 (3.100 tris) mostra falhas no tabardo se visto de perto; só aparece com zoom ortográfico > ~22.
- Na iluminação do Map1 o personagem aparece escuro quando de costas para o sol (ambiente escuro, mesma cena do Arqueiro); o mapa metálico foi escalado (30 %) para evitar armadura preta.
- Clipes Mixamo: uso em jogo livre, não redistribuir os arquivos soltos (ver `LICENSE.md`). O plano pago da Meshy não é verificável pela API.
- Balanceamento e FPS não medidos. Produção em edifício não ligada. Nada no Build Settings.
