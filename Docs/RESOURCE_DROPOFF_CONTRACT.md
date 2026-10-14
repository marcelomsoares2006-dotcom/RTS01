# Resource Dropoff contract

`ResourceDropoffContract` is the deterministic boundary between worker
delivery and the economy ledger. It is built from `BuildingContentSpec`, so
the catalog remains the source of truth for accepted resources.

Each delivery must satisfy all of these rules:

- the catalog entry enables dropoff and lists the resource;
- the dropoff is operational;
- the worker owner matches the building owner;
- the worker is within the horizontal access radius;
- the amount is positive and a target `EconomyLedger` exists.

Accepted deliveries are added atomically to the target ledger. Rejected
deliveries do not mutate it. The legacy `InGameResource` overload maps only
Gold, Wood and Stone. Food has no legacy representation and incompatible
legacy values are rejected rather than coerced into another resource.

This contract deliberately does not create prefabs or move workers. Runtime
controllers, AI and save replay can call the same API after their movement or
state checks, keeping simulation behavior reproducible.
