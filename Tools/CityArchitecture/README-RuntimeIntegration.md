# Integração arquitetural de runtime: LOD/colliders

`AuthorArchitecturalRuntimeIntegration.py` é um authoring tool determinístico e reversível para os nove prefabs arquiteturais listados no código.

Uso padrão (não promove produção):

```powershell
python Tools/CityArchitecture/AuthorArchitecturalRuntimeIntegration.py --root . --label 2026-10-18
```

O resultado fica em `Tools/CityArchitecture/RuntimeIntegration/<label>/Candidates` e `Backups`, com o relatório em `Docs/QA`. O script copia cada prefab original para backup antes de escrever o candidato, adiciona um `LODGroup` no GameObject raiz e um `MeshCollider` não-convexo para cada `MeshFilter` que já referencia uma malha. Nenhuma malha é criada ou substituída.

`EraImperialCityBlock.prefab` é um marcador sem `MeshFilter`; recebe somente um `BoxCollider` desabilitado, documentado como `BLOCKED_EMPTY_SOURCE`. Isso evita inventar limites de colisão. A promoção coletiva é recusada enquanto esse caso permanecer sem bounds authoring:

```powershell
python Tools/CityArchitecture/AuthorArchitecturalRuntimeIntegration.py --root . --label 2026-10-18 --promote
```

`--promote` é opt-in, exige auditoria limpa, hashes estáveis e backup correspondente. O uso normal deste trabalho não promoveu nem alterou os prefabs de produção.
