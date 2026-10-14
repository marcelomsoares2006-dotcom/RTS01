# Archery Range military contract

The Archery Range is the Feudal ranged production slice in the Era Imperial
construction roadmap. Its authoritative `BuildingContentCatalog` entry uses
an exact construction cost of **150 Wood**, requires an operational
`town_center` and the `fletching` technology, and produces `archer` for 30
Food and 50 Wood in 15 seconds.

`ArcheryRangeMilitaryContract.Evaluate` repeats the Feudal, technology,
construction, ownership and lifecycle gates at the runtime boundary. A
blueprint, enemy or damaged range, a pre-Feudal state, or an intermediate
asset cannot open the military contract.

`TryCreateInitialProductionCommand` projects the catalog production entry to
the deterministic `ConstructionReplayCommand` stream with a stable command
ID, population cost and sorted replay costs. `ConstructionPlanningRules`
exposes the same first-Archer choice to strategic AI, while
`BuildingProductionCatalogRules` remains the queue compatibility boundary.

The editor tests cover Feudal and Fletching gates, Town Center and range
lifecycle, exact cost, Archer selection, replay fingerprint stability, AI
projection and legacy production projection. Unity Test Runner execution
remains required when a licensed Unity 6000.6.3f1 editor is available.
