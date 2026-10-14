# First playable construction slice

This gate covers the first two buildings that make the economy loop playable:
Town Center (`town_center`) and the generic Resource Dropoff
(`resource_dropoff`). The catalog already describes their costs, resource
coverage and prerequisites, but their visual/runtime assets remain planned
until authored content is imported.

Run the editor-free gate from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Validate-FoundationAssets.ps1
```

The command must fail while either entry is planned or an asset is missing.
Use `-ReportOnly` to print every outstanding issue while preparing the assets:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Validate-FoundationAssets.ps1 -ReportOnly
```

The gate reports `model-intermediate` when a foundation folder contains only
the repository's graybox OBJ sources. That state is useful for art handoff but
still fails the gate; the next required step is an authored FBX/GLB import and
Unity prefab assembly.

## Repository acceptance

For each entry, the gate requires:

- catalog status `Playable` or `Validated`;
- a `BuildingData` GUID that resolves to an asset whose `code` matches the
  catalog code;
- a prefab path and `.meta` file;
- a model path containing the authored model set;
- a prefab reference and at least one construction mesh in `BuildingData`;
- a Unit-layer object (layer 12) and a Unit-tagged object in the prefab.

## Unity acceptance

After the repository gate passes, Unity 6000.6.3f1 must verify the serialized
prefab and runtime behavior:

- root has `BuildingManager` and `BuildingBT` and the manager references a
  `MeshRenderer`/`MeshFilter`;
- a non-trigger placement collider and trigger footprint are present and do
  not include the preview's own collider in blocking checks;
- `BuildingData.prefab`, `constructionMeshes`, `maxConstructors`, terrain
  probe distance, minimum supported corners and placement spacing are set;
- Town Center accepts Food/Wood/Gold/Stone, produces `villager`, provides
  population capacity and garrison capacity;
- Resource Dropoff accepts its declared resources and requires an operational
  Town Center;
- placement succeeds on valid terrain, rejects steep/blocked/overlapping
  terrain, consumes the declared cost once, and enters construction with a
  visible feedback state;
- construction reaches operational state, releases constructors, and the
  resulting building appears in the strategic snapshot and save/load path.

The gate does not create placeholders or infer missing components from names.
Missing authored assets remain an explicit blocker until Blender/Unity content
is available and passes both repository and Unity validation.

## Procedural graybox handoff

The repository may contain `TownCenter_graybox.obj` and
`ResourceDropoff_graybox.obj` under the documented model folders as temporary
shape references. They are source geometry only: they have no materials,
production prefab, `BuildingData`, construction stage meshes, colliders,
sockets, LODs, or Unity component wiring. The catalog must remain planned while
these files are present. An artist may replace them with authored FBX/GLB
content, then follow the repository and Unity acceptance gates above.
