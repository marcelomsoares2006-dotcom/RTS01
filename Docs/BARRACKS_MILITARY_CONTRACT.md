# Barracks military contract

The Barracks is the first military production slice in the Era Imperial
construction roadmap. Its authoritative `BuildingContentCatalog` entry is a
Dark Age production building with an exact construction cost of **150 Wood**
and an operational `town_center` requirement. The contract is deliberately
validated again at runtime so a blueprint, enemy building, damaged building or
intermediate asset cannot open a production queue.

`BarracksMilitaryContract.Evaluate` requires both an owned operational Town
Center and an owned operational Barracks, plus a runtime-ready `BuildingData`.
`TryResolveInitialProduction` chooses `spearman` whenever the catalog contains
it. A faction extension that omits Spearman receives the lexicographically
smallest valid production code, keeping the choice stable across machines.

`TryCreateInitialProductionCommand` projects that choice to the existing
`ConstructionReplayCommand` stream with a stable command ID, population cost
and sorted replay costs. `ConstructionPlanningRules.TryCreateInitialBarracksIntent`
exposes the same decision to the strategic AI, so UI, AI and replay do not
invent separate first-unit policies.

The editor tests cover Town Center and lifecycle gates, exact construction
contract, Spearman preference, stable replay fingerprints and AI projection.
Unity Test Runner execution remains required when a licensed Unity
6000.6.3f1 editor is available.
