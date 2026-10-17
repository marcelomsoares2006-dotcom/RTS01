# Town Center — showcase isolado / silhueta procedural

Data: 2026-10-17

## Escopo

Foram atualizados somente o builder e a cena do showcase: `Assets/Editor/CityArchitecture/CityArchitectureShowcaseBuilder.cs` e
`Assets/Scenes/CityArchitectureShowcase.unity`.
O Town Center de produção continua sendo apenas uma referência carregada com
`PrefabUtility.LoadPrefabContents`; seu `Mesh`, scripts, `BuildingData`, colisores
e prefab não são instanciados nem salvos de volta.

## Melhoria aplicada

`AddTownCenterVisualCandidate` agora cria uma cópia visual procedural chamada
`TownCenter_VisualCandidate`, composta por uma massa arquitetônica contínua:

- plinto e nave escalonada com alas conectadas;
- keep central e telhados de duas águas com cumeeira legível;
- torres laterais integradas, entrada frontal, janelas, eixo vertical e bandeira;
- materiais somente `Showcase_*` e objetos sem collider de gameplay.

O objetivo é remover a leitura de módulo fragmentado da composição anterior, sem
promover a solução para o gameplay.

## Segurança verificada

- o check focado no builder/relatório passou; o `git diff --check` global reporta
  campos serializados vazios gerados pelo Unity na cena e uma linha trailing de
  relatório preexistente, sem indicar problema no código procedural;
- diff em prefabs de produção: **NONE**;
- `SaveAsPrefabAsset`, `ApplyPrefabInstance` e `SavePrefabAsset`: ausentes no builder;
- a cena persistida foi regenerada pelo builder; o diff contém a cena visual, sem
  diff em prefab/FBX de produção.

## Captura D3D11

O comando Unity 6000.6.3f1 foi executado com `-force-d3d11` e
`CityArchitectureShowcaseBuilder.BuildAndCaptureViews`, registrado em
`TestResults/towncenter-showcase-d3d11.log`, com exit code 0. O log confirma:

- `Forcing GfxDevice: Direct3D 11`;
- `Renderer: Radeon RX 580 Series`;
- `CITY_DENSITY_VISUAL_VOLUMES=100`;
- `SHOWCASE_MULTI_VIEW_CAPTURED=4`.

Capturas novas (1920×1080):

- `TestResults/CityArchitectureShowcase_Overview.png`;
- `TestResults/CityArchitectureShowcase_Mid.png`;
- `TestResults/CityArchitectureShowcase_Close.png`;
- `TestResults/TownCenter_Showcase_D3D11.png` — vista hero do Town Center.

A inspeção visual da vista hero confirma a silhueta contínua (plinto, nave,
torres, telhados, pórtico e bandeira), sem os vazios que faziam a cópia anterior
parecer um conjunto de módulos.

Nota: o audit genérico da cidade registra `CityDensity gate visual-only: FAIL`
porque encontrou 4 `MonoBehaviour` na cena composta; isso não corresponde a
scripts no Town Center procedural e não altera o escopo desta frente.
