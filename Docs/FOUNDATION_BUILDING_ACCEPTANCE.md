# First playable construction slice

This gate covers the first economy foundation buildings: Town Center
(`town_center`), the generic Resource Dropoff (`resource_dropoff`) and the
Wood-only Lumber Camp (`lumber_camp`). The catalog already describes their
costs, resource coverage and prerequisites, but their visual/runtime assets
remain planned until authored content is imported.
Gold/Stone Mining Camp (`mining_camp`) is included in the same authored-model
gate so both specialized dropoffs can be reviewed together. Food Mill (`mill`)
is included as the fifth economy foundation, and infantry Barracks (`barracks`)
is included as the first military production foundation. All use the same Unity
authoring and placement review contract.
Archery Range (`archery_range`) is included as the second military foundation;
its Feudal ranged-production contract is already present in the content catalog.
Stable (`stable`) is included as the third military foundation; its Feudal
cavalry-production contract is already present in the content catalog.

Run the editor-free gate from the project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Validate-FoundationAssets.ps1
```

The command must fail while any entry is planned or an asset is missing.
The machine-readable import plan is [`FOUNDATION_IMPORT_MANIFEST.yml`](FOUNDATION_IMPORT_MANIFEST.yml);
the gate checks that it exists, points to the intermediate sources and retains
the required Unity component and approval fields.
Use `-ReportOnly` to print every outstanding issue while preparing the assets:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/Validate-FoundationAssets.ps1 -ReportOnly
```

The gate reports `model-intermediate` when a foundation folder contains only
the repository's graybox OBJ sources. That state is useful for art handoff but
still fails the gate. An authored OBJ, FBX or GLB may coexist with the graybox
for traceability; it must be listed in the import manifest and still requires
Unity prefab assembly and review. This project currently has no glTFast
dependency, so the Unity-native OBJ output from
`Tools/GenerateFoundationAssets.mjs` is the default import route. GLBs remain
interchange and preview sources.

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
- Lumber Camp accepts Wood only, costs 50 Wood and requires an operational Town
  Center through `LumberCampEconomyContract`;
- Mining Camp accepts Gold and Stone, costs 50 Wood and requires an operational
  Town Center through the existing mining dropoff contract;
- Mill accepts Food, costs 50 Wood and requires an operational Town Center;
- Barracks trains the initial infantry roster, costs 150 Wood and requires an operational Town Center;
- Archery Range trains the initial ranged roster after Feudal `fletching`, costs 150 Wood and requires an operational Town Center;
- Stable trains the initial cavalry roster after Feudal `riding`, costs 150 Wood and requires an operational Town Center;
- placement succeeds on valid terrain, rejects steep/blocked/overlapping
  terrain, consumes the declared cost once, and enters construction with a
  visible feedback state;
- construction reaches operational state, releases constructors, and the
  resulting building appears in the strategic snapshot and save/load path.

The gate does not create placeholders or infer missing components from names.
Missing authored assets remain an explicit blocker until Blender/Unity content
is available and passes both repository and Unity validation.

`FoundationPlacementRules` provides the editor-free geometry/economy contract
before those assets exist. It validates the foundation role, era and catalog
requirements, footprint radius, terrain normal/slope, spacing from existing
buildings, construction costs after reservations, Town Center capacity and
dropoff resource coverage. A Unity adapter must supply the terrain and
positions and still run the prefab-level review described above.

Runtime authorization is closed by default: a planned entry returns
`ContentNotRuntimeReady`. An explicit design audit with
`requireRuntimeReady = false` returns `AuditOnly`, which cannot be interpreted
as permission to place the building. Economy routing follows the same rule and
returns `ContentNotRuntimeReady` until the dropoff has a runtime-ready asset.

## Procedural graybox handoff

The repository may contain graybox OBJ sources under the documented model
folders as temporary shape references. Authored OBJ and GLB sources are
generated reproducibly by
`Tools/GenerateFoundationAssets.mjs`; the OBJ is the Unity-native import
route, while the GLB provides interchange and preview. Both provide named
parts, normals and PBR material intent, but neither creates a production prefab,
`BuildingData`, construction stage meshes, colliders, sockets, LODs, or Unity
component wiring. The catalog must remain planned until the authored assets are
imported and reviewed in Unity.

For a host without Unity, `Tools/FoundationPreviewServer.mjs` serves a local
Three.js preview of all nine foundation GLBs. This preview is an art sanity check only; it
does not replace Unity import, prefab serialization or PlayMode acceptance.
