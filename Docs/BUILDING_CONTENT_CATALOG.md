# Building content catalog

The authoritative roadmap is `Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset`.
It separates content that can be offered to the runtime from content that is still a design contract. Planned entries keep their expected prefab/model paths as text and intentionally keep `buildingData` empty; this makes missing content visible without introducing broken Unity references.

## Assets currently present

| Building | Data | Prefab | Model | Status |
| --- | --- | --- | --- | --- |
| House | `Assets/Resources/ScriptableObjects/Units/Buildings/House.asset` | `Assets/Resources/Prefabs/Units/Buildings/House.prefab` | `Assets/Resources/Imports/Units/Buildings/house.fbx` plus two construction meshes | Playable |
| Tower | `Assets/Resources/ScriptableObjects/Units/Buildings/Tower.asset` | `Assets/Resources/Prefabs/Units/Buildings/Tower.prefab` | `Assets/Resources/Imports/Units/Buildings/tower.fbx` plus two construction meshes | Prototype |

The project also has building materials, selection/ambient audio and the `BuildHouse`/`BuildTower` skills. No Town Center, resource dropoff, Barracks, Stable or Archery Range prefab/model/data asset was found in the project at the time this catalog was created.

## First content gate

The first playable building slice is Town Center, House, one generic resource dropoff family, and one production building. A planned entry becomes `Playable` only after it has a `BuildingData`, prefab, model set, construction stages, placement footprint and editor validation. The catalog does not add planned entries to construction runtime automatically.

Town Center acceptance requires worker production, population support, resource dropoff, garrison, starting-base placement and save/load identity. Resource dropoff acceptance requires resource-type routing and worker access rules. Barracks acceptance requires a production queue, production time, unit output and age/technology requirements. These are contracts for later integration; this change does not alter economy runtime.

## Validation

`BuildingContentCatalogTests` checks stable unique codes, required foundation entries, runtime readiness rules, playable prefab references and the runtime-ready set. Unity Editor execution is still required in an installed/licensed Unity 6000.6.3f1 environment.
