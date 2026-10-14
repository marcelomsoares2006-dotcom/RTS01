# Placement preview feedback contract

`BuildingPlacementPreviewRules` is the presentation boundary for construction
preview. It translates pure placement results into four stable UI values:

- `canPlace`, which is the only authorization signal;
- `resultKey`, a compact telemetry/UI state;
- `messageKey` and a fallback English message;
- `iconKey`, a deterministic sprite key.

`Allowed` is the only result that sets `canPlace = true`. Planned or
intermediate content produces `ContentNotRuntimeReady` or `AuditOnly` and is
always shown as non-placeable. Wall and gate `PlannedOnly` results follow the
same rule. Geometry, spacing, ownership, prerequisites and resource failures
map to stable error keys without requiring a prefab or a visual asset.

Localization and icon assets can be added later by mapping the keys. They must
not infer authorization from color, text or the existence of a preview mesh.
