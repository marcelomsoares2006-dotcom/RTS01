# Stable military contract

The Stable is the Feudal mounted production slice in the Era Imperial
construction roadmap. Its authoritative catalog entry costs **150 Wood**,
requires an operational `town_center` and the `riding` technology, and
produces `horseman` for 100 Food and 20 Wood in 23 seconds.

`StableMilitaryContract.Evaluate` repeats the Feudal, technology, exact cost,
ownership and lifecycle gates at the runtime boundary. A pre-Feudal state,
missing Riding, enemy or damaged Stable, missing Town Center, or intermediate
asset cannot open the military contract.

`TryCreateInitialProductionCommand` projects the first valid mounted product
to the deterministic `ConstructionReplayCommand` stream with a stable command
ID, population cost and sorted replay costs. `ConstructionPlanningRules`
exposes the same Horseman choice to strategic AI, while
`BuildingProductionCatalogRules` remains the queue compatibility boundary.

The editor tests cover Feudal and Riding gates, Town Center and Stable
lifecycle, exact construction cost, deterministic Horseman selection, replay
fingerprint stability, AI projection and legacy production projection. Unity
Test Runner execution remains required when a licensed Unity 6000.6.3f1 editor
is available.
