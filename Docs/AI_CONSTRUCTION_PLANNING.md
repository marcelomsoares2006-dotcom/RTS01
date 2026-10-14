# Deterministic construction planning

`ConstructionPlanningRules` turns a detached `StrategicPlanningState` and the
`BuildingContentCatalog` into one `ConstructBuilding` intent. It never spends
resources or places a prefab; those responsibilities stay in the runtime
executor and placement gate.

The priority order is:

1. establish a playable and affordable Town Center when no owned foundation
   exists;
2. establish the first playable and affordable resource dropoff after the
   Town Center;
3. establish the first missing production building, in the stable preference
   order Barracks, Archery Range, Stable, then code order.

The rule checks era, population, technology and building prerequisites, ignores
catalog entries that are not runtime ready, and rejects costs that cannot be
represented by the legacy resource snapshot (including Food until the legacy
economy exposes it). Blueprints, buildings under construction and damaged
buildings count as existing to prevent duplicate orders; destroyed and ruined
buildings do not.

`BasicStrategicPlanner(BuildingContentCatalog)` enables this policy while the
parameterless planner remains compatible with existing goal-driven callers.
An explicit enabled `ConstructBuilding` goal always takes precedence over the
automatic policy.
