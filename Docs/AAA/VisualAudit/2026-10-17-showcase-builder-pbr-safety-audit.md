# Auditoria do builder e dos mapas PBR do showcase — 17/10/2026

## Veredito

**Regeneração segura somente no escopo do showcase, com guarda de diff.** O builder não contém operação de salvar/aplicar prefab de produção, mas a execução não é somente leitura: ela regrava a cena do showcase, pode atualizar os cinco materiais `Showcase_*`, pode reimportar os mapas complementares e grava evidências em `TestResults`.

**O builder Unity não foi executado nesta auditoria e nenhuma captura nova foi gerada.** Os PNGs e logs existentes são evidência histórica, não resultado desta rodada.

## Estado encontrado

- `Assets/Editor/CityArchitecture/CityArchitectureShowcaseBuilder.cs` prevê o Town Center visual e oito instâncias candidatas (`House_Medieval`, `Barracks_Medieval`, `Market_Classical`, `Temple_Imperial`).
- `Assets/Scenes/CityArchitectureShowcase.unity` atualmente contém `TownCenter_VisualCandidate`, muralhas/portões e vias, mas **0** nomes `Showcase_Building_`. A cena persistida está defasada em relação ao builder atual.
- `TestResults/CityArchitectureShowcaseAudit.txt`, que o builder atual escreveria em `WriteShowcaseMetrics`, não está presente.
- Não há diff em `Assets/**/*.prefab` ou `Assets/**/*.prefab.meta`. O workspace já estava sujo antes desta auditoria por alterações de outras frentes; isso não foi normalizado nem revertido.

## Segurança do builder

### Caminho seguro observado

1. O builder cria uma cena vazia e instancia os prefabs de muralha e candidatos com `PrefabUtility.InstantiatePrefab(..., scene)`.
2. Para o Town Center, abre o prefab com `LoadPrefabContents`, clona somente o objeto `Mesh`, move a cópia para a cena e chama `UnloadPrefabContents`.
3. A cena é salva em `Assets/Scenes/CityArchitectureShowcase.unity`; materiais de apresentação são atualizados em `Assets/CityArchitecture/Materials/Showcase_*.mat`.
4. Não foram encontrados `SaveAsPrefabAsset`, `ApplyPrefabInstance` ou `SavePrefabAsset` no builder.

Assim, as alterações de posição, escala, era e material ficam na cópia da cena. O uso dos prefabs de produção como fonte é seguro enquanto o builder continuar sem aplicar overrides ou salvar o conteúdo carregado de volta ao prefab.

### Escritas que devem ser esperadas e revisadas

- `Assets/Scenes/CityArchitectureShowcase.unity` — sobrescrita determinística da cena visual.
- `Assets/CityArchitecture/Materials/Showcase_*.mat` — `EnsureShowcaseMaterials()` chama `SetDirty` e `AssetDatabase.SaveAssets()`.
- `.meta` dos mapas complementares — `ConfigureShowcaseMapImporter()` pode chamar `SaveAndReimport()`.
- `TestResults/CityArchitectureShowcase*.png` e `TestResults/CityArchitectureShowcaseAudit.txt` — saídas de captura/métricas.

Qualquer diff em prefab, FBX de produção, `Assets/Resources/Prefabs`, `Assets/CityArchitecture/Prefabs` ou scripts de gameplay deve reprovar a execução e ser investigado antes de continuar.

## Auditoria dos mapas PBR

### Inventário

| Grupo | Quantidade | Resolução verificada | Uso |
|---|---:|---:|---|
| `PBR_Stone/Plaster/Wood/Roof/Trim.png` | 5 | 512×512 | albedo/base color em `_MainTex` |
| `Showcase_*_Normal.png` | 5 | 512×512 | normal map em `_BumpMap` |
| `Showcase_*_MetallicGloss.png` | 5 | 512×512 | metallic/smoothness em `_MetallicGlossMap` |
| `Showcase_*_AO.png` | 5 | 512×512 | oclusão em `_OcclusionMap` |

Os 15 mapas complementares estão presentes para os cinco perfis. Os `.meta` atuais classificam os Normals como `textureType: 1` e os mapas MetallicGloss/AO como `textureType: 0`; os mapas não-normal estão com `sRGBTexture: 0`, coerente com dados lineares.

### Natureza e limites

`Tools/CityArchitecture/GenerateShowcasePBRMaps.py` é determinístico, usa perfis procedurais e escreve somente os 15 PNGs em `Assets/CityArchitecture/Generated/Textures/Showcase`. O cabeçalho do script declara que não abre nem salva `.blend`, FBX, prefab ou cena. Portanto é adequado para evidência/apresentação isolada, mas não é bake PBR de produção nem prova de calibração física.

Os cinco `PBR_*.png` são mapas base de albedo; não há conjunto de mapas de produção correspondente. Isso limita a conclusão a “materiais PBR de showcase configurados”, não “PBR completo de produção”.

### Divergência que deve ser conferida após uma regeneração controlada

O código do builder define `_BumpScale` como `0.65` (ou `0.35` para Trim) e `_OcclusionStrength` como `0.72`, mas os cinco `.mat` serializados atualmente mostram `1.0` para ambos. Isso não afeta prefabs de produção, porém prova que uma futura execução pode alterar materiais de showcase; o diff dos `.mat` precisa ser revisado explicitamente.

## LOD/UV e bloqueios não resolvidos

O relatório `Docs/AAA/VisualAudit/2026-10-17-uv-lod-audit.md` mantém 20 ocorrências UV classificadas sem normalização automática. Os LODs arquitetônicos continuam bloqueados para promoção por fragmentos deslocados acima do limiar de 1 cm. O builder atual não integra `LODGroup`, o que é a decisão segura.

## Evidência histórica, sem nova captura

- `TestResults/pbr-showcase-material-audit.log` contém marcadores históricos de criação da cena e `SHOWCASE_MULTI_VIEW_CAPTURED=3`, com encerramento batch bem-sucedido.
- Os arquivos `TestResults/CityArchitectureShowcase_Overview.png`, `_Mid.png` e `_Close.png` existem, mas são capturas anteriores; não foram regenerados nesta auditoria.
- `TestResults/pbr-showcase-maps.log` registra uma tentativa Unity interrompida por `Licensing is not yet initialized`; não é prova de uma nova geração dos mapas.

## Procedimento seguro recomendado

1. Fechar o Unity e garantir que não haja outro processo usando o projeto.
2. Registrar `git status` e hashes dos materiais/mapas antes da execução.
3. Executar somente `CityArchitectureShowcaseBuilder.Build` em cópia controlada.
4. Conferir que o diff contém apenas cena showcase, materiais/mapas de showcase, `.meta` de importação e evidências.
5. Só depois, se necessário, executar `BuildAndCaptureViews`; declarar captura apenas com log Unity novo e PNGs com timestamp correspondente.
6. Manter os LODs fora da cena e não promover qualquer prefab.

**Conclusão:** a arquitetura atual permite regenerar o showcase sem alterar prefabs de produção, mas a execução deve ser tratada como uma escrita controlada. A cena persistida ainda não comprova a regeneração do builder atual; a validação Unity/captura fica pendente e não foi declarada.
