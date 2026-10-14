# Building construction save contract

`GamePlayerUnitData.construction` is the forward-compatible save boundary for
one building. It stores the stable building code and owner, transform position,
construction progress, maximum HP, lifecycle, constructor IDs, reserved costs,
reservation ID/population, and the content origin/readiness flag.

The snapshot is detached from Unity objects. It never serializes a prefab,
`BuildingData`, mesh, or scene reference. Legacy saves continue to use the
existing scalar fields when `construction` is null.

## Restore gate

`BuildingConstructionSaveRules.Validate(snapshot, ownerId, true)` is the
runtime gate. It rejects malformed records, owner mismatches, invalid costs, and
planned or model-intermediate content with `ContentUnavailable`. A save may
retain an intermediate blueprint for audit or migration (`requireRuntimeReady`
`false`), but `DataHandler` skips that record during scene instantiation. This
prevents a save file from promoting an OBJ/intermediate entry into an authored
runtime asset.

## Acceptance checklist

- A blueprint round trip retains code, owner, position, lifecycle, construction
  HP, and maximum HP.
- Worker IDs are trimmed and deduplicated, then reconnected only after all
  characters have been restored.
- Reserved costs are normalized by resource and reject negative or overflowing
  entries.
- The runtime `Building` captures the effective cost after the same buy-cost
  technology multiplier used by placement, and clears it when construction
  becomes operational.
- `RuntimeReady` is required for scene restoration; `Planned` and
  `ModelIntermediate` remain persisted but blocked.
- Unity/asset validation remains a separate gate. The save contract does not
  fabricate prefabs, `BuildingData`, meshes, or BuildingData GUIDs.
- Legacy records with no nested snapshot remain readable through the original
  `constructionHP`, `buildingLifecycle`, and `constructorIds` fields.

Run `Tools/Validate-EditorFree.ps1` after changes. Unity play-mode evidence is
still required to confirm visual reconstruction, worker AI reattachment, and
reservation refunds against authored assets.

## Cancellation and demolition boundary

`BuildingConstructionCancellationRules.TryCancel` accepts only `Blueprint` and
`UnderConstruction`. It refunds normalized reserved costs using the supplied
ratio, clears saved worker IDs, clears the reservation, and emits a detached
`Ruined` snapshot. `Operational`, `Damaged`, `Destroyed`, and `Ruined` states are
rejected by this construction cancellation path; they belong to the separate
repair/demolition contract.

Runtime cancellation requires `RuntimeReady` content. An intermediate snapshot
can be audited with the explicit `requireRuntimeReady: false` option, while the
default path returns `ContentUnavailable` and does not mutate the stockpile.
