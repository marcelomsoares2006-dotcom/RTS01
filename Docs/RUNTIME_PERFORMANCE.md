# Runtime performance contracts

## Field-of-view target selection

`CheckEnemyInFOVRange` runs from unit behavior trees and can execute once per
tick for many units. Its query contract is:

- select the nearest enemy unit inside the configured field of view;
- ignore colliders without `UnitManager` and units owned by the evaluator;
- preserve the previous target data keys (`currentTarget` and
  `currentTargetOffset`);
- avoid a managed result-array allocation during normal evaluations.

The implementation uses `Physics.OverlapSphereNonAlloc` with a reusable buffer.
The buffer doubles when a dense scene fills it and falls back to the allocating
query only above 4096 colliders, preserving completeness under extreme load.
Nearest selection is a single linear scan, replacing the LINQ `Where`,
`OrderBy`, and `First` chain.

## Measurement protocol

Use the Unity Profiler in a representative battle scene and compare the
`CheckEnemyInFOVRange.Evaluate` marker over at least 10 seconds with the same
unit count, tick interval, and camera state. Record:

- GC Alloc per frame;
- main-thread time per frame;
- total physics query time;
- number of active units and colliders.

The expected result is zero managed result-array allocation in steady state and
lower main-thread time as unit count increases. The fallback path is an
exceptional correctness guard and should remain absent from normal profiler
captures.
