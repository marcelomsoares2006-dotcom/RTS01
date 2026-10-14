# Building objective projections

`BuildingObjectiveProjectionRules` is the shared, prefab-free contract for construction UI and strategic AI.

Each catalog entry is projected with its asset origin, runtime readiness, declarative availability, and lifecycle state. `constructionEligible` means the catalog and era/technology/building requirements allow a construction command. `objectiveEligible` is stricter: the owner's observed building must also be operational and runtime-ready. Planned or model-intermediate entries remain visible for roadmap/UI purposes, but resolve to `ContentUnavailable` and cannot count toward objectives.

Territory and victory adapters use the same boundary. Catalog-aware territory evaluation filters out entries without runtime-ready `BuildingData`; catalog-aware victory evaluation rejects required entries that are planned or intermediate before evaluating control, hold time, era, population, and technology. Production already uses the same catalog gate.

Construction menu entries also expose a stable accessibility projection: a
status string, semantic label, keyboard shortcut (`B1`, `B2`, ...), icon key
derived from the stable content code, and a contrast class. Keyboard,
screen-reader, and high-contrast adapters can consume these values before a
visual panel or icon asset is authored.

The contracts are deterministic and editor-free. Unity compilation, Test Runner execution, scene deserialization, and player builds remain required before treating the integration as runtime validated.
