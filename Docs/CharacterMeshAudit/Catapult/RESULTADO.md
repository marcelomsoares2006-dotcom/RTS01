# Catapult — resultado (2026-10-06)

Estado: **COMPLETA** (modelo procedural próprio, visual, integração e validações aprovadas). Limitações abaixo.

Origem do modelo: **obra do projeto**, modelada proceduralmente em Blender/Python (`Tools/CharacterRigging/BuildCatapult.py`), como o arco/aljava do Arqueiro. Sem terceiros, sem serviço de IA, sem chave. Ver `LICENSE.md`.

## Conferência da entrega

| Item pedido | Estado | Evidência |
|---|---|---|
| Modelo de catapulta de cerco (onagro de torção), ~3,2 x 2,2 x ~2,6 m | **OK** | 3,23 m (comprimento, frente = +Z) x 2,30 m x 2,05 m armada (2,65 m com o braço na vertical). Chassi com longarinas, travessas e tábuas separadas, quadros em A, feixe de torção de 6 cordas com catracas e fecho, braço com cinchas de ferro, concha de madeira com aro de ferro e amarras, olhal de recarga, guincho com tambor/catraca/manivelas e corda, 4 rodas de 8 raios com pneu de ferro. `Unity-*.png`, `blender-*.png` |
| 3 LODs estritamente decrescentes (<= 8k/3k/1k) | **OK** | 4.976 / 1.036 / 408 triângulos. Troca em jogo pela câmera ortográfica: zoom 8 = LOD0, 18 = LOD1, 26 = LOD2 (`playmode.txt`) |
| UV válidas, materiais Standard sem magenta, texturas procedurais | **OK** | UVs em [0,1], finitas, sem triângulos degenerados; `Catapult_Wood` (1024², 7 tábuas), `Iron`, `Rope` (512²), `Stone` (256²) geradas por código; 0 pixels magenta em todos os renders (Edit e Play Mode) |
| Esqueleto genérico, pesos normalizados | **OK** | `Root`, `Arm`, `Ammo`, `Winch`, `Rope`, `Wheel_FL/FR/BL/BR` (9 ossos), 1 influência por vértice (peso 1,0). Avatar Generic válido. Deformação medida com `BakeMesh` (cada osso move a sua parte) |
| Clipes Idle, Move, Reload, Fire, Hit, Destroy | **OK** | `Catapult@<Clip>.fbx`: 3,00 s / 1,13 s (1 volta de roda a 2,5 m/s) / 2,57 s / 1,10 s / 0,47 s / 2,37 s. Fire: evento `CatapultRelease` em 0,400 s = `releaseDelay` |
| Sockets reais | **OK** | `ProjectileSpawn` = filho do osso `Arm` no centro da concha (acompanha o braço; altura 1,17 m armada, 2,64 m na saída). `ArmPivot` = o próprio osso `Arm` (pivô). Caminhos a partir da raiz do wrapper: `Mesh/CatapultVisual/CatapultArmature/Root/Arm/ProjectileSpawn` e `.../Root/Arm` |
| Projétil separado | **OK** | `Prefabs/CatapultBoulder.prefab` (pedra 0,41 m, 320 tris, sem scripts; rastro de fumaça por TrailRenderer); `AdvancedProjectile` é adicionado em runtime |
| Efeito de disparo e de impacto | **OK** | `CatapultFire.prefab` (poeira + clarão, no `ProjectileSpawn` pelo evento do clipe) e `CatapultImpact.prefab` (flash, nuvem de poeira, fragmentos de malha, fagulhas); texturas de partícula suaves geradas por código |
| Wrapper jogável | **OK** | `Assets/Resources/Prefabs/Units/Characters/Catapult.prefab`: BoxCollider 2,3 x 2,3 x 3,3, anel de time raio 1,5 m, NavMeshAgent (velocidade 2,5, raio 1,0, altura 2,2), `AdvancedUnitEraBinding`, `AdvancedUnitAnimatorDriver`, `CatapultCombatController`, `AdvancedUnitDeathEffect` (prefab `CatapultDestroyed`, 20 s) |
| Animator | **OK** | Parâmetros padrão; `Fire` -> Fire -> Reload -> Idle; `Speed` -> Move (velocidade do estado proporcional a `Speed`); `Damaged` -> Hit |
| Contrato e catálogo | **OK** | `Catapult.asset`: `Validate()` válido, `IsVisualReady` verdadeiro (unidade + projétil), animações Idle/Move/Reload/Fire/Hit/Destroy, efeitos disparo/impacto, sockets verificados por `Transform.Find`. `CharacterData` `catapult` registrado (valores Ancient) |
| Prefab de morte | **OK** | `CatapultDestroyed.prefab`: toca `Destroy` sozinho (braço cai, estrutura tomba, 2 rodas se soltam), poeira e lascas, permanece desabado |

## Validações (Unity 6000.6.3f1, pela fila)

- **Auditoria** `CatapultAudit.Run`: ver `unity-audit.txt` (230 PASS, 0 FAIL): licença, importadores, rig, pesos, UV, materiais, LODs, clipes e evento, controller, socket seguindo o braço, deformação por osso, renders de 5 vistas, 3 LODs e 12 poses, efeitos, projétil separado, wrapper, contrato x assets, 4 eras, catálogo.
- **Play Mode no jogo real** `CatapultPlayModeCheck.Run` (Core -> Map1): ver `playmode.txt`. Não dispara a 5 m (< mínimo 8) nem a 23,5 m (> máximo 20); dispara a 14 m; pedra sai no `releaseDelay`; projétil nasce no socket, balístico em arco (ápice 6,7 m acima da reta, subida/descida monotônicas, erro de 0,10 m no alvo); dano direto 45 no alvo; radial 12 e 5 nos dois vizinhos inimigos (queda linear, igual à fórmula), 0 fora do raio, 0 no aliado; efeitos de disparo e impacto com partículas ativas; recarga de 6 s respeitada (nem 0,3 s nem 5,7 s depois); Fire > Reload > Idle; Hit; movimento (Speed 2,48, rodas giram 41 graus/0,12 s); 4 eras (60/80/105/135 PV, 45/60/78/100 dano); disparo conduzido pelo `CharacterBT` real (dano 45); LOD; destruição. 0 erros de console, 0 pixels magenta.
- **NUnit EditMode** `EraImperial.AdvancedUnits.Tests`: 102/102 aprovados, incluindo `Visual/CatapultVisualTests.cs` (9 testes da unidade). `TestResults/catapult-tests.xml`.

## Renders (esta pasta)
`Unity-front/back/side/q-front/q-rear.png`, `Unity-LOD0/1/2-q-front.png`, `Unity-pose-<Clipe>-<centésimos de s>.png` (Fire, Reload, Hit, Move, Destroy), `blender-*.png` (Workbench), `playmode-front/back/side/fire/impact-flash/impact/move/destroy-start/mid/end.png` (no jogo).

Crítica honesta dos renders: reconhece-se como onagro medieval (A-frame, feixe de cordas, braço com concha, guincho, rodas raiadas) e a escala é coerente (3,2 m contra um soldado de 1,8 m). Estilo low-poly limpo, texturas procedurais discretas (sem foto-realismo; os personagens Meshy são mais detalhados). Em repouso o braço fica baixo, atrás (armado), então de frente a silhueta é a do quadro em A; o braço só domina a silhueta durante o disparo. A pedra e a madeira têm pouca variação de cor em distância de jogo. A poeira do impacto (nuvem de ~4 m) e o flash são legíveis no jogo; os fragmentos de pedra são pequenos e escuros.

## Limitações e pendências
- O behavior tree do jogo não conhece alcance mínimo: um inimigo a menos de 8 m mantém a catapulta "atacando" sem disparar (o controlador recusa). Falta uma regra no BT (arquivo compartilhado; não alterado).
- `AdvancedProjectile` orienta a pedra pela velocidade; não há rotação própria (a pedra é quase esférica, quase imperceptível).
- Trigger `Damaged` recebido durante Fire/Reload fica pendente e toca Hit ao voltar ao Idle.
- A catapulta é um chassi rígido por peça (sem tecido/cordas dinâmicas); a corda do guincho é um osso esticado.
- Sem som, sem produção em edifício, balanceamento inicial, FPS não medido. Não registrado em Build Settings.
- Rodar scripts `.ps1` neste computador exigiu `-ExecutionPolicy Bypass` (política padrão bloqueia scripts) apenas no processo, sem alterar a configuração do sistema.

## Reconstruir e validar
`powershell -ExecutionPolicy Bypass -File Tools\CharacterRigging\Build-Catapult.ps1 [-Validate]` (Blender + `CatapultSetup.BatchBuild` pela fila). Validar: `-executeMethod CatapultAudit.Run`, `CatapultPlayModeCheck.Run` e os testes NUnit EditMode, sempre por `Tools\AdvancedUnits\Invoke-UnityBatch.ps1`.
