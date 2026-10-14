# Building garrison contract

`BuildingContentSpec` is the content source of truth for garrison behavior.
`supportsGarrison` must be true and `garrisonCapacity` must be greater than zero
before a building can accept a unit. `allowedUnitCodes` is compared
case-insensitively after trimming whitespace; an empty list means any non-empty
unit code is allowed.

`BuildingGarrisonRules` returns a stable `BuildingGarrisonResult` for every
request. Entry requires an operational building, a valid unit code, matching
owner IDs, available capacity, and an allowed code. Exit requires a valid
contract, matching owner IDs, and membership in that building. Invalid or stale
inputs return a rejection code and do not mutate state.

`Building.TryEnterGarrison` and `TryExitGarrison` keep the accepted runtime
membership on the building. The contract intentionally does not create assets,
move transforms, alter economy, or update UI; those integrations can consume
the accepted/rejected result in a later milestone.
