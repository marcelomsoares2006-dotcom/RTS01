# Auditoria independente - Balestra (`crossbow`) - 2026-10-06

Auditor: agente independente (nao construiu a unidade). Somente leitura nos arquivos da unidade e nos compartilhados.
Arquivos criados pelo auditor: `Assets/Editor/AdvancedAudit/CrossbowIndependentAudit.cs` (EditMode), `Assets/Editor/AdvancedAudit/CrossbowIndependentPlay.cs` (Play Mode, Map1 e Map2 reais, padrao de `AdvancedUnitMapCheck.cs` sem edita-lo), esta pasta e `TestResults/crossbow-indep-*.log`.
O veredito vem das verificacoes proprias; `CrossbowAudit`/`CrossbowPlayModeCheck` do construtor nao foram usados como prova.

## Veredito: PRONTA COM RESSALVAS

A unidade esta funcional, visualmente reconhecivel como balista de carreta, com dados, animacoes, projetil, dano e escopo corretos. Nao ha defeito bloqueante dentro da unidade. As ressalvas sao: dois problemas de integracao com o behavior tree/era (camada compartilhada, nao arquivos da balestra) e polimento visual da animacao Destroy/Fire.

## Comandos executados
```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name crossbow-indep-edit -UnityArgs '-quit -executeMethod CrossbowIndependentAudit.RunEdit'
powershell -NoProfile -ExecutionPolicy Bypass -File Tools\AdvancedUnits\Invoke-UnityBatch.ps1 -Name crossbow-indep-play -UnityArgs '-executeMethod CrossbowIndependentPlay.Run'
blender -b --factory-startup --python audit_fbx.py     (reimporta o FBX e confere pesos/UV; saida: blender-fbx-check.txt)
git status / git diff / git diff --check (arquivos da unidade)
```
Logs: `TestResults/crossbow-indep-edit.log`, `TestResults/crossbow-indep-play.log`. Saidas: `edit-checks.txt`, `play-checks.txt`, `blender-fbx-check.txt`.

## Resultados numericos
- EditMode (`edit-checks.txt`): **270 PASS, 3 FAIL** - os 3 FAIL sao criterios do proprio auditor: (a) 5.5 limiar do ultimo LOD = 0 (LOD2 nunca some; valido, nao e defeito); (b) 4.2 material `Crossbow_Feather` sem textura (so cor; menor); (c) 4.2 material `FOV` (anel compartilhado clonado do Soldier, nao e da unidade).
- Play Mode Map1 + Map2 (`play-checks.txt`): **232 PASS, 8 FAIL, 0 erros de console**. FAIL: 4 sao artefato de amostragem do auditor (Map2 `.G.<era>.7`: primeira amostra do virote tomada 2-3 quadros tarde, 1,3 m a 30 m/s; no Map1 a distancia ao BoltSpawn foi 0,01 m); 4 sao achados reais (N1 e N5, em ambos os mapas; ver defeitos).
- Pixels magenta: 0,000 % em todos os 68 PNG de render.

## Checklist

| # | Item | Resultado | Evidencia |
|---|---|---|---|
| 1 | Malha, origem e licenca | PASS | Obra procedural (Python/Blender, sem terceiros). `BuildCrossbow.py` sem import externo/rede/Mixamo/Meshy; `LICENSE.md` coerente; nao ha `Inputs/Crossbow` (correto); 0 segredos em 40+ arquivos de texto; nenhum arquivo de terceiros pelo nome |
| 2 | Rig / Avatar | PASS | Maquina: esqueleto Generic de 11 ossos (Root, Wheel_L/R, LimbL/R(+Tip), String, Bolt, Winch, Trigger); Avatar valido e nao humanoide (correto); 6 clipes Generic reais (`humanMotion=false`). `isHuman` nao se aplica |
| 3 | Pesos | PASS | Unity e Blender (reimportando o FBX): 0 vertices sem peso, soma = 1, max 2 influencias, em LOD0 (11 pecas), LOD1 e LOD2; bindposes = ossos |
| 4 | UVs / materiais | PASS (menor) | UVs finitas, 0 % degenerados; todos os shaders `isSupported`, nenhum `InternalErrorShader`; 0 magenta. Menor: `Crossbow_Feather` sem textura |
| 5 | LODs | PASS | Triangulos 3.650 > 2.329 > 882 (limites 8k/3k/1k); LODGroup 0,21 / 0,12 / 0; nenhum renderer fora do LODGroup; LOD pelo zoom do jogo: 8 > LOD0, 18 > LOD1, 26 > LOD2 (Map1 e Map2) |
| 6 | Wrapper | PASS | CharacterManager, NavMeshAgent (r 0,9; v 3,5), BoxCollider 1,8x1,5x2,45 (modelo 1,83x1,47x2,45), EraBinding -> contrato, AnimatorDriver, ProjectileAttackController, DeathEffect -> CrossbowDeath; 0 Missing; contem todos os componentes do Soldier; 1 Animator; `UnitManager.animator` = Animator do visual |
| 7 | Sockets | PASS | `BoltSpawn` (0; 1,06; 1,05) na ponta frontal do virote carregado (dist < 0,35 m), eixo central, +Z; `WeaponMount` no cabecote. No jogo o virote nasce a 0,01 m do BoltSpawn |
| 8 | Animacoes / Animator | PASS (menor) | Idle/Move (loop), Reload, Fire, Hit, Destroy reais, curvas animadas, sem NaN; evento `CrossbowRelease` em 0,2 s = `releaseDelay`; Idle<->Move por Speed, Idle/Move->Fire por trigger, Fire->Reload->Idle por tempo, camada Hit mascarada com `Damaged`; parametros Speed/Fire/Damaged/Running/Attacking/Hit presentes; Fire+Reload = 2,2 s < cooldown de todas as eras (folga 0,1 s no Imperial); virote some na soltura e volta no Reload; continuidade Fire->Reload->Idle = 0 m; em jogo `Idle > Fire > Reload > Idle` e `Hit` ao receber dano. Menor: recuo do arco no Fire e queda no Destroy sao discretos (ver defeitos) |
| 9 | Projetil | PASS | `CrossbowBolt.prefab` real (0,6 m, ponta em +Z, pivo na ponta, sem collider), contrato Piercing/reto/30 m/s, impacto real com particulas (Sparks+Dust), disparo (muzzle) real; `AdvancedProjectile` funcional |
| 10 | Contrato / catalogo | PASS | `Validate().IsValid`, `IsVisualReady`, clipes/malhas/efeitos reais nos caminhos esperados; `CharacterData` unico `crossbow` aponta para o mesmo wrapper, valores Ancient (28/22/14/3,2/FOV 20) e custo igual; codigos unicos; `buildSettingsApproved = false` |
| 11 | Progressao 4 eras | PASS (dados e unidade) / ressalva (BT) | Vida 28>35>43>52, dano 22>29>37>47, alcance 14>15,5>17>18,5, cooldown 3,2>2,9>2,6>2,3, penetracao 0,15>0,25>0,35>0,5. No jogo cada era aplica PV/dano/alcance, o dano real do virote = dano da era, o alcance maximo e respeitado por era (alvo a R+0,6 nao dispara; a R-0,5 dispara) e o cooldown da era e respeitado pelo controlador. Ressalvas N1/N5 abaixo |
| 12 | Comportamento no Map1 e Map2 | PASS (com N1/N5) | Mover 8 m, Speed 3,5, rodas 180 graus; BT autonomo: alvo a 17,8 m aproxima-se e atira sempre a 13,95 m, 4 tiros, 22 de dano cada, intervalo 3,22 s; ordem de ataque fora do FOV; alcance maximo 14 m recusa a 14,6 m; cooldown recusa a 3,11 s e aceita a 3,23 s; virote reto (desvio 0 m, 30 m/s, alinhado, ponta a frente); dano nao e instantaneo (soltura 0,21 s); aliado na linha de tiro, aliado atras do alvo e disparo dirigido a aliado = 0 dano; alvo morre durante o preparo e atirador morre durante o preparo = sem erro; edificio inimigo recebe 22; morte instancia `CrossbowDeath` que toca `Destroy` e some em 8 s; 0 erros de console |
| 13 | Renders proprios | PASS (com observacoes) | 68 PNG (frente/costas/lado/3-4/topo por LOD, poses, virote, escala vs humano, escala de jogo, disparo, destruicao); 0 magenta. Analise abaixo |
| 14 | Compilacao / referencias | PASS (menor) | 0 erros/avisos CS atribuiveis a unidade; 0 GUID quebrado nos assets da unidade; todo arquivo com `.meta`; nenhum nome de tipo/arquivo duplicado; wrapper nao depende de assets de outras unidades. `git diff --check`: fontes (.cs/.py/.ps1/.md) limpas; apenas espaco final em YAML gerado pelo Unity (`m_Name: `) e avisos LF/CRLF do autocrlf |
| 15 | Escopo | PASS | Alterados: apenas `Data/AdvancedUnits/Crossbow.asset` e `Projectiles/CrossbowBolt.asset` (somente `Bind*`: referencias; tiers inalterados). Novos: `Assets/CharacterRigging/Crossbow/**`, `Resources/.../Crossbow.prefab`, `.../Crossbow.asset`, `Docs/CharacterMeshAudit/Crossbow/**`, `Tools/CharacterRigging/{BuildCrossbow.py,RenderCrossbowPreview.py,Build-Crossbow.ps1}`, `Visual/CrossbowVisualTests.cs`. Nenhuma mudanca em Worker/Soldier/Spearman/HeraldicKnight/DarkKnight/Archer, Unit.cs, UnitManager.cs, DebugConsole.cs ou Build Settings (so Core/MainMenu/GameScene/Map1/Map2) |

## Analise visual (imagens examinadas)
Principais: `render-LOD0-iso.png`, `render-scale-iso.png` (ao lado de capsula de 1,8 m), `render-LOD0-{front,back,side}.png`, `render-pose-*`, `play-Map1-{front,back,side,iso}.png`, `play-Map*-gamescale-{6,10,16}.png`, `play-Map*-fire-*.png`, `play-Map1-destroy-*.png`.
- **Reconhecibilidade: boa.** Le-se como balista de carreta medieval: duas rodas raiadas com aro de ferro, armacao de madeira, arco de dois bracos com corda, trilho com virote, cabrestante com manivela, apoio traseiro. Escala coerente (balista ~1,5 m de altura vs 1,8 m humano). Estilo low-poly facetado, em caixas, sem chanfros, texturas procedurais suaves; adequado a camera do jogo.
- **Em escala de jogo** (ortografico 10, `play-Map1-gamescale-10.png`) a unidade ocupa ~50 px de altura (metade do "soldado"); distingue-se como carroca de madeira com arco, mas os detalhes (virote, corda) so aparecem de perto. LOD2 (882 tris) perde as rodas redondas (octogonos), aceitavel a zoom 26.
- **Fire:** corda recua 0,30 m e some o virote na soltura; os bracos do arco mexem apenas ~3,5 cm: recuo pouco legivel. **Reload:** corda e virote voltam de forma legivel. **Destroy:** raiz gira so ~16 graus e desloca 12 cm; a pose final parece quase uma balista intacta levemente inclinada (uma roda/braco soltos discretos); a leitura de "destruida" depende da poeira. O relatorio do construtor ("tomba, roda solta") exagera o efeito.
- Nota: algumas capturas `play-*-fire-*.png` mostram a unidade em amarelo chapado: e artefato do auditor (materiais trocados pelo teste de LOD antes da captura), nao defeito.

## Defeitos encontrados

Bloqueantes: nenhum.

Importantes (camada compartilhada, fora dos arquivos da balestra; descrever ao coordenador):
1. **N1 - mudanca de era depois do spawn trava o BT** (Map1 e Map2): unidade criada em Ancient e era mudada para Imperial; alvo a 16,3 m (alcance Imperial 18,5): 0 disparos em 11 s. `CheckUnitInRange` guarda alcance 14 na construcao, enquanto `TaskFollow` usa `Unit.AttackRange` atual (18,5) e fica parado a ~18,45 m, fora do alcance verificado. Hoje a era so muda pelo console de debug, mas qualquer ligacao futura da era ao jogo deixara as unidades ja em campo sem atacar. (O comentario de `AdvancedUnitEraBinding` documenta so "nao muda a aproximacao", nao o travamento.) Correcao sugerida em `AdvancedUnitEraBinding`/BT: recriar a arvore ou reler alcance em `CheckUnitInRange`.
2. **N5 - cadencia real no jogo ignora o cooldown da era**: via BT a cadencia e 3,22 s em qualquer era (Imperial incluso), pois o `Timer` do BT usa `CharacterData.attackRate` (3,2, Ancient). O contrato promete 3,2/2,9/2,6/2,3; o controlador so impoe o cooldown como minimo. Corrigir na camada compartilhada (Timer do BT lendo o cooldown da era).

Menores:
3. Animacao Destroy fraca (inclinacao ~16 graus); a pose final mal indica destruicao.
4. Recuo do arco no Fire discreto (3,5 cm nas pontas).
5. `Crossbow_Feather` sem textura; virote pequeno (0,6 m) pouco visivel em zoom de jogo.
6. Penetracao de armadura nao tem efeito no jogo (nao existe armadura; `ComputeDamage` usa armadura 0): "perfurante" e so o tipo do contrato hoje. Dano perfurante verificado = dano da era.
7. Margem de apenas 0,1 s entre o ciclo Fire+Reload (2,2 s) e o cooldown Imperial (2,3 s): se o ciclo crescer, o trigger Fire pode ficar preso.
8. `Reload` esta vinculado ao parametro `Fire` no contrato (descricao apenas).
9. A distancia efetiva de tiro desconta o tamanho do alvo (convencao do jogo): o alcance de 14 m e entre o atirador e a borda do alvo (centro a centro ate 14 + meia largura; ex. 14,7 m para o trabalhador).

## Conclusao
PRONTA COM RESSALVAS: pode ser usada no jogo (Ancient e em Play). Ressalvas: (1) corrigir N1 e N5 na camada compartilhada antes de ligar a era ao jogo; (2) reforcar Destroy/recuo do arco (opcional, visual); (3) a unidade segue fora do Build Settings por decisao do coordenador.
