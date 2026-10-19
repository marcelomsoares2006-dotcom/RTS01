# Auditoria independente PBR / UV / seams / pivôs / LOD

Data: 2026-10-19
Projeto: `RTS01-Corrigido`
Modo: leitura estática + inspeção visual das evidências existentes. Nenhum FBX, prefab, material, textura ou cena de produção foi promovido ou corrigido nesta auditoria.

## Veredito

**REVIEW / promoção ampla bloqueada.** Há um piloto PBR de `House` já aplicado no estado atual e candidatos PBR tecnicamente coerentes, mas faltam gates runtime limpos para muralhas, seams, UVs e transições LOD. Os LODs atuais passaram o teste geométrico de deslocamento, porém permanecem sem `LODGroup` nos prefabs e têm faces degeneradas em LOD2.

## Evidência reproduzida

- `Tools/CityArchitecture/AuditWallMaterialCandidates.ps1` → `WALL_CANDIDATE_AUDIT_PASS_STATIC_CANDIDATE_ONLY`.
- `Tools/CityArchitecture/AuditAAAVisualAssets.py` no Blender 5.2.1 → `AAA_VISUAL_AUDIT_REVIEW files=44 failures=20`.
- `Tools/CityArchitecture/AuditBuildingLODs.py` no Blender 5.2.1, com `ERA_IMPERIAL_LOD_TEST_ROOT=TestResults/LOD-Generation-Round7` → `production=0 round7=0 legacy_isolated=32`.
- Referências obrigatórias `3d-modeling/references/sharp_edges.md` e `validations.md` não existem nesta instalação; a limitação foi compensada com os auditores locais e inspeção de imagens.

## Materiais PBR

### House — produção observada, não promovida nesta rodada

- `Assets/Resources/Prefabs/Units/Buildings/House.prefab` referencia `HousePilot_Wood`, `HousePilot_Roof`, `HousePilot_WoodFloor` e preserva `PlayerColor`.
- `Assets/Resources/Materials/Building/HousePilotPBR/` contém 3 materiais Standard com `_MainTex`, `_BumpMap`, `_MetallicGlossMap` e `_OcclusionMap` preenchidos.
- Os 8 mapas em `Assets/Resources/Textures/Buildings/HousePilotPBR/` são 512×512; albedo sRGB, normal `textureType: 1` linear, MetallicGloss/AO lineares.
- `TestResults/HousePBRPilot/authoring.log` registra `HOUSE_PBR_PILOT_PROMOTED=true`; o backup está em `TestResults/HousePBRPilot/backup/House.prefab`. A diferença de hash é esperada após a promoção observada.
- A captura `TestResults/HousePBRPilot/candidate.png` mostra leitura PBR e sem magenta, mas contém uma barra verde do prefab e é comparação isolada; não é captura limpa de runtime após a promoção. Status independente: **PASS estático / REVIEW visual-runtime**.

### Muralhas — candidatos preservados

- Produção: `Assets/CityArchitecture/Materials/Wall_Ancient.mat`, `Wall_Classical.mat`, `Wall_Medieval.mat` e `Wall_Imperial.mat` usam Standard, mas os arquivos `_BumpMap`, `_MetallicGlossMap` e `_OcclusionMap` estão com `fileID: 0`; só `_MainTex` está preenchido.
- Candidatos: `Assets/CityArchitecture/Materials/Candidates/Wall_*_Candidate.mat` e 16 mapas `Wall_*_{Albedo,Normal,MetallicGloss,AO}.png`.
- O auditor confirmou 4 materiais, 16 mapas 512×512, GUIDs completos, canais coerentes e **0 referências candidatas em prefabs de produção**.
- Inspeção dos mapas mostra textura procedural repetida com grade/tiles deliberados; isso não comprova ausência de seams no mesh ou no Unity. **Não promover.**

### Showcase e kits

- `Assets/CityArchitecture/Materials/Showcase_*.mat` e `Assets/CityArchitecture/Generated/Textures/Showcase/` têm conjunto PBR configurado e as capturas históricas `TestResults/CityArchitectureShowcase_*.png` não mostram magenta.
- `Assets/CityArchitecture/Generated/EraBuildingKit_PBR.fbx` permanece um kit sem par LOD completo e sem evidência de integração em prefab de produção.
- Showcase é evidência de configuração/apresentação, não aprovação dos materiais `Wall_*` nem de produção runtime.

## UV e seams

- O inventário atual cobre 44 FBX e mantém 20 ocorrências UV fora de 0–1: 8 `BORDA_DE_PRECISAO`, 9 `REPETICAO_INTENCIONAL_PROVAVEL`, 2 `REVISAR` e 1 `ILHA_QUASE_LIMITE`.
- Caso arquitetônico: `Assets/CityArchitecture/Generated/Buildings/Market_Classical.fbx/MarketPost*` e `Assets/CityArchitecture/Generated/LOD/Market_Classical_LOD1.fbx/MarketPost*_LOD`; overrun máximo aproximadamente `7e-08`. Manter sem normalização automática, mas validar visualmente no Unity.
- Crossbow (`Bolt_LOD0`, `Trigger_LOD0`) continua em `REVISAR`; Crossbow com repetição e HeraldicKnight quase-limite não devem ser reempacotados por regra.
- Não foi localizado relatório dedicado de seams. O auditor Blender local reporta, para os FBX arquitetônicos `Buildings/` e `LOD/`, **0 arestas não-manifold** e **0 normais inválidas**; isso é evidência topológica parcial, não prova de seam de textura/iluminação.
- A mesma auditoria encontrou **48 faces degeneradas em LOD2**: Barracks 18, House 18, Market 12, Temple 0. Não corrigir automaticamente em produção; investigar a origem no gerador/Decimate e gerar candidato isolado.

## Pivôs e LOD

- `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.json` regenerado com limiares de centro `0,01 m` e pivô `0,00001 m`: `production=0` fragmentos deslocados, `Round7=0`, pivôs alinhados no conjunto auditado.
- Os 8 FBX de `TestResults/LOD-Generation-Round7/` têm hashes diferentes dos 8 FBX atuais em `Assets/CityArchitecture/Generated/LOD`; portanto o PASS do candidato não deve ser interpretado como identidade binária com produção. A produção atual, contudo, passou a comparação estática independente.
- Os 9 prefabs de `Assets/CityArchitecture/Prefabs/` continuam sem `LODGroup` e sem collider serializado, incluindo `EraBuildings/House_Medieval.prefab`, `Barracks_Medieval.prefab`, `Market_Classical.prefab` e `Temple_Imperial.prefab`.
- As capturas `TestResults/LOD-Generation-Round4/House_Medieval_LOD-comparison.png` e `TestResults/UVLOD-Isolated/House_Medieval_LOD-comparison.png` são evidências offline/históricas; não substituem captura Unity de transição LOD perto–médio–longe.
- Os 32 casos `legacy_isolated` são cópia externa não comparável e não devem ser somados aos resultados de produção.

## Pendências objetivas

1. Capturar no Unity 6, em cena isolada e em runtime, `House` após a promoção e cada família de muralha em perto/médio/longe; verificar shader efetivo, normal, smoothness, AO, magenta e seams.
2. Validar no Unity as oito UVs de borda do Market e a amostragem entre LOD0/LOD1; manter as coordenadas se a repetição for intencional.
3. Investigar as 48 faces degeneradas de LOD2 no gerador, corrigir somente em candidato externo e repetir importação Blender + captura Unity.
4. Decidir explicitamente a integração de `LODGroup` e colliders; não adicionar componentes aos prefabs por automatismo.
5. Atualizar os relatórios antigos que ainda citam 23 fragmentos deslocados como estado atual; esse número pertence à rodada anterior, enquanto a auditoria atual registra zero.

## Alterações desta rodada

Somente os relatórios/auditores foram executados; nenhum asset de produção foi editado. O relatório de candidatos muralha foi reproduzido em `Docs/QA/2026-10-18-wall-material-candidate-audit.{md,json}` e os relatórios Blender foram atualizados em `Docs/AAA/VisualAudit/`.
