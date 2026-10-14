# Siege Workshop military contract

`SiegeWorkshopMilitaryContract` is the shared boundary for the planned
`siege_workshop` production building. Its catalog entry is Castle age in the
current roster, while the contract accepts either Feudal or Castle as a valid
catalog minimum so a balance pass can move the unlock without changing the
runtime adapter.

The entry must cost exactly 300 Wood, require an operational Town Center, be
runtime-ready, and expose a valid production list. The first valid siege
product is selected deterministically: `battering_ram` is preferred, then the
lexicographically smallest valid product. Its cost, population reservation,
queue limit and production time are copied into a `ProduceUnit`
`ConstructionReplayCommand`.

The same evaluation is available to strategic AI through
`TryCreateInitialSiegeWorkshopIntent`, and editor tests cover Feudal/Castle
gating, operational ownership, deterministic selection, replay fingerprint
stability, costs and rejection of unsupported Imperial-only catalog entries.
