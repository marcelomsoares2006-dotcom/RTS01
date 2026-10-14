# Strategic territory influence

`StrategicTerritoryInfluenceResolver` is a pure planning contract that reads a
`StrategicPlanningState` snapshot. It does not touch scene objects, UI, or
assets, so AI can evaluate territory without introducing runtime side effects.

Only buildings with `lifecycle == Operational`, a non-negative `ownerId`, and a
positive `influenceRadius` contribute. A building contributes `radius -
distance` at positions inside its radius. Scores are summed by owner. The
highest score controls a position when it exceeds the second highest by more
than `DisputeEpsilon`; otherwise the position is `Contested`. With no valid
contributors, it is `Neutral`.

`SampleField` returns a deterministic row-major field for map-level planning.
The observation radius is intentionally data-facing; runtime adapters can map
it from each building's catalog or faction rules in a later integration.
