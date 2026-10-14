# Building content catalog

The authoritative roadmap is `Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset`.
It separates content that can be offered to the runtime from content that is still a design contract. Planned entries keep their expected prefab/model paths as text and intentionally keep `buildingData` empty; this makes missing content visible without introducing broken Unity references.

## Assets currently present

| Building | Data | Prefab | Model | Status |
| --- | --- | --- | --- | --- |
| House | `Assets/Resources/ScriptableObjects/Units/Buildings/House.asset` | `Assets/Resources/Prefabs/Units/Buildings/House.prefab` | `Assets/Resources/Imports/Units/Buildings/house.fbx` plus two construction meshes | Playable |
| Tower | `Assets/Resources/ScriptableObjects/Units/Buildings/Tower.asset` | `Assets/Resources/Prefabs/Units/Buildings/Tower.prefab` | `Assets/Resources/Imports/Units/Buildings/tower.fbx` plus two construction meshes | Prototype |

The project also has building materials, selection/ambient audio and the `BuildHouse`/`BuildTower` skills. The essential roster is now represented by explicit planned contracts for Town Center, resource dropoffs, Barracks, Stable and Archery Range. Their `buildingData` fields remain empty until the corresponding authored assets are imported, so the catalog cannot create broken Unity object references.

## Essential planned roster

| Building | Minimum era | Construction cost | Production/dropoff contract | Asset state |
| --- | --- | --- | --- | --- |
| Town Center | Dark | 400 Wood | Villager; Food/Wood/Gold/Stone dropoff | Planned |
| Resource Dropoff | Dark | 50 Wood | Food/Wood/Gold/Stone dropoff | Planned |
| Barracks | Dark | 150 Wood | Spearman | Planned |
| Archery Range | Feudal | 150 Wood | Archer | Planned |
| Stable | Feudal | 150 Wood | Horseman | Planned |
| Mining Camp | Dark | 50 Wood | Gold/Stone dropoff | Planned |
| Lumber Camp | Dark | 50 Wood | Wood dropoff | Planned |
| Mill | Dark | 50 Wood | Food dropoff | Planned |

## First content gate

The first playable building slice is Town Center, House, one generic resource dropoff family, and one production building. A planned entry becomes `Playable` only after it has a `BuildingData`, prefab, model set, construction stages, placement footprint and editor validation. The catalog does not add planned entries to construction runtime automatically.

Town Center and the essential roster now carry explicit gameplay contracts in the catalog asset. Town Center is specified at 400 Wood, supports Food/Wood/Gold/Stone dropoff, provides 10 population capacity, garrisons 5 units and produces `villager` at 50 Food per 20 seconds with a queue limit of 5. Resource Dropoff is specified at 50 Wood and accepts the same four economy resource types. Barracks, Archery Range and Stable specify 150 Wood construction and Spearman, Archer and Horseman production respectively. Mining Camp, Lumber Camp and Mill specify 50 Wood construction and specialized resource routing. These values are design data for the upcoming runtime adapters; they do not imply that a missing prefab is ready for placement.

Town Center acceptance requires worker production, population support, resource dropoff, garrison, starting-base placement and save/load identity. Resource dropoff acceptance requires resource-type routing and worker access rules. Barracks acceptance requires a production queue, production time, unit output and age/technology requirements. These are contracts for later integration; this change does not alter economy runtime.

## Validation

`BuildingContentCatalogTests` checks stable unique codes, required foundation entries, runtime readiness rules, playable prefab references, the runtime-ready set and the essential roster's era, construction cost, production and Town Center prerequisite contracts. `BuildingContentCatalog.ValidateGameplayContracts()` is the data-level gate for construction costs, production records, production costs, non-empty requirements, dropoff coverage and Town Center population/garrison requirements. The editor-free validator also requires all essential roster codes and positive costs. Unity Editor execution is still required in an installed/licensed Unity 6000.6.3f1 environment.
