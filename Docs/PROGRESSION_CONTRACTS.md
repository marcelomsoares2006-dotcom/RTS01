# Progression contracts

`ProgressionContracts.cs` defines deterministic rules shared by era UI, strategic AI and construction commands. It contains no model, prefab, scene or visual content.

An era transition must advance exactly one era and satisfy population, operational landmark, operational building, technology and resource requirements. `TryAdvance` mutates `StrategicPlanningState.era` only after all checks pass.

Technology definitions use a stable code, minimum era, prerequisite technology codes, required operational buildings and a resource cost. `TryUnlock` appends a normalized code only after all checks pass and rejects duplicate unlocks deterministically.

Construction continues to use `BuildingCatalogResolver` and `BuildingConstructionGate`; the contracts consume the same `BuildingEra`, planning state, technology codes and operational building observations. Asset creation and visual production remain separate acceptance gates.
