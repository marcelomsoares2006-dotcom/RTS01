# Auditoria do City Block e assets de fortificação

**Data:** 2026-10-13  
**Projeto:** `RTS01-Corrigido`  
**Escopo:** somente leitura da produção; nenhum código, prefab ou asset existente foi alterado nesta auditoria.

## Resultado executivo

Os assets gerados estão presentes em `Assets/CityArchitecture/Generated` e possuem pares `.meta` criados pelo Unity:

| Asset | Tamanho aproximado | Observação |
|---|---:|---|
| `EraImperialCityBlock.blend` | 106 KB | Fonte Blender do bloco urbano |
| `EraImperialCityBlock.fbx` | 10 KB | Exportação FBX do bloco urbano |
| `PremiumFortificationKit.blend` | 143 KB | Fonte Blender do kit premium |
| `PremiumFortificationKit.fbx` | 579 KB | Exportação FBX de muralha, torre e portão |

Os quatro FBX/BLEND estão acompanhados de metadados Unity. O repositório está em `master`, 27 commits à frente de `origin/master`; há também um arquivo de produção não rastreado, `Assets/Editor/CityArchitecture/CityBlockPrefabBuilder.cs`, que deve ser revisado antes de qualquer publicação.

## Inventário e riscos

### `EraImperialCityBlock`

- Representa o bloco urbano derivado da planta CADCraft.
- O FBX é pequeno para uma cidade completa; isso indica geometria predominantemente simples/procedural, não uma cidade AAA final.
- Deve ser tratado como referência/layout ou bloco de prototipagem até haver auditoria visual de materiais, UVs, colisões, LODs e escala.
- A existência do `CityBlockPrefabBuilder.cs` sugere uma etapa de conversão para prefab, mas o script não foi executado nesta auditoria.

### `PremiumFortificationKit`

- Contém a fonte Blender e FBX separado do bloco urbano.
- O FBX é significativamente maior que o bloco urbano, mas o tamanho do arquivo não comprova qualidade visual, UVs, colisões ou desempenho.
- Antes de uso em mapa jogável, verificar: escala em metros, origem/pivô, normais, materiais, UV 0–1, submeshes, colisores, LOD0/LOD1/LOD2 e custo de renderização.
- O kit não deve substituir automaticamente as fortificações existentes; integração deve ocorrer por prefab/cena de teste isolada e com reversibilidade.

## Compatibilidade e integração

- Os `.meta` presentes indicam que o Unity já reconheceu os arquivos como assets.
- A presença de `.blend` dentro de `Assets` pode provocar reimportação dependente do Blender instalado e aumentar o tempo de importação. Para produção, preferir manter o FBX como formato de runtime e o `.blend` como fonte versionada/documentada.
- Não foi possível concluir, por inspeção dos arquivos, se os FBX possuem materiais PBR completos, colisores ou LODs. Isso exige inspeção no Unity ou leitura do arquivo no Blender.
- Não foram alterados `BuildingEraDecoration`, cenas, prefabs existentes, NavMesh ou catálogo de unidades.

## Comandos de validação recomendados

Executar com o Unity fechado. Estes comandos são somente de validação e não devem ser substituídos por limpeza destrutiva:

```powershell
Set-Location 'C:\Users\User\Downloads\Era Imperial\EraImperial-UnityRTS-Base\RTS01-Corrigido'

# Estado do repositório e inventário dos assets
git status --short
Get-ChildItem 'Assets\CityArchitecture\Generated' -File |
  Select-Object Name, Length, LastWriteTime

# Validação oficial do projeto (EditMode, PlayMode e build, conforme o script)
powershell -ExecutionPolicy Bypass -File '.\Tools\Validate-Project.ps1'

# Conferência de referências aos assets gerados
rg -n "EraImperialCityBlock|PremiumFortificationKit|CityBlockPrefabBuilder" Assets Docs
```

Para uma inspeção visual no Unity, abrir uma cópia/cena de showcase e verificar os itens abaixo antes de promover qualquer prefab:

1. escala e alinhamento sobre o terreno;
2. ausência de magenta e materiais faltantes;
3. UVs e normais nas quatro orientações da câmera;
4. colisão e passagem pelo portão;
5. LOD e contagem de triângulos a curta/média distância;
6. impacto no NavMesh e no posicionamento de unidades.

## Parecer do fiscal

**Estado:** aprovado para prototipagem e inspeção isolada; **não aprovado ainda para substituir a cidade de produção**.

O maior risco atual é confundir um bloco/layout exportado com um asset AAA pronto para jogo. A próxima etapa segura é importar/inspecionar em uma cena de showcase, medir geometria e materiais, e somente depois criar ou atualizar prefabs versionados. Esta auditoria não executou alterações de produção e não criou commit.
