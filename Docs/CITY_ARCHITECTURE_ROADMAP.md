# Kit arquitetônico da cidade — Era Imperial

## Inventário atual

O catálogo jogável atual contém apenas `House` e `Tower`. Por isso, as próximas construções devem ser criadas como um kit coerente e integradas uma por vez ao `BuildingData`, ao `BuildingPlacer`, à navegação e aos testes.

## Famílias planejadas

### Núcleo urbano

- Casa popular e casa nobre
- Centro urbano / prefeitura
- Mercado e armazém
- Praça com poço, fonte e estandartes

### Produção

- Fazenda e moinho
- Serraria e pedreira
- Ferreiro
- Estábulo

### Militar

- Quartel
- Campo de tiro
- Oficina de cerco
- Torre de vigia
- Torre pesada
- Muro reto, canto, portão e ponte de ligação

## Linguagem visual por era

- **Ancient:** barro, madeira, palha, paliçada e formas simples.
- **Classical:** pedra cortada, colunas, pátios e telhados baixos.
- **Medieval:** madeira sobre pedra, contrafortes, telhas, ameias e portões pesados.
- **Imperial:** pedra aparelhada, metal, telhados monumentais, torres mais altas e detalhes de prestígio.

## Critérios de produção

1. Silhueta legível na câmera RTS e em miniatura.
2. Detalhes grandes primeiro; ornamentos pequenos somente depois.
3. Escala, pivot, UV, normais, LOD e materiais verificados antes de catalogar.
4. Colisão e NavMesh separados do acabamento visual.
5. Cada construção precisa de teste de placement, construção, save/load e PlayMode.

## Próxima etapa recomendada

Produzir o primeiro kit de muralha modular: `WallStraight`, `WallCorner`, `Gatehouse` e `WallTowerConnector`. Depois dele, criar o núcleo urbano e as construções de produção usando os mesmos materiais e regras de era.
