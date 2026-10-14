# Concurrent construction reservations

`ConstructionReservationLedger` is the shared transaction boundary for
construction and production requests. It normalizes costs by resource, sorts a
batch by descending priority and stable reservation ID, checks the aggregate
resource/population demand before debiting, and records each reservation.

If a later request cannot be funded, every earlier debit in that batch is
rolled back. Cancellation refunds the exact normalized cost and releases the
population reservation; `Commit` removes the record after the operation owns the
spent resources. Duplicate IDs, negative costs, insufficient resources and
population caps never mutate the ledger.

Construction save snapshots carry reservation ID/population fields alongside
reserved costs. The reservation ledger remains detached from prefabs and can be
replayed by a runtime adapter after content readiness has been validated.
