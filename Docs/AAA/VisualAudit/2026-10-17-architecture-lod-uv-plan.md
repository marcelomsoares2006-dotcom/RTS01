# Plano de auditoria LOD/UV arquitetônicos — 17/10/2026

## Resultado executivo

Auditoria somente leitura do projeto `RTS01-Corrigido`. Foram localizados 18 FBX em `Assets/CityArchitecture/Generated`: 12 pertencem às quatro famílias arquitetônicas com LOD0/LOD1/LOD2 comparável e 6 são kits/blocos sem par LOD completo. Há ainda 6 FBX legados em `Assets/Resources/Imports/Units/Buildings`, registrados como fora do gate arquitetônico atual.

Os 12 FBX comparáveis foram importados em Blender sem salvar ou reexportar os assets de produção. O limiar de deslocamento de fragmento é **0,010000 m**. Foram encontrados **23 fragmentos de produção acima do limiar**: House 6, Barracks 5, Market 8 e Temple 4. O máximo é `BarracksBody` em `Barracks_Medieval_LOD2`, com **0,149326 m** contra LOD0. A comparação direta LOD1↔LOD2 também excede o limiar nas quatro famílias; portanto, a promoção de `LODGroup` continua bloqueada.

As **20 ocorrências UV existentes** foram preservadas e classificadas: 8 `BORDA_DE_PRECISAO`, 9 `REPETICAO_INTENCIONAL_PROVAVEL`, 2 `REVISAR` e 1 `ILHA_QUASE_LIMITE`. **Nenhuma UV foi alterada**. As 20 ocorrências incluem 8 arquitetônicas da Market, 11 da Crossbow e 1 do HeraldicKnight; as duas últimas famílias são mantidas no inventário global UV, mas não são LODs arquitetônicos.

## Escopo e evidências

- Inventário FBX: `Assets/CityArchitecture/Generated/**/*.fbx` e `Assets/Resources/Imports/Units/Buildings/*.fbx`.
- Comparação LOD: `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.json`, gerado por `Tools/CityArchitecture/AuditBuildingLODs.py` em modo de importação Blender somente leitura.
- Classificação UV: `Docs/AAA/VisualAudit/uv-review-2026-10-14.json` e `uv-review-2026-10-14.md`, com 44 FBX no gate e 20 ocorrências UV.
- Evidência da correção experimental isolada: `TestResults/UVLOD-Isolated/House_Medieval_LOD1_HouseBody-correction.json`; ela confirma `production_assets_modified=false` e não foi promovida para produção.
- Prefabs arquitetônicos verificados: 9 em `Assets/CityArchitecture/Prefabs` e 11 em `Assets/Resources/Prefabs/Units/Buildings`; nenhum contém `LODGroup` no inventário atual.

Nenhum FBX, prefab, script de gameplay ou componente de produção foi editado nesta auditoria. Os arquivos alterados por esta tarefa são somente este plano e `TestResults/architecture-lod-uv-plan.json`.

## Inventário arquitetônico

### Famílias comparáveis LOD0/LOD1/LOD2

| Família | LOD0 | LOD1 | LOD2 | Fragmentos LOD0 |
|---|---|---|---:|---:|
| House Medieval | `Assets/CityArchitecture/Generated/Buildings/House_Medieval.fbx` | `Assets/CityArchitecture/Generated/LOD/House_Medieval_LOD1.fbx` | `Assets/CityArchitecture/Generated/LOD/House_Medieval_LOD2.fbx` | 3 |
| Barracks Medieval | `Assets/CityArchitecture/Generated/Buildings/Barracks_Medieval.fbx` | `Assets/CityArchitecture/Generated/LOD/Barracks_Medieval_LOD1.fbx` | `Assets/CityArchitecture/Generated/LOD/Barracks_Medieval_LOD2.fbx` | 3 |
| Market Classical | `Assets/CityArchitecture/Generated/Buildings/Market_Classical.fbx` | `Assets/CityArchitecture/Generated/LOD/Market_Classical_LOD1.fbx` | `Assets/CityArchitecture/Generated/LOD/Market_Classical_LOD2.fbx` | 6 |
| Temple Imperial | `Assets/CityArchitecture/Generated/Buildings/Temple_Imperial.fbx` | `Assets/CityArchitecture/Generated/LOD/Temple_Imperial_LOD1.fbx` | `Assets/CityArchitecture/Generated/LOD/Temple_Imperial_LOD2.fbx` | 4 |

### Outros FBX de CityArchitecture sem par LOD completo

- `Assets/CityArchitecture/Generated/EraBuildingKit.fbx`
- `Assets/CityArchitecture/Generated/EraBuildingKit_Detailed.fbx`
- `Assets/CityArchitecture/Generated/EraBuildingKit_PBR.fbx`
- `Assets/CityArchitecture/Generated/EraBuildingKit_Textured.fbx`
- `Assets/CityArchitecture/Generated/EraImperialCityBlock.fbx`
- `Assets/CityArchitecture/Generated/PremiumFortificationKit.fbx`

### FBX legados fora do gate arquitetônico comparável

- `Assets/Resources/Imports/Units/Buildings/house-construction_00.fbx`
- `Assets/Resources/Imports/Units/Buildings/house-construction_01.fbx`
- `Assets/Resources/Imports/Units/Buildings/house.fbx`
- `Assets/Resources/Imports/Units/Buildings/tower-construction_00.fbx`
- `Assets/Resources/Imports/Units/Buildings/tower-construction_01.fbx`
- `Assets/Resources/Imports/Units/Buildings/tower.fbx`

## Medições de deslocamento

Centro de bounds em espaço importado, comparado por fragmento correspondente. `DISPLACED_FRAGMENT` significa erro euclidiano estritamente maior que 1 cm.

| Família | LOD1 vs LOD0 | LOD2 vs LOD0 | LOD1 vs LOD2 | Maior desvio observado |
|---|---:|---:|---:|---:|
| House Medieval | 3/3; máx. 0,048883 m | 3/3; máx. 0,107319 m | 3/3; máx. 0,147316 m | HouseBody LOD2: 0,107319 m vs LOD0 |
| Barracks Medieval | 2/3; máx. 0,048960 m | 3/3; máx. 0,149326 m | 3/3; máx. 0,189898 m | BarracksBody LOD2: 0,149326 m vs LOD0 |
| Market Classical | 6/6; máx. 0,032920 m | 2/6; máx. 0,067652 m | 6/6; máx. 0,095554 m | MarketPlatform LOD2: 0,067652 m vs LOD0 |
| Temple Imperial | 4/4; máx. 0,056606 m | 0/4; máx. 0,000000 m | 4/4; máx. 0,056606 m | TempleBody LOD1: 0,056606 m vs LOD0 |

Total: **23 fragmentos deslocados em produção**. A cópia isolada de teste ainda registrou 32 divergências internas, portanto não é evidência suficiente para promoção. O desalinhamento da hierarquia interna explica o popping/peças flutuantes observado no teste de `LODGroup` do showcase.

## Classificação das 20 ocorrências UV

| Classe | Qtde | Decisão |
|---|---:|---|
| `BORDA_DE_PRECISAO` | 8 | Manter; validar costura e wrap no material antes de qualquer edição. |
| `REPETICAO_INTENCIONAL_PROVAVEL` | 9 | Manter; não empacotar, normalizar ou reescalar automaticamente. |
| `REVISAR` | 2 | Revisão manual de ilha/material; nenhuma correção automática. |
| `ILHA_QUASE_LIMITE` | 1 | Inspeção manual; nenhuma normalização automática. |

Detalhamento completo, com caminho, malha, contagem fora de 0–1, overrun e ação, está em `TestResults/architecture-lod-uv-plan.json`.

Resumo dos casos:

- `BORDA_DE_PRECISAO`: quatro `MarketPost*` do LOD0 e quatro `MarketPost*_LOD` do LOD1; overrun máximo medido `7e-08`.
- `REPETICAO_INTENCIONAL_PROVAVEL`: `Crossbow_LOD2`, `Crossbow_LOD1`, `Body_LOD0`, `String_LOD0`, `WheelL_LOD0`, `WheelR_LOD0`, `LimbL_LOD0`, `LimbR_LOD0` e `Winch_LOD0`; spans acima de um tile são compatíveis com madeira/ferro/corda e materiais repetíveis.
- `REVISAR`: `Bolt_LOD0` e `Trigger_LOD0` da Crossbow; não há evidência segura para reempacotar ou redimensionar.
- `ILHA_QUASE_LIMITE`: `HeraldicKnight_Body` em `HeraldicKnight_LOD2`; overrun máximo `2,9e-07`, sem repetição relevante.

## Lista ordenada de correções seguras

1. **Bloquear a promoção para produção.** Não adicionar `LODGroup` aos prefabs enquanto qualquer fragmento correspondente exceder 0,010000 m ou houver popping/flutuação em captura próxima, média e distante.
2. **Corrigir a fonte em cópia isolada.** Abrir o `.blend`/fonte arquitetônico fora de `Assets`, criar uma raiz neutra por prédio, aplicar transformações, manter o piso e alinhar cada subobjeto correspondente ao LOD0. Não editar o FBX de produção diretamente.
3. **Reexportar LOD1 e LOD2 isoladamente.** Gerar candidatos fora de `Assets`, preservar nomes/hierarquia/material slots e repetir a comparação de bounds. Promover somente o candidato que passe todos os fragmentos, não apenas o pior fragmento.
4. **Revisar a hierarquia antes do Unity.** Confirmar que cada LOD possui o mesmo conjunto de subobjetos correspondentes, pivôs locais coerentes e origem estável; repetir a captura A/B do showcase sem tocar em prefab jogável.
5. **Tratar as duas UVs `REVISAR` manualmente.** Inspecionar `Bolt_LOD0` e `Trigger_LOD0` em Blender com seus materiais; só reempacotar se a intenção de textura e a ausência de costura forem comprovadas, sempre em cópia e com round-trip.
6. **Validar as oito bordas de precisão.** Conferir wrap, padding e costura dos `MarketPost*`; manter as coordenadas se a amostragem for intencional e o LOD correspondente conservar o mesmo comportamento.
7. **Validar as nove repetições prováveis.** Confirmar `TextureWrapMode.Repeat`/trim nos materiais da Crossbow e manter os spans; não converter para 0–1 por regra automática.
8. **Inspecionar a ilha quase limite.** Conferir `HeraldicKnight_Body` LOD2 e seu material; nenhuma alteração é segura sem evidência de artefato visível.
9. **Só então testar integração.** Montar `LODGroup` primeiro em showcase/cópia, medir transições e revisar visualmente; a promoção para prefab de produção exige aprovação explícita e validação Unity completa.

## Critérios de aceitação

- Inventário reproduzível: 18 FBX em `CityArchitecture/Generated`, 12 na matriz comparável e 6 auxiliares; 6 FBX legados registrados fora do gate.
- LOD0/LOD1/LOD2 importam sem erro e todos os fragmentos têm correspondência nominal.
- Para cada família, todos os centros correspondentes têm erro `<= 0,010000 m` contra LOD0 e entre LOD1/LOD2; nenhum `DISPLACED_FRAGMENT` permanece.
- Capturas isoladas próxima/média/distante mostram silhueta estável, sem popping, flutuação, salto de peça ou quebra de material.
- As 20 UVs continuam contabilizadas e classificadas exatamente como 8/9/2/1; UV fora de 0–1 só é aceita com intenção de repetição/borda comprovada.
- UV0 é finita, tem material válido e não introduz costura visível; nenhuma normalização automática é aplicada.
- Nenhum prefab arquitetônico recebe `LODGroup` antes do gate; placement, colliders e NavMesh permanecem inalterados durante a frente de authoring.
- `git diff` da execução contém apenas este plano e `TestResults/architecture-lod-uv-plan.json` como artefatos produzidos por esta tarefa; FBX, prefabs e scripts de gameplay permanecem sem alteração.

**Status:** `REVIEW / LOD BLOQUEADO PARA PROMOÇÃO / UV PRESERVADA`.
