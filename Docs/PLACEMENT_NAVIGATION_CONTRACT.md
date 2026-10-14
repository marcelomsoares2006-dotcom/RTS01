# Placement navigation contract

`PlacementNavigationRules` is the editor-free gate for footprint and movement
space. Runtime adapters provide footprint clearance, NavMesh readiness,
obstacle readiness and corridor measurements; the rule never probes Unity
physics itself.

Content must be runtime-ready before navigation can authorize placement. A wall
or gate marks `corridorRequired` and must provide a clear corridor at least as
wide as both its configured minimum and twice the unit radius. Missing NavMesh,
stale obstacle data, blocked corridors and invalid footprints produce stable
feedback codes through `BuildingPlacementPreviewRules`.

The preview feedback remains non-authorizing for every failure and for
intermediate content. Unity integration still needs runtime evidence that
NavMeshSurface updates and obstacle carving complete before the adapter reports
readiness.
