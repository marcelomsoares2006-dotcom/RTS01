# Building feedback contract

`BuildingFeedbackEmitter` gives every runtime building a single, testable
feedback stream for construction progress, health and lifecycle state.

## States

| Lifecycle | Visual cue | Audio cue | Existing asset used |
|---|---|---|---|
| Blueprint | Blueprint | None | Existing placement material |
| UnderConstruction | UnderConstruction | ConstructionLoop | `Resources/Sounds/Units/Buildings/construction_site.wav` |
| Operational | Operational | OperationalAmbient | Building `ambientSound` (`house-ambient.mp3` where configured) |
| Damaged | Damaged | DamageWarning | Existing construction site clip as a temporary fallback |
| Destroyed/Ruined | Destroyed | Stopped | Existing final mesh is hidden before the unit is removed |

The feedback snapshot exposes normalized `constructionProgress` and
`healthRatio`, so UI, accessibility narration, telemetry and future VFX can
subscribe through `FeedbackChanged` without reading private `Building` fields.

The damage cue deliberately reuses the existing construction audio because the
project currently has no dedicated damage or destruction clip. No model,
texture, sound or VFX asset is fabricated by this integration.
