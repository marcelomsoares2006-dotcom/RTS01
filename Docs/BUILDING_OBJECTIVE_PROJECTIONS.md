# Building objective projections

`BuildingObjectiveProjectionRules` is the shared, prefab-free contract for construction UI and strategic AI.

Each catalog entry is projected with its asset origin, runtime readiness, declarative availability, and lifecycle state. `constructionEligible` means the catalog and era/technology/building requirements allow a construction command. `objectiveEligible` is stricter: the owner's observed building must also be operational and runtime-ready. Planned or model-intermediate entries remain visible for roadmap/UI purposes, but resolve to `ContentUnavailable` and cannot count toward objectives.

Territory and victory adapters use the same boundary. Catalog-aware territory evaluation filters out entries without runtime-ready `BuildingData`; catalog-aware victory evaluation rejects required entries that are planned or intermediate before evaluating control, hold time, era, population, and technology. Production already uses the same catalog gate.

The contracts are deterministic and editor-free. Unity compilation, Test Runner execution, scene deserialization, and player builds remain required before treating the integration as runtime validated.
