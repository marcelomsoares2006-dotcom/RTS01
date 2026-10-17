# Auditoria UV/LOD arquitetônicos — 17/10/2026

## Escopo e decisão

Auditoria reproduzida em 17/10/2026 dos 20 casos UV já classificados e dos LOD1/LOD2 de House Medieval, Barracks Medieval, Market Classical e Temple Imperial. A auditoria Blender é somente leitura para os assets de produção. A única correção feita nesta rodada foi em uma cópia isolada de um único subobjeto; nenhum prefab, FBX de produção ou `LODGroup` foi promovido.

## UV — 20 ocorrências

O gate reproduzível retornou `AAA_VISUAL_GATE_PASS files=44 uv_review=20 unsafe_rows=0`.

| Classificação | Quantidade | Decisão |
|---|---:|---|
| `BORDA_DE_PRECISAO` | 8 | Manter; overrun máximo da Market `7e-08`. |
| `REPETICAO_INTENCIONAL_PROVAVEL` | 9 | Manter; materiais da Crossbow usam tiling deliberado. |
| `REVISAR` | 2 | Bolt/Trigger; sem evidência não destrutiva para editar. |
| `ILHA_QUASE_LIMITE` | 1 | HeraldicKnight LOD2; margem `2.9e-07`, sem normalização. |

Conclusão UV: **20/20 auditadas, 0/20 corrigidas**. A repetição da Crossbow foi confirmada nos materiais (`TextureWrapMode.Repeat`); portanto não é defeito objetivo de UV. O detalhamento por objeto permanece em `uv-review-2026-10-14.md` e `uv-review-2026-10-14.json`.

## Correção isolada — um defeito comprovado

Foi corrigido somente `HouseBody_LOD` em `House_Medieval_LOD1`, na cópia:

`TestResults/UVLOD-Isolated/House_Medieval_LOD1_HouseBody-corrected.fbx`

O centro do fragmento estava `0,048883 m` distante do centro de `HouseBody` do LOD0, acima do limiar de `0,01 m`. A translação rígida de um único subobjeto reduziu o erro para `0,000000 m`; o round-trip por reimportação do FBX também retornou `0,000000 m`. A execução confirmou `production_assets_modified=false`. Evidência numérica: `TestResults/UVLOD-Isolated/House_Medieval_LOD1_HouseBody-correction.json`. Evidência visual: `TestResults/UVLOD-Isolated/House_Medieval_LOD-comparison.png`.

## LOD arquitetônico

O comparador marcou divergência de centro de fragmento acima de `0,01 m` nos FBX de produção:

| Prédio | LOD1 | LOD2 | Máximo observado |
|---|---:|---:|---:|
| House Medieval | 3/3 fragmentos | 3/3 | `0,107319 m` |
| Barracks Medieval | 2/3 | 3/3 | `0,149326 m` |
| Market Classical | 6/6 | 2/6 | `0,067652 m` |
| Temple Imperial | 4/4 | 0/4 | `0,056606 m` |

Esses desvios, somados à captura Unity anterior que mostrou fragmentos deslocados, são suficientes para manter o gate LOD em **REVIEW/BLOQUEADO PARA PROMOÇÃO**. A correção de um fragmento não torna os demais LODs seguros e não autoriza `LODGroup` em prefab de produção. O relatório de pivôs permanece em `LOD_PIVOT_AUDIT_2026-10-17.md`; os oito LODs de teste continuam fora de `Assets`.

Na reprodução desta sessão, o comparador encontrou 23 fragmentos deslocados nos FBX de produção; os 32 casos adicionais na saída JSON pertencem às cópias de teste isoladas e não alteram a decisão de promoção.

## Evidências e reprodutibilidade

- Script UV somente leitura: `Tools/CityArchitecture/AuditAAAVisualAssets.py`.
- Gate UV/asset: `Tools/CityArchitecture/Validate-AAAVisualGate.ps1`.
- Auditoria LOD: `Tools/CityArchitecture/AuditBuildingLODs.py`.
- Correção isolada: `Tools/CityArchitecture/CorrectIsolatedBuildingLODError.py`.
- Render de verificação: `Tools/CityArchitecture/RenderIsolatedLODComparison.py`.
- JSON completo da auditoria: `2026-10-17-uv-lod-audit.json`.

Estado final: **UV REVIEW (20 casos preservados); LOD NÃO PROMOVIDO; 1 correção isolada comprovada**.
