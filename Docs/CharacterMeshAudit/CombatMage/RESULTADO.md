# CombatMage (Mago de combate, `combat_mage`) — resultado

Data: 2026-10-06. Estado: **COMPLETA** (visual e integração; nada comitado, Build Settings intacto).

## O que foi entregue
| Item | Onde |
|---|---|
| Corpo Meshy (texto→imagem→3D→auto-rig, referência `Inputs/CombatMage/ref1.png`), 3 LODs, Avatar Humanoid | `Assets/CharacterRigging/CombatMage/Models/CombatMage.fbx` (+ `.glb`) |
| Clipes `Idle`, `Walk`, `Run`, `Cast`, `Hit`, `Death` (`CombatMage@<Clipe>.fbx`) | `Assets/CharacterRigging/CombatMage/Models/` |
| Cajado separado (haste, couro, bronze, orbe emissivo, `CastPoint`) | `Accessories/CombatMage_Staff.fbx`, preso a `StaffMount` na mão direita |
| Efeitos: projétil `CombatMageBolt` (orbe + partículas + rastro), `CombatMageImpact`, `CombatMageCast` (carga no orbe) + texturas procedurais | `Prefabs/`, `Textures/CombatMage_Fx*.png`, `Materials/` |
| Prefab visual, prefab de morte (`Death` toca sozinho), wrapper jogável | `Prefabs/CombatMageVisual.prefab`, `Prefabs/CombatMageDeath.prefab`, `Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab` |
| Animator Controller (Locomotion por `Speed`, `Fire`→Cast, `Damaged`→Hit, parâmetros do BT) | `Animation/CombatMage.controller`, `CombatMageDeath.controller` |
| Contrato ligado (`BindPrefab`, malhas, efeitos, sockets, clipes, projétil) e `CharacterData` registrado | `Assets/Data/AdvancedUnits/CombatMage.asset`, `Projectiles/CombatMageBolt.asset`, `Resources/ScriptableObjects/Units/Characters/CombatMage.asset` |
| Editor/Runtime próprios | `Editor/CombatMageSetup.cs`, `CombatMageEffectsBuilder.cs`, `CombatMageAudit.cs`, `CombatMagePlayModeCheck.cs`; `Scripts/CombatMageStaffEffects.cs` |
| Testes NUnit EditMode | `Assets/Tests/Editor/AdvancedUnits/Visual/CombatMageVisualTests.cs` (11 testes) |
| Reconstrução ponta a ponta | `Tools/CharacterRigging/Build-CombatMage.ps1` (`BuildCombatMage.py`, `BuildCombatMageAccessories.py`, `CombatMageSetup.BatchBuild`) |

## Números
- LOD0/1/2: 30 939 / 9 281 / 3 093 triângulos (estritamente decrescentes, LOD0 ≤ 31 k); 4 influências por vértice, pesos normalizados (soma 1,0); textura 2048².
- Cajado: 1 556 + 520 triângulos (≤ 8 k). Contrato: `Validate()` válido e `EvaluateVisuals()` pronto.
- Clipe `Cast`: 1,77 s; evento `CombatMageRelease` em 0,767 s (quadro 24 a 30 fps) = `releaseDelay` 0,77 s.
- Wrapper: caixa 0,8 × 1,9 × 0,8 m, agente raio 0,4 / altura 1,9, velocidade 4,2, anel de time raio 0,5.
- LODs do jogo calibrados como no Arqueiro (câmera ortográfica 8..26, lodBias 2): 0,17 / 0,10 / 0,0.

## Validações (comandos e resultados)
| Validação | Comando | Resultado |
|---|---|---|
| Build Blender + Unity | `Tools\CharacterRigging\Build-CombatMage.ps1` | `COMBATMAGE_BUILD_OK` (idempotente: rodado várias vezes, GUIDs estáveis) |
| Auditoria EditMode | `Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name combatmage-audit -UnityArgs '-quit -executeMethod CombatMageAudit.Run'` | **208 PASS, 0 FAIL** (`unity-audit.txt`) |
| NUnit EditMode | `... -runTests -testPlatform EditMode -assemblyNames EraImperial.AdvancedUnits.Tests` | 79 casos (suíte inteira, com testes de outras unidades): 78 aprovados; meus 11 testes aprovados; 1 falha **fora do meu escopo** (ver limitações) — `nunit-results.xml` |
| Play Mode (jogo real, Map1) | `... -Name combatmage-playmode -UnityArgs '-executeMethod CombatMagePlayModeCheck.Run'` | `COMBATMAGE_PLAYMODE_PASSED`, 60 PASS, 0 erros de console, 0 pixels magenta (`playmode.txt`) |

O Play Mode cobre: 4 eras trocando `AdvancedEraState` (vida/dano/alcance de `Unit` = tier do contrato); locomoção pelo NavMesh (Speed 3,98); alvo fora do alcance (14 m) sem conjuração;
alvo a 6 m: dano zero durante a conjuração, esfera liberada em 0,78 s (releaseDelay 0,77 s), projétil em voo, dano 18 só na chegada;
recarga (ordem durante os 2,4 s não dispara; intervalo medido 3,7 s e, no ataque autônomo do behavior tree, 2,48 s); era Imperial: dano 40 e alcance 11;
reação a dano (`Damaged`→Hit); morte (`CombatMageDeath(Clone)` no estado `Death`); troca de LOD pelo zoom (8→LOD0, 18→LOD1, 26→LOD2).

## Evidências nesta pasta
- Corpo vs referência: `comparacao-referencia-blender-unity.png` (referência Meshy, Blender frente/costas/lado, Unity LOD0 frente/costas/lado).
- Unity: `Unity-LOD{0,1,2}-{front,back,side}.png`, `Unity-pose-<Clipe>-<tempo>-{front,side}.png`, `Unity-pose-Cast-*-threeq.png`, `Unity-closeup-Cast-*.png`.
- Jogo: `playmode-idle-{front,back,side}.png`, `playmode-walk/cast/bolt/impact/bt-cast/hit/death.png`.
- Dados: `build-report.json` (Blender), `unity-build-report.txt`, `accessories.json`, `LICENSE.md`.

## Revisão visual (crítica)
- Proporção e vestes fiéis à referência (manto índigo até a canela, ombreira de couro, cinto com bolsas, botas, barba curta). Sem deformação visível em ombros e cotovelos nos seis clipes; arestas p99,9 % ≤ 4,0 e menos de 0,3 % das arestas esticadas acima de 3×.
- `Cast`: recuo do cajado com a mão livre erguida (carga), depois o cajado aponta para o alvo e a mão livre empurra para a frente. O orbe pulsa, acumula partículas durante o estado Cast e dá um clarão no disparo.
- Efeito legível: esfera azul-arcana com rastro, impacto com clarão, anel, centelhas e fumaça arcana.

## Limitações
1. A malha Meshy não tem dedos rigados: a mão fica espalmada e o cajado atravessa a palma (não há pegada fechada). Visto de câmera de RTS não chama atenção; um refinamento exigiria rig de dedos.
2. `Walk`/`Run` são nativos do Meshy: o balanço do braço direito inclina bastante o cajado preso à mão (aceitável, parece carregar o cajado em movimento).
3. LOD2 (3 k triângulos) mostra pequenos pontos claros no manto de perto; só aparece no zoom máximo da câmera.
4. A pose de morte (`Dying` do Mixamo) cai de bruços; o prefab dura 6 s (`AdvancedUnitDeathEffect.lifetime`).
5. O sol/ sombra do mapa deixa o manto escuro de costas para a luz (iluminação do jogo, não do asset).
6. Teste compartilhado `VisualReadinessTests.ShippedDefaults_DeclareExpectedPathsOnly_NoneResolved_AndNoneExistOnDisk` (não editável por mim) falha para `combat_mage`: afirma que `CombatMage.prefab` não existe em disco, o que deixou de ser verdade por desenho. Precisa excluir/atualizar o caso `combat_mage` (e os demais quando as unidades forem entregues).
7. `Tools\Validate-Project.ps1` segue exigindo exatamente 3 testes (inalterado).
8. Sem Git LFS necessário nos meus arquivos (maiores: FBX ~10 MB). `Tools/CharacterRigging/Working/CombatMage` (44 MB, .blend de trabalho) e `Inputs/CombatMage` (51 MB) são fontes brutas; decida se entram no repositório/LFS.
