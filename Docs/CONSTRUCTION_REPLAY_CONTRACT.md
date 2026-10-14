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
