# Auditoria estática — integração arquitetural de runtime

Data: 2026-10-18  
Projeto: Unity 6000.6.3f1  
Escopo: somente LOD/colliders dos 9 prefabs em `Assets/CityArchitecture/Prefabs`

## Resultado

**CANDIDATES_READY_WITH_ONE_EXPLICIT_BLOCK**. Foram gerados candidatos e backups; nenhum prefab de produção foi promovido ou sobrescrito. A auditoria foi editor-free/static e conferiu hashes, cardinalidade de componentes e reversibilidade.

| Prefab | Malhas/renderers | Collider candidato | Resultado |
|---|---:|---:|---|
| Barracks_Medieval | 3 / 3 | 3 MeshCollider | CANDIDATE_READY |
| House_Medieval | 3 / 3 | 3 MeshCollider | CANDIDATE_READY |
| Market_Classical | 6 / 6 | 6 MeshCollider | CANDIDATE_READY |
| Temple_Imperial | 4 / 4 | 4 MeshCollider | CANDIDATE_READY |
| EraImperialCityBlock | 0 / 0 | 1 BoxCollider desabilitado | BLOCKED_EMPTY_SOURCE |
| Gatehouse | 36 / 36 | 36 MeshCollider | CANDIDATE_READY |
| WallCorner | 32 / 32 | 32 MeshCollider | CANDIDATE_READY |
| WallStraight | 36 / 36 | 36 MeshCollider | CANDIDATE_READY |
| WallTowerConnector | 24 / 24 | 24 MeshCollider | CANDIDATE_READY |

Todos os candidatos recebem exatamente um `LODGroup` no GameObject raiz, referenciando os `MeshRenderer` existentes como LOD0. Não foram criadas, substituídas ou reimportadas malhas. Os colliders Mesh são não-convexos, não-trigger e referenciam o mesmo `MeshFilter.m_Mesh`; isso é apropriado para arquitetura estática e não altera scripts de gameplay.

`EraImperialCityBlock.prefab` é um marcador sem `MeshFilter`/`MeshRenderer`. O autoring tool não inventa bounds: cria um `BoxCollider` desabilitado apenas para deixar o caso explícito e bloqueia promoção coletiva até que um limite seja definido por authoring visual.

## Reversibilidade e promoção

Cada arquivo de produção foi copiado para `Tools/CityArchitecture/RuntimeIntegration/2026-10-18/Backups` antes do candidato correspondente em `Candidates`. O JSON com hashes completos está em [2026-10-18-architectural-runtime-integration.json](2026-10-18-architectural-runtime-integration.json).

O modo padrão não promove. `--promote` exige auditoria limpa, hash da fonte inalterado e backup correspondente; nesta execução ele seria recusado pelo marcador vazio. Unity PlayMode não foi declarado: o objetivo desta rodada foi authoring reversível e auditoria estática.

## Arquivos criados nesta rodada

- `Tools/CityArchitecture/AuthorArchitecturalRuntimeIntegration.py`
- `Tools/CityArchitecture/README-RuntimeIntegration.md`
- `Tools/CityArchitecture/RuntimeIntegration/2026-10-18/Candidates/**` — 9 prefabs candidatos
- `Tools/CityArchitecture/RuntimeIntegration/2026-10-18/Backups/**` — 9 cópias dos prefabs originais
- `Docs/QA/2026-10-18-architectural-runtime-integration.json`
- este relatório

Nenhum arquivo em `Assets/CityArchitecture/Prefabs`, `Assets/Scripts`, `Assets/Editor` ou gameplay foi alterado por esta rodada.
