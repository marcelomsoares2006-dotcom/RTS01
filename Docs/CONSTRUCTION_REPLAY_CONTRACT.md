# Construction command replay

`ConstructionReplayLog` records construction, production, cancellation,
resource reservation and age-up commands using stable IDs, simulation ticks,
issued timestamps and priorities. Commands are cloned at append time and
canonically ordered by tick, timestamp, descending priority and command ID.

The log computes an FNV based fingerprint over the canonical payload. Replaying
the same log produces the same command ID sequence and fingerprint; a changed
payload returns `DivergentReplay`. Duplicate IDs, invalid timestamps, owners,
targets or quantities are rejected before they can alter the stream.

The contract is editor-free and does not execute Unity objects. Runtime adapters
apply the accepted commands to construction, production, reservation and era
systems while retaining this log as the deterministic audit trail.

`ConstructionReplayExecutor` is the detached reference executor. It produces a
serializable snapshot of active buildings, produced units, reservations and
era, then fingerprints that state. Replaying the same log twice yields equal
snapshots; a supplied snapshot with a different fingerprint returns
`DivergentReplay`. Age-up commands must advance exactly one era at a time.

When the economic overload is used, `ProduceUnit` commands consume their
declared resource and population payload through the same atomic reservation
ledger used by the live production queue. The temporary production reservation
is committed immediately, so it does not appear as a construction reservation;
an unaffordable or over-cap order fails atomically and restores the supplied
economy snapshot.
