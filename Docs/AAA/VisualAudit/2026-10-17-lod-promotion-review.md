# Revisão de promoção de LOD — 2026-10-17

## Resultado

**BLOQUEADO — nenhuma promoção foi aprovada e nenhum pacote de promoção foi criado.**

As cópias em `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\after` têm os centros geométricos alinhados ao LOD0, mas os pivôs dos objetos foram deslocados pela própria correção aplicada. O conjunto não pode ser promovido enquanto os pivôs não coincidirem com os pivôs correspondentes do LOD0.

## Escopo e segurança

- Quatro edifícios: `House_Medieval`, `Barracks_Medieval`, `Market_Classical` e `Temple_Imperial`.
- Oito candidatos: LOD1 e LOD2 de cada edifício.
- Os FBX analisados como produção foram somente importados em Blender; nenhum FBX de `Assets/CityArchitecture/Generated` foi escrito.
- Nenhum prefab ou `LODGroup` foi alterado.
- A pasta `after/` é uma área de teste externa, não uma pasta de produção e não deve ser tratada como promoção aprovada.
- O estado de produção ficou sem diff nos caminhos rastreados `Assets/CityArchitecture/Generated/Buildings` e `Assets/CityArchitecture/Generated/LOD`.
- Os 12 FBX de produção tiveram SHA-256 idêntico ao respectivo arquivo em `before/` durante a conferência; portanto, a revisão não alterou essas fontes.

## Método batch

Blender `5.2.1 LTS`, em modo `-b`, executou os scripts externos:

- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\align_lods.py`
- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\inspect_lods.py`
- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\render_comparison.py`

Critérios usados nesta revisão:

- pivô: `location` do objeto importado, comparado ao objeto correspondente do LOD0, com tolerância de `0,00001 m`;
- rotação e escala: diferença máxima de `0,00001`;
- centro geométrico: erro máximo de `0,01 m`;
- objetos ausentes, transformações não finitas ou alteração de produção: bloqueio automático.

O relatório batch original usa `safe_alignment=true`, mas esse campo significa apenas alinhamento do centro dos bounds. Ele não certifica alinhamento de pivô e, por isso, não é suficiente para promoção.

## Evidência do bloqueio

No conjunto `before/`, os pivôs coincidiam com o LOD0 (`erro máximo: 0 m`), mas havia 23 fragmentos com centro geométrico deslocado. O script `align_lods.py` aplicou uma translação rígida igual ao delta do centro do bounds. Essa operação corrigiu os centros, porém também moveu o pivô do objeto.

No conjunto `after/`:

- fragmentos deslocados pelo critério de centro: `23 → 0`;
- erro residual máximo de centro: `0,000003 m`;
- objetos com pivô fora da tolerância: `23`;
- erro máximo de pivô: `0,149326 m`, em `Barracks_Medieval_LOD2/BarracksBody`;
- rotação e escala: sem divergência detectada;
- objetos ausentes: `0`;
- transformações não finitas: `0`.

### Pivôs divergentes no `after/`

Os vetores abaixo são `pivô_after - pivô_LOD0`, em metros:

| Arquivo | Objeto | Delta do pivô | Erro |
|---|---|---:|---:|
| `House_Medieval_LOD1.fbx` | `HouseBody` | `[0,036317; -0,010801; 0,030886]` | `0,048883 m` |
| `House_Medieval_LOD1.fbx` | `HouseDoor` | `[0,007526; -0,009735; -0,000514]` | `0,012316 m` |
| `House_Medieval_LOD1.fbx` | `HouseRoof` | `[0,019410; 0,000863; 0,015171]` | `0,024651 m` |
| `House_Medieval_LOD2.fbx` | `HouseBody` | `[-0,107319; 0; 0]` | `0,107319 m` |
| `House_Medieval_LOD2.fbx` | `HouseDoor` | `[0; 0,026756; 0]` | `0,026756 m` |
| `House_Medieval_LOD2.fbx` | `HouseRoof` | `[-0,052732; 0; 0]` | `0,052732 m` |
| `Barracks_Medieval_LOD1.fbx` | `BarracksBody` | `[0,038057; -0,012361; 0,028211]` | `0,048959 m` |
| `Barracks_Medieval_LOD1.fbx` | `BarracksRoof` | `[0,020519; 0,000728; 0,012757]` | `0,024172 m` |
| `Barracks_Medieval_LOD2.fbx` | `BarracksBanner` | `[0; 0,065499; 0]` | `0,065499 m` |
| `Barracks_Medieval_LOD2.fbx` | `BarracksBody` | `[-0,149326; 0; 0]` | `0,149326 m` |
| `Barracks_Medieval_LOD2.fbx` | `BarracksRoof` | `[-0,071959; 0; 0]` | `0,071959 m` |
| `Market_Classical_LOD1.fbx` | `MarketAwning` | `[0,015920; 0,000413; 0,013077]` | `0,020606 m` |
| `Market_Classical_LOD1.fbx` | `MarketPlatform` | `[0,025646; 0,001173; 0,020607]` | `0,032920 m` |
| `Market_Classical_LOD1.fbx` | `MarketPost` | `[0; -0,015520; 0]` | `0,015520 m` |
| `Market_Classical_LOD1.fbx` | `MarketPost.001` | `[0; -0,015520; 0]` | `0,015520 m` |
| `Market_Classical_LOD1.fbx` | `MarketPost.002` | `[0; -0,015520; 0]` | `0,015520 m` |
| `Market_Classical_LOD1.fbx` | `MarketPost.003` | `[0; -0,015520; 0]` | `0,015520 m` |
| `Market_Classical_LOD2.fbx` | `MarketAwning` | `[-0,041018; 0; 0]` | `0,041018 m` |
| `Market_Classical_LOD2.fbx` | `MarketPlatform` | `[-0,067652; 0; 0]` | `0,067652 m` |
| `Temple_Imperial_LOD1.fbx` | `TempleBody` | `[0,041307; -0,013548; 0,036255]` | `0,056606 m` |
| `Temple_Imperial_LOD1.fbx` | `TempleColumn` | `[0; 0,030729; 0]` | `0,030729 m` |
| `Temple_Imperial_LOD1.fbx` | `TempleColumn.001` | `[0; 0,030729; 0]` | `0,030729 m` |
| `Temple_Imperial_LOD1.fbx` | `TemplePediment` | `[0,024843; -0,017209; 0,006543]` | `0,030921 m` |

`Temple_Imperial_LOD2.fbx` não teve correção e manteve os pivôs alinhados, mas isso não libera o conjunto: os outros sete arquivos candidatos continuam bloqueados.

## Pacote de promoção

**Não criado.** Criar um ZIP ou copiar qualquer candidato para `Assets` neste estado poderia mascarar o deslocamento de pivô e reintroduzir popping/offset ao trocar de LOD. As cópias `after/`, o JSON e as imagens de comparação permanecem apenas como evidência externa da rodada:

- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\alignment-report.json`
- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\inspection.json`
- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\after\`
- `D:\AI\EraImperial-Lod-Tests\2026-10-17-LOD_AlignedRound2\comparison_*_after.png`

## Desbloqueio necessário

Gerar novamente cada LOD mantendo o `location`/pivô do fragmento igual ao LOD0 e corrigindo a posição da geometria no mesh/na exportação, sem traduzir o objeto inteiro. Reexecutar a comparação de pivôs e centros; somente com erro de pivô `≤ 0,00001 m` em todos os fragmentos será possível preparar o pacote de promoção, ainda sem copiar ou alterar os FBX/prefabs de produção nesta frente.
