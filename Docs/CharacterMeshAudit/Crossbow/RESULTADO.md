# Balestra (`crossbow`) - resultado (2026-10-06)

**Estado: COMPLETA** (com as ressalvas de qualidade e de arquivos compartilhados listadas no fim).

Desenho: **balista de carreta** - arma de ataque a distancia sobre carreta de duas rodas, unidade movel sem tripulacao visivel, `dataKind` Character (mesma familia de integracao da catapulta, menor, mais rapida e de tiro direto). Nao ha modelo licenciado nem chave de geracao 3D: tudo foi **modelado proceduralmente em Blender/Python** (obra do projeto, ver `LICENSE.md`).

## Conferencia da entrega

| Item | Estado | Evidencia |
|---|---|---|
| Modelo (carreta, 2 rodas raiadas, apoio traseiro, cabecote com canal do virote, arco de dois bracos, corda separada, cabrestante, gatilho, virote carregado) | OK | `blender-*.png`, `Unity-LOD*.png`; 2,45 x 1,83 x 1,47 m, origem no chao, frente = +Z |
| Esqueleto generico | OK | `Root`, `Wheel_L/R`, `LimbL/LimbL_Tip`, `LimbR/LimbR_Tip`, `String`, `Bolt`, `Winch`, `Trigger`; pesos normalizados (<= 2 influencias) |
| Pecas separadas e nomeadas (LOD0) | OK | `Body, WheelL, WheelR, LimbL, LimbR, String, Bolt, Winch, Trigger` (`_LOD0`) |
| 3 LODs estritamente decrescentes | OK | **3.650 / 2.329 / 882** triangulos (limites 8.000 / 3.000 / 1.000) |
| UVs e materiais | OK | UVs finitas, 0 % degenerados; 5 materiais Standard (Wood, DarkWood, Iron, Rope, Feather) com texturas procedurais leves (< 1 MB cada); 0 pixels magenta |
| Animacoes `Crossbow@<Clip>.fbx` (Generic) | OK | Idle 2,0 s (loop), Move 4,0 s (loop, 1 volta de roda = 3,456 m; `Speed` multiplica o tempo), Reload 1,4 s, Fire 0,8 s (soltura em 0,2 s), Hit 0,6 s, Destroy 2,0 s |
| Fire / Reload / continuidade | OK | virote some do trilho na soltura e volta no Reload; fim de Reload = Idle, inicio de Fire = Idle, fim de Fire = inicio de Reload (diferenca < 2 cm) |
| Sockets reais | OK | `Mesh/CrossbowVisual/CrossbowRig/Root/Sockets/BoltSpawn` (ponta do trilho) e `.../WeaponMount` (centro do cabecote) |
| Projetil separado | OK | `Prefabs/CrossbowBolt.prefab` (0,60 m, haste de madeira + ponta de ferro + 3 penas, pivo na ponta), impacto `CrossbowImpact.prefab` (faiscas + poeira), disparo `CrossbowMuzzle.prefab` (fumaca/poeira leve) |
| Wrapper jogavel | OK | `Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab`: BoxCollider 1,8 x 1,5 x 2,45, anel de time r = 1,1 m, NavMeshAgent 3,5, `AdvancedUnitEraBinding`, `AdvancedUnitAnimatorDriver`, `ProjectileAttackController` (`releaseDelay` 0,2), `AdvancedUnitDeathEffect` (`CrossbowDeath.prefab`) |
| Animator | OK | parametros padrao; `Idle -> Fire -> Reload -> Idle`; `Speed -> Move`; camada `Hit` mascarada em `Root` (a tremida sobrepoe o que a base estiver fazendo) com `Damaged -> Hit` |
| Contrato | OK | `IsVisualReady` verdadeiro; animacoes Idle, Move, Reload, Fire, Hit, Destroy; efeitos: disparo e destruicao; malhas: balestra e virote; `buildSettingsApproved` **continua falso** |
| Registro | OK | `CharacterData` `crossbow` criado por `RegisterCharacterData` (so apos contrato valido + visual 100 % real); estatisticas Ancient (28 PV, dano 22, alcance 14, cooldown 3,2) |

## Validacoes

- **`CrossbowAudit.Run`**: 334 PASS, 0 FAIL (`unity-audit.txt`). Cobre licenca/fonte, rig, UV, materiais, LODs, componentes, sockets, clipes (deformacao finita, chao, continuidade), Animator, projetil, efeitos, contrato x assets, catalogo e progressao nas 4 eras.
- **`CrossbowPlayModeCheck.Run`** (jogo real, Core -> Map1): 55 PASS, 0 erros no Console, 0 pixels magenta em 8 capturas (`playmode.txt`):
  - locomocao: andou 10 m, `Speed` 3,5, estado Move, rodas giraram 180 graus;
  - alvo a 17,5 m (alcance 14): nenhum disparo, nenhum virote, nenhum dano, sem animacao Fire;
  - em alcance: virote sai 0,19-0,21 s depois do comando (evento `CrossbowRelease` coincide, +0,02 a +0,05 s), efeito de disparo visivel, voo reto (desvio 0,000 m), nasce no `BoltSpawn`, ~30 m/s, dano **22** = perfurante esperado contra armadura 0 (HP 1000 -> 978), efeito de impacto com particulas vivas;
  - cooldown: tentativa em 2,7 s recusada; a 0,3 s do fim recusada; aceita em 3,3 s; segundo virote acumula 44;
  - aliado na linha de tiro e disparo direto contra aliado: HP inalterado;
  - sequencia de animacao `Idle > Fire > Reload > Idle`; `TakeHit` aciona `Damaged` e a camada Hit;
  - LOD pelo zoom ortografico do jogo: 8 -> LOD0, 18 -> LOD1, 26 -> LOD2;
  - destruicao: `AdvancedUnitDeathEffect` instancia `CrossbowDeath(Clone)` tocando `Destroy` (0,21 -> 0,47) com poeira.
- **NUnit EditMode** `EraImperial.AdvancedUnits.Tests` (inclui `Visual/CrossbowVisualTests.cs`, 9 testes): 102/102 aprovados (`crossbow-tests.xml`).
- Reconstrucao idempotente: `CrossbowSetup.BatchBuild` foi executado varias vezes sem erro.

## Revisao visual (critica honesta)

- Le-se como **balista de carreta**: arco de dois bracos curvos com corda reta/V, canal com virote claro contra madeira escura, cabrestante com manivelas e catraca no fundo, rodas raiadas com aro de ferro, apoio traseiro. A comparacao com o "soldado" do jogo (caixa vermelha de 2 m) e com um cilindro de 1,8 m no Blender mostra escala coerente.
- Estilo **low-poly facetado, em caixas**: arestas retas, sem chanfro. Bom para a camera do jogo (ortografica, zoom 8-26), mas nao e um modelo "escultural". Detalhes pequenos (pregos, rebites, penas) so aparecem no LOD0 e em zoom proximo.
- O virote carregado (0,6 m) e pequeno em relacao a carreta; fica legivel so de perto (zoom 8-12).
- Texturas procedurais (madeira, ferro, corda): suaves e leves, sem normal map; o ferro tem pouca ferrugem.
- `Destroy`: carreta tomba para a direita, uma roda solta, braco esquerdo quebra, corda arrebenta, virote cai. Funcional, com poeira; nao ha estilhacos de madeira (nao e destruicao fisica).

## Limitacoes

- Arma sem tripulacao, como decidido; nao ha fisica nem clipe de "empurrar a carreta" alem do giro das rodas.
- O rig e rigido por osso (bracos com 2 ossos para curvar); a corda e deformada por pesos lineares.
- `Hit` so mexe na raiz (tremida do corpo todo); durante Fire/Reload a camada Hit tambem sobrepoe a raiz.
- O projetil nao tem colisao fisica: o dano segue o contrato (`AdvancedProjectile`), igual as outras unidades avancadas.
- Balanceamento inicial (do contrato), sem playtest; FPS nao medido.
- Nao ha producao em edificio nem entrada no Build Settings (decisao do coordenador).
- Nao foi feita uma verificacao `VerifyCrossbowExports.py` de reimportacao em Blender; a verificacao equivalente e feita pelo `CrossbowAudit` no Unity.

## Reconstruir e validar

```
Tools\CharacterRigging\Build-Crossbow.ps1 [-Validate]
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name crossbow-audit -UnityArgs '-quit -executeMethod CrossbowAudit.Run'
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name crossbow-playmode -UnityArgs '-executeMethod CrossbowPlayModeCheck.Run'
```
Observacao: neste PC a politica de execucao do PowerShell bloqueia `Invoke-UnityBatch.ps1` chamado diretamente; use `-ExecutionPolicy Bypass` (apenas no processo).

## Arquivos

- Blender/ferramentas: `Tools/CharacterRigging/BuildCrossbow.py`, `RenderCrossbowPreview.py`, `Build-Crossbow.ps1`; trabalho em `Tools/CharacterRigging/Working/Crossbow/`.
- Unity: `Assets/CharacterRigging/Crossbow/{Models,Accessories,Textures,Materials,Animation,Prefabs,Scripts,Editor}` (`CrossbowSetup.cs`, `CrossbowAudit.cs`, `CrossbowPlayModeCheck.cs`, `CrossbowFireEffects.cs`).
- Jogo: `Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab`, `Assets/Resources/ScriptableObjects/Units/Characters/Crossbow.asset`.
- Dados (via `AdvancedUnitBuilders.Bind*`): `Assets/Data/AdvancedUnits/Crossbow.asset`, `Projectiles/CrossbowBolt.asset`.
- Testes: `Assets/Tests/Editor/AdvancedUnits/Visual/CrossbowVisualTests.cs`.
- Evidencias: esta pasta (`LICENSE.md`, `unity-audit.txt`, `playmode.txt`, `crossbow-tests.xml`, `build-report.json`, `unity-build-report.txt`, renders `blender-*`, `Unity-*`, `playmode-*`).
