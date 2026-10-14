# Progression contracts

`ProgressionContracts.cs` defines deterministic rules shared by era UI, strategic AI and construction commands. It contains no model, prefab, scene or visual content.

An era transition must advance exactly one era and satisfy population, operational landmark, operational building, technology and resource requirements. `TryAdvance` mutates `StrategicPlanningState.era` only after all checks pass.

Technology definitions use a stable code, minimum era, prerequisite technology codes, required operational buildings and a resource cost. `TryUnlock` appends a normalized code only after all checks pass and rejects duplicate unlocks deterministically.

Construction continues to use `BuildingCatalogResolver` and `BuildingConstructionGate`; the contracts consume the same `BuildingEra`, planning state, technology codes and operational building observations. Asset creation and visual production remain separate acceptance gates.

`BuildingContentProgressionRules` now bridges the authoritative `Building Content Catalog` into that same decision path. It evaluates a content entry's minimum era, technology requirements, population and owned operational building requirements, and returns `ContentUnavailable` while a planned/prototype entry has no runtime-ready `BuildingData`. Passing `requireRuntimeReady: false` is reserved for design and progression audits, allowing planned entries to be inspected without making them constructible. Foreign-owner, incomplete and destroyed buildings never satisfy a prerequisite.

`ValidateEraContracts` audits the roster graph before runtime content is complete: Town Center is the Dark Age anchor; dropoff and production entries must require an operational Town Center; Feudal military production remains age-gated; and forward defenses may remain independent for strategic placement. This makes the intended Dark → Feudal unlock path executable and reviewable while the corresponding prefabs are still planned.

`TryResolveConstructionContract` returns a detached, prefab-free snapshot containing the stable code, role, minimum era, construction cost, requirements, production records and accepted dropoff resources. Town Center and Resource Dropoff commands can consume this snapshot for affordability and routing while their authored prefabs remain separate content gates.

`TryProjectEconomy` is the UI/AI read model. It exposes costs, villager or military production, population capacity, dropoff resources, era and availability while preserving the asset origin: `Planned`, `ModelIntermediate` or `RuntimeReady`. Entries with documented model/prefab paths but no valid `BuildingData` reference are `ModelIntermediate`; this state is visible to planning and review, yet still returns `ContentUnavailable` to placement.
