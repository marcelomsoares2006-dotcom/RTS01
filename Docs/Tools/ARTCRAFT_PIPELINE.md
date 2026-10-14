# ArtCraft no pipeline do Era Imperial

## Decisão

O ArtCraft é uma família de aplicativos open source. Para o projeto Unity, o componente útil neste momento é o **CADCraft 0.5.0**: ele trabalha com DXF/DWG, camadas, blocos, cotas e exportação PNG/SVG/PDF. Ele será usado para planejar cidades, muralhas, torres, estradas e áreas de construção antes da modelagem no Blender.

Ele não substitui o Blender no modelagem de malhas, UV, rigging ou exportação FBX. O pipeline permanece:

`CADCraft (planta e medidas) -> Blender/Mixar (malha, UV, materiais) -> Unity (prefab, colisão e NavMesh)`

## Instalação local

- Ferramenta: `D:\AI\ArtCraft\CADCraft\cadcraft-0.5.0-windows-x64-portable`
- CLI: `cadcraft-cli.exe --version` retorna `0.5.0`
- Projeto de desenhos: `D:\AI\ArtCraft\Projects\EraImperial`

## Teste realizado

Foi executado o CLI oficial com um desenho de amostra. Saídas:

- `D:\AI\ArtCraft\Projects\EraImperial\city-layout-sample.dxf`
- `D:\AI\ArtCraft\Projects\EraImperial\city-layout-sample.png`

## Uso recomendado

1. Criar no CADCraft a planta da cidade por camadas: `WALLS`, `GATES`, `TOWERS`, `ROADS`, `BUILDINGS`, `NAVMESH_GUIDE`.
2. Exportar DXF para manter medidas e PNG para revisão visual.
3. Reproduzir as medidas no gerador Blender/Unity, sem importar DXF diretamente como geometria de gameplay.
4. Conferir escala, acessos, largura de portões e corredores antes de criar colisores e NavMesh.

O uso inicial é separado e não altera cenas, prefabs ou scripts existentes.
