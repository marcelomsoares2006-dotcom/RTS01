# Production save restore ownership

`BuildingProductionSaveRules.Restore` is an atomic replacement boundary. It
rejects a restore when the destination queue already contains orders, returning
`QueueNotEmpty`. The live orders retain ownership of their resource and
population reservations, so a failed load cannot silently orphan reservations
or double spend them. Callers must cancel/refund the live queue first, or use a
fresh queue, before restoring a snapshot.
