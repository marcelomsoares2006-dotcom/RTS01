# Unity foundation authoring

`FoundationBuildingAuthoringWindow` is an editor-only draft authoring aid for
Town Center and Resource Dropoff. Open it from `Tools > Era Imperial >
Buildings > Foundation Authoring` after Unity imports the authored GLBs.

The tool audits each source for a usable `MeshFilter` and `MeshRenderer`, then
can create `_DRAFT` prefabs and `_DRAFT` `BuildingData` assets. It configures
the Unit layer/tag, `BuildingManager`, `BuildingBT`, trigger footprint,
non-trigger placement collider, kinematic rigidbody and NavMesh obstacle. Draft
assets are deliberately separate from the catalog: promotion remains a manual
step after the acceptance checklist and PlayMode placement/construction test.

The tool does not infer a final balance, assign the catalog references or mark
either building playable. Review materials, scale, construction meshes,
population/dropoff/production data and runtime behavior in Unity before
promoting the entries.
