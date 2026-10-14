# Building repair and demolition contract

`BuildingRepairDemolitionService` is the deterministic, engine independent
boundary for maintenance operations. It is intentionally separate from
presentation, assets, and UI so the same ownership and transaction rules can
be used by runtime, save/load, replays, and AI.

Repair accepts damaged buildings and, when `allowDestroyedRepair` is enabled,
destroyed buildings. The configured cost is reserved atomically at start.
Progress raises health deterministically and completion returns the building to
`Operational`. Cancellation restores the original health and lifecycle and
refunds the full reservation.

Demolition reserves its configured cost, then changes the building to
`Ruined` only after the full duration elapses. The optional refund is granted
only at completion and is scaled by `demolitionRefundRatio`. Cancellation
restores the original state and refunds the demolition reservation.

Every mutation requires the owning player id. Invalid owner, state, duration,
cost, or affordability failures leave both the operation and stockpile intact.
The runtime integration can later call the service from `Building` without
coupling the rules to Unity objects or adding UI behavior.
