# Construction lockstep contract

`ConstructionLockstepCoordinator` is the command ingress for multiplayer or
replay lockstep. Every command requires a nonnegative owner and unique stable
ID. A tick lower than the last accepted tick is stale and rejected. Duplicate
IDs are rejected before the replay log changes.

Callers provide the fingerprint they last confirmed. A mismatch rejects the
command without mutation. Accepted commands return a confirmation containing
owner, command ID, highest confirmed tick, acceptance and the new canonical
fingerprint. The coordinator is editor-free and leaves resource spending and
scene mutation to the detached replay executor/runtime adapter.

Sessions may construct the coordinator with an expected owner ID, or pass the
expected owner to the overload of `Submit`; commands from another owner are
rejected before fingerprint or log mutation. Replay restore returns its exact
result and records `LastRestoreResult`; invalid commands and divergent state
leave `State` null with an explicit failure for the save/load caller to handle.
