# Strategic AI planning contract

This increment introduces a side-effect-free planning layer under
`Assets/Scripts/StrategicAI`.

`StrategicPlanningState` is a snapshot of player-owned facts: era, population,
resources, buildings, units, technologies and goals. `IStrategicPlanner` turns
that snapshot into a `StrategicPlan`, which contains ordered
`StrategicActionIntent` values. `BasicStrategicPlanner` is deterministic and
only sorts enabled goals by priority and id.

The layer has no dependency on `Building`, `BuildingManager`, `UIManager`,
`GameData`, `DataHandler`, or scene objects. It cannot spend resources, place
buildings, move units, change a production queue or write a save. A future
adapter can translate intents into existing runtime commands after explicit
validation and ownership rules.

Current action kinds cover economy, construction, production, scouting,
research, defense and objectives. Resource reservations are visible to
planners without mutating the live economy.

The next safe step is a read-only adapter that builds the snapshot from runtime
observations, followed by an execution gateway returning accepted/rejected
command results. Keep both separate from save serialization.

`Assets/Tests/Editor/StrategicPlanningTests.cs` validates resource reservation,
goal de-duplication, deterministic ordering, disabled goals and side-effect-free
planning in the existing Unity Editor test assembly.

