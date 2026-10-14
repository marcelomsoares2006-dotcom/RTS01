# Unity foundation authoring

`FoundationBuildingAuthoringWindow` is an editor-only draft authoring aid for
Town Center, Resource Dropoff, Lumber Camp, Mining Camp, Mill, Barracks, Archery Range and Stable. Open it from `Tools > Era
Imperial > Buildings > Foundation Authoring` after Unity imports the authored
OBJ files.

The repository keeps the authored GLBs for Blender, web preview and future
pipeline interchange, but the project has no glTFast or other GLB importer in
`Packages/manifest.json`. Unity's built-in model importer reliably handles OBJ
and FBX, so the generator emits matching `*_authoring.obj` plus `.mtl` files
from the same deterministic geometry. The window prefers OBJ and only uses a
GLB when an optional importer has actually made it available through
`AssetDatabase`.

The tool audits each imported source for a usable `MeshFilter` and `MeshRenderer`, then
can create `_DRAFT` prefabs and `_DRAFT` `BuildingData` assets. It configures
the Unit layer/tag, `BuildingManager`, `BuildingBT`, trigger footprint,
non-trigger placement collider, kinematic rigidbody and NavMesh obstacle. The
draft `BuildingData` uses catalog-aligned construction costs (Town Center:
400 Wood; Resource Dropoff, Lumber Camp, Mining Camp and Mill: 50 Wood; Barracks, Archery Range and Stable: 150 Wood).
Resource routing remains
owned by the existing catalog/economy contracts, while the draft assets carry
the runtime prefab, construction mesh and placement references. Draft assets
are deliberately separate from the catalog: promotion remains a manual step
after the acceptance checklist and PlayMode placement/construction test.

The tool does not infer a final balance, assign the catalog references or mark
any building playable. Review materials, scale, construction meshes,
population/dropoff/production data and runtime behavior in Unity before
promoting the entries.
