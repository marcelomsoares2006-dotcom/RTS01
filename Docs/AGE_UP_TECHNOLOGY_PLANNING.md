# Age-up and technology planning

`AgeUpTechnologyPlanningRules` emits one deterministic intent at a time. The
order is explicit: an operational `town_center`, missing required buildings,
missing technology prerequisites, and finally the `AdvanceEra` intent. A target
technology is only emitted when `TechnologyProgressionRules` confirms its era,
building, prerequisite, and available-resource gates.

The planner never unlocks a technology, changes the era, spends resources, or
creates a prefab. Those mutations belong to the runtime command adapter. Missing
technology definitions and insufficient resources produce a blocked rationale
instead of an unsafe intent. `Planned` and intermediate building content remain
subject to the existing runtime readiness gates.
