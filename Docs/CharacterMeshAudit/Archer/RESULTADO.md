# Archer — resultado (2026-10-05)

Pipeline pedido: Meshy → FBX com rig → Blender (correção/otimização) → arco, aljava e flechas separados → animações → Unity como `Archer` → validação de deformação, materiais, LOD, ataque à distância e Play Mode.

## Conferência da entrega

| Item pedido | Estado | Evidência |
|---|---|---|
| Malha com rig humanoide | **OK** | 30.912 triângulos (LOD0), 24 ossos, até 4 influências, pesos normalizados, 1,78 m, textura PBR 2K (base + normal). Avatar Humanoid do Unity válido, 22 ossos mapeados. `unity-audit.txt`, `build-report.json` |
| Arco e aljava separados | **OK** | `Archer_Bow.fbx` (876 tris), `Archer_Quiver.fbx` (1.040 tris, com bandoleira ajustada ao torso) e `Archer_Arrow.fbx` (160 tris), cada um com seu material e pontos de referência. No Unity: arco filho da mão esquerda, aljava filha do tronco, flecha na mão direita, corda desenhada por `LineRenderer`. |
| Animações idle, caminhada e disparo | **OK, e mais** | `Idle`, `Walk`, `Run`, `Aim`, `DrawString` (puxar corda) e `Fire` (disparo). Humanoid, com eventos de animação (pegar flecha, encaixar, soltar). |
| Exportação FBX/GLB | **OK** | `Archer.fbx` (rig + 3 LODs), 6 `Archer@<Clip>.fbx` e `Archer.glb` (LOD0 + rig + todos os clipes). Reimportados em sessão limpa do Blender: `export-verification.json` (todas PASS). |
| Licença confirmada | **OK, com um limite** | Ver `LICENSE.md`. O corpo foi gerado com a chave de API do usuário; a API da Meshy exige plano pago, em que o usuário é dono do resultado. O plano da conta não é legível pela API (conferir em meshy.ai se quiser prova documental). Clipes Mixamo: uso comercial livre, proibida a redistribuição dos arquivos de animação como pacote. |

## Validações

- **Auditoria Unity** (`ArcherAudit.Run`): 119 verificações PASS, 0 FAIL. Avatar, importação dos 6 clipes, malhas e pesos dos 3 LODs, materiais sem shader com erro (visual, acessórios, projétil e wrapper), soquetes, controller, wrapper, catálogo, deformação e 40+ capturas sem pixels magenta.
- **Deformação**: 9 amostras por clipe com `BakeMesh`; 99% das arestas ficam entre ×0,55 e ×2,0 do comprimento de repouso; até 0,2% das arestas passam de ×3 (pior clipe: Aim), com máximo isolado de ×7. Os limiares foram calibrados depois da primeira medição, então o critério decisivo foi visual: close-ups dos ombros e das costas nas poses mais extremas (`sheet_unity_closeups.png`) não mostram rasgos nem dobras.
- **Materiais**: `Archer_Mat` (Standard, base color + normal map), 5 materiais de acessórios, 2 unlit (corda e anel do time). 0% de pixels magenta em todas as capturas, inclusive no jogo.
- **LOD**: 30.912 / 9.273 / 3.091 triângulos. A câmera do jogo é ortográfica (zoom 8–26) e a qualidade Ultra usa `lodBias 2`; limiares medidos e ajustados em Play Mode: zoom 8 → LOD0, 18 → LOD1, 26 → LOD2 (cor dominante por LOD na imagem renderizada).
- **Play Mode no jogo real** (Core → Map1, `ArcherPlayModeCheck.Run`): o Archer vem do catálogo (`Globals.CHARACTER_DATA["archer"]`), anda 10 m no NavMesh (parâmetro `Speed` 1,6), e contra um inimigo parado a 9 m o `CharacterBT` aciona Draw → Aim → Fire; sem dano durante Draw/Aim (não é instantâneo); a flecha sai quando a animação solta a corda; dano de 14 por flecha (= `AttackDamage`); 2 ciclos completos; distância mínima mantida de 9 m; 0 erros no Console.
- **Regressão**: `Tools/Validate-Project.ps1` → 6/6 testes unitários e 3/3 cenários de Play Mode (Map1, Map2, salvar/recarregar), 0 erros.

## O que foi criado

- `Assets/CharacterRigging/Archer/` — modelos, acessórios, texturas, materiais, controller, prefabs (`ArcherVisual`, `ArcherArrowProjectile`), dados, scripts e editores (`ArcherSetup`, `ArcherAudit`, `ArcherPlayModeCheck`).
- `Assets/Resources/Prefabs/Units/Characters/Archer.prefab` e `Assets/Resources/ScriptableObjects/Units/Characters/Archer.asset` (código `archer`): HP 40, alcance 12, dano 14, cadência 3 s, custo 60 ouro + 30 madeira (valores iniciais, para balancear).
- `Assets/Scripts/Units/Archer/` — `ArcherCombatController`, `ArcherProjectile`, `ArcherBowString`, `ArcherAnimationEvents`, `IAttackHandler` (+ o `ArcherUnitContract` e `RangedAttackProfile` que já existiam).
- `Tools/CharacterRigging/` — `Build-Archer.ps1` (reconstrói tudo), `BuildArcher.py`, `BuildArcherAccessories.py`, `MeasureArcherTorso.py`, `VerifyArcherExports.py` e scripts de renderização.

## Mudanças em arquivos existentes (mínimas)

1. `Assets/Scripts/Units/UnitManager.cs`: `Attack` consulta um `IAttackHandler` opcional no mesmo GameObject; sem ele o comportamento é idêntico ao anterior.
2. `Assets/Tests/Editor/ArcherInfrastructureTests.cs`: **não compilava** (o assembly de testes não enxerga o `Assembly-CSharp`) e bloqueava qualquer build do Unity; reescrito com a mesma intenção, por nome de tipo/reflexão.
3. `Tools/CharacterRigging/Test-ArcherPreparation.ps1` e `ArcherPreparation/*`: atualizados do estado "bloqueado" para "integrado e validado".

## Limitações conhecidas

- O rig da Meshy não tem dedos: as mãos são rígidas.
- Os clipes do Mixamo foram retargetados para um corpo de proporções diferentes: erro de posição de mãos/pés de 11 a 14 cm em relação à origem. A postura de tiro é lateral (como no clipe original).
- Escorregamento de pés em Walk/Run não foi medido; FPS não foi medido.
- Durante o ataque o arco é mantido na vertical; a flecha encaixa no osso da mão, não nos dedos.
- O Archer existe no catálogo e funciona no `CharacterBT`, mas ainda não foi adicionado a uma lista de produção de edifício.
- Nada foi commitado. Há trabalho paralelo de outro agente (pastas `Units/Advanced`, `Crossbow`, `Mage`, `Paladin`) que não foi tocado.
