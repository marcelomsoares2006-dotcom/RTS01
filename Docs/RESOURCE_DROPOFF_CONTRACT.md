# Resource Dropoff contract

`ResourceDropoffContract` is the deterministic boundary between worker
delivery and the economy ledger. It is built from `BuildingContentSpec`, so
the catalog remains the source of truth for accepted resources.

Each delivery must satisfy all of these rules:

- the catalog entry enables dropoff and lists the resource;
- the dropoff is operational;
- the worker owner matches the building owner;
- the worker is within the horizontal access radius;
- the amount is positive and a target `EconomyLedger` exists.

Accepted deliveries are added atomically to the target ledger. Rejected
deliveries do not mutate it. The legacy `InGameResource` overload maps only
Gold, Wood and Stone. Food has no legacy representation and incompatible
legacy values are rejected rather than coerced into another resource.

This contract deliberately does not create prefabs or move workers. Runtime
controllers, AI and save replay can call the same API after their movement or
state checks, keeping simulation behavior reproducible.

## Lumber Camp milestone

`LumberCampEconomyContract` specializes this boundary for the first economic
building milestone. Its catalog contract is exact: `lumber_camp` costs 50 Wood,
accepts Wood only, and requires an owned operational `town_center` before the
route can be created. The camp itself must also be operational and runtime-ready.

The contract can consume an `EconomyCollector` after the worker reaches the
access radius. It deposits the integer carried amount through
`ResourceDropoffContract`, preserving fractional carry for the next trip and
rejecting a collector assigned to another resource without mutating the ledger.
The construction planner uses the same operational prerequisite rule, so AI
intent cannot enqueue a dependent dropoff while the Town Center is still under
construction.

## Mining Camp milestone

`MiningCampEconomyContract` specializes this boundary for the Gold and Stone
route. Its catalog contract is exact: `mining_camp` is a Dark Age building
costing 50 Wood, accepts Gold and Stone only, and requires an owned operational
`town_center` before the route can be created. The camp itself must also be
operational and runtime-ready.

Gold and Stone deliveries use the same `ResourceDropoffContract` path as the
simulation and save/replay adapters. The collector deposits only its integer
carried amount, preserving fractional carry, and rejected Wood deliveries do
not mutate the ledger. `BuildingEconomyRules` validates both accepted resource
types, while `ConstructionPlanningRules` exposes the camp to AI only after the
Town Center is operational and the 50 Wood cost is affordable.
