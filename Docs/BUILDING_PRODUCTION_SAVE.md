# Production save restore ownership

`BuildingProductionSaveRules.Restore` is an atomic replacement boundary. It
rejects a restore when the destination queue already contains orders, returning
`QueueNotEmpty`. The live orders retain ownership of their resource and
population reservations, so a failed load cannot silently orphan reservations
or double spend them. Callers must cancel/refund the live queue first, or use a
fresh queue, before restoring a snapshot.

Snapshots also carry the era, asset-origin stage, and normalized unlocked
technology list. Restore rejects `StateMismatch` when any of those gates drift,
so replay/load cannot make a queue valid under a different progression state.

`BuildingProductionReplayRules.ComputeFingerprint` includes the catalog
runtime gate, era, technologies, population, stockpile, and queue orders. A
rejected production command must leave this fingerprint unchanged, providing a
small deterministic checkpoint for replay and save verification.

Fingerprint serialization uses ordinal technology ordering and invariant round
trip float formatting, so replay hashes remain identical across machine locale
settings.
