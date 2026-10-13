# Proposta segura — catálogo urbano

## Escopo

Esta etapa prepara cinco construções novas sem alterar `BuildingData`, `BuildingEraDecoration.cs`, `BuildingPlacer` ou o catálogo carregado pelo jogo:

- `market` — mercado;
- `warehouse` — armazém;
- `barracks` — quartel;
- `blacksmith` — ferreiro;
- `mill` — moinho.

O manifesto complementar está em `Docs/urban-building-catalog.json`. Ele é documentação de produção e **não é carregado pelo runtime**.

## Análise concreta do contrato atual

`BuildingData` herda de `UnitData` e possui apenas:

- `ambientSound` (`AudioClip`);
- `constructionMeshes` (`Mesh[]`).

Os campos que cada construção precisa preencher vêm de `UnitData`:

| Campo | Uso atual | Regra para os novos assets |
|---|---|---|
| `code` | Identificação de compra e save | IDs únicos, minúsculos, estáveis |
| `unitName` | Nome exibido | Nome localizado posteriormente |
| `description` | Texto da unidade | Descrição curta, sem lógica |
| `healthpoints` | Vida/construção | Valor positivo e coerente com o papel |
| `prefab` | Instância visual e colisão | Prefab jogável com `BuildingManager` |
| `sprite` | Ícone de UI | Não pode ficar ausente na entrega final |
| `cost` | Compra | Pelo menos um recurso e valores positivos |
| `canProduce` | Produção | Só preencher se o prédio realmente produzir algo |
| `skills` | Ações da UI | Referências válidas e compatíveis com o prédio |
| `fieldOfView` | Visão | Valor positivo; zero somente se deliberado |
| `attackRange`, `attackDamage`, `attackRate` | Ataque automático | Zero para prédios civis; valores explícitos para defesa |
| `onSelectSound`, `ambientSound` | Áudio | Opcional, mas validar referências antes da integração |
| `constructionMeshes` | Visual durante construção | Três estágios ou conjunto equivalente, na ordem crescente |

O carregamento atual é automático: `DataHandler.LoadBuildingData()` usa
`Resources.LoadAll<BuildingData>("ScriptableObjects/Units/Buildings")`. Por isso,
os cinco `.asset` só devem ser criados depois de seus prefabs, ícones, custos e
skills passarem pela auditoria. Um asset parcial nessa pasta entra no catálogo e
pode quebrar a UI ou o placement.

## Proposta de produção por prédio

| ID | Função | Produção/skills | Requisitos visuais |
|---|---|---|---|
| `market` | economia e troca | skill de troca/compra, sem unidade militar | praça coberta, bancas, toldos, estoque visível |
| `warehouse` | armazenamento | capacidade/estoque; sem unidade militar | volume baixo, portas largas, caixas e carroças |
| `barracks` | produção militar | skills de produção de soldado/arqueiro quando existirem | pátio, armas penduradas, entrada larga |
| `blacksmith` | melhoria tecnológica | skills de tecnologia quando existirem | forja, chaminé, bigorna e braseiro |
| `mill` | produção agrícola | skill de produção agrícola quando existir | torre, pás, telhado e área de trabalho |

Cada prédio deve ter um prefab por unidade lógica. A progressão Ancient →
Classical → Medieval → Imperial deve ser visual, usando variantes de material,
telhado, pedra e metal no próprio prefab ou em um componente novo isolado.
Não se deve duplicar lógica em `BuildingEraDecoration` nesta etapa.

## Critérios de aceite antes de registrar qualquer asset

1. O prefab abre sem `Missing (Mono Script)` e contém exatamente um fluxo de
   placement compatível com o `BuildingManager` existente.
2. O collider cobre a base, não tem collider adicional em detalhes decorativos
   e permite posicionamento em terreno inclinado conforme as regras atuais.
3. Existem três meshes de construção ou uma justificativa documentada para o
   número usado.
4. `code` é único entre todos os assets retornados por `LoadAll`.
5. `prefab`, `sprite`, custos e skills apontam para objetos existentes.
6. O prédio pode ser comprado, colocado, construído e salvo/carregado sem
   modificar o formato de `GameData`.
7. EditMode, PlayMode e uma captura visual sem magenta passam antes do merge.

## Ordem segura

1. Mercado e armazém — prédios civis, menor risco de dependências.
2. Quartel — depende das skills/unidades produzíveis.
3. Ferreiro — depende da árvore tecnológica final.
4. Moinho — depende da regra econômica agrícola.
5. Só então criar e colocar os cinco `BuildingData` em `Resources`.

## Fora do escopo desta etapa

- Não editar `BuildingData`.
- Não editar `BuildingEraDecoration.cs`.
- Não registrar IDs no catálogo runtime.
- Não criar prefabs fictícios ou assets com referências nulas.
- Não alterar Build Settings, cenas ou saves.
