# Scenario catalog

Stable IDs for the required scenarios in root AGENTS.md. Fixtures, tests, specs, tasks and reports reference these IDs. Expected outcomes restate AGENTS.md rules; The constitution governs; AGENTS.md supplies consistent operational guidance. PDS state names are provisional.

## ID rules

- Format `<AREA>-NN`. Never renumber or reuse an ID; mark retired IDs `deprecated` and keep the row.
- Areas: INT all intake paths, H10 shared H10 webhooks, SAL Salou VIP POST, CAS Cassis West polling, BRK any broker (reused by forks), KFK Kafka-specific, PDS registration.
- Tag every test with scenario metadata supported by the chosen framework (for example, xUnit `[Trait("Scenario", "INT-03")]`). The framework is not selected yet. One test may cover several IDs; every in-scope ID needs at least one test. Preserve an explicit mapping to result IDs.
- Intake paths in "Applies to": H10 = H10 Central/East webhooks, SAL = Salou VIP POST, CAS = Cassis West poll.
- Fixture layout and file formats are decided in feature 001; until then "Fixtures" stays empty.
- Synthetic data only. Fixtures reference guests by synthetic IDs; never put identity data in this file.

## Intake (all paths)

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| INT-01 | First snapshot | H10, SAL, CAS | Booking applied and outbox row inserted in one transaction | | planned |
| INT-02 | Duplicate snapshot (equal updatedAt, identical normalized data) | H10, SAL, CAS | No booking or outbox effect | | planned |
| INT-03 | Stale snapshot (older updatedAt) | H10, SAL, CAS | Ignored; no booking or outbox effect | | planned |
| INT-04 | Conflicting snapshot (equal updatedAt, different data) | H10, SAL, CAS | Conflict recorded and surfaced; no tie-break; reconcile with source | | planned |
| INT-05 | Invalid snapshot | H10, SAL, CAS | Rejected with safe reason; no booking or outbox effect | | planned |
| INT-06 | Concurrent delivery of the same booking | H10, SAL, CAS | Atomic guard prevents duplicate effects per accepted snapshot; state never regresses and ends at the newest non-conflicting snapshot. Distinct increasing updates may each apply | | planned |

## H10 shared webhooks

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| H10-01 | Same booking ID in h10-central and h10-east | H10 | Two distinct bookings by providerId:propertyId:bookingId | | planned |
| H10-02 | Property not authorized for the integration | H10 | Rejected; no booking effect | | planned |
| H10-03 | Webhook retry of an accepted event | H10 | Acknowledged again; no duplicate effect | | planned |
| H10-04 | Missing event | H10 | Reconciliation retrieves a provider update Kafka never received; detection cadence and provider query contract are specified per feature | | planned |

## Salou VIP POST

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| SAL-01 | POST retried after client timeout | SAL | Retry after durable acceptance produces no duplicate effect; response semantics are defined in the intake spec | | planned |
| SAL-02 | Unauthorized property | SAL | Rejected; no booking effect | | planned |

## Cassis West polling

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| CAS-01 | Multi-page poll | CAS | All valid page items durably accepted; cursor advances only past accepted items/pages; invalid-item handling is defined in the intake spec | | planned |
| CAS-02 | Overlapping timestamp boundaries between polls | CAS | Boundary re-reads do not duplicate effects; all eligible records are accepted under documented provider ordering, pagination and timestamp guarantees | | planned |
| CAS-03 | Poll interrupted mid-pagination | CAS | Cursor not advanced past unaccepted pages | | planned |
| CAS-04 | Restart with durable cursor | CAS | Resumes from last durable cursor; no gap, no duplicate effect | | planned |

## Broker (any broker; forks reuse)

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| BRK-01 | Crash after DB commit, before ack | all | Redelivered; no duplicate booking or outbox effect | | planned |
| BRK-02 | Redelivery of an applied message | all | No duplicate booking or outbox effect | | planned |
| BRK-03 | Repeated processing failure | all | Bounded retries, then dead-letter path | | planned |
| BRK-04 | Dead-letter handoff | all | Dead letter durably stored before failed work is acknowledged | | planned |

## Kafka

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| KFK-01 | Keyed ordering | all | Same stable key routes to one partition under the configured partitioning; consumer preserves partition order. Source freshness still rejects stale arrivals | | planned |
| KFK-02 | Offset commit timing | all | Successful records commit only after DB commit; failed records advance only after durable dead-letter handoff; never skip earlier incomplete offsets in the partition | | planned |
| KFK-03 | Replay from offset | all | Replay of retained records produces no duplicate business effect; expired or never-received updates require provider reconciliation | | planned |

## PDS registration

| ID | Scenario | Applies to | Expected outcome | Fixtures | Status |
| --- | --- | --- | --- | --- | --- |
| PDS-01 | Successful registration | all | registered; guest check-in unchanged | | planned |
| PDS-02 | Missing data | all | awaitingData; resumes when data arrives | | planned |
| PDS-03 | Rejection, then correction | all | rejected, then corrected per the agreed PDS contract; retries of an unchanged request reuse its idempotency key; correction/amendment identity is specified before implementation | | planned |
| PDS-04 | Known temporary outage | all | retryPending; bounded retry | | planned |
| PDS-05 | Uncertain timeout | all | unknown; reconcile before any resubmission | | planned |
| PDS-06 | Duplicate or delayed result | all | No duplicate registration; completed status never regresses | | planned |
| PDS-07 | Booking changes while registration pending | all | Handled per defined amendment rules (to be specified) | | planned |
