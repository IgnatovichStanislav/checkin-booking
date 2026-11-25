# Feature 006 scenario coverage

Coverage spans **30 scenario IDs and 90 scenario/path cells** from the unchanged [scenario catalog](../../e2e/fixtures/scenarios.md). Runtime proof is pending for 70 applicable cells and inapplicable for 20 cells. No cell has a runtime pass or failure. Passing contract methods support 28 applicable cells.

The source is the [Windows and Linux backend run](https://github.com/IgnatovichStanislav/checkin-booking/actions/runs/45601115572) at `de6e85b`. Both jobs passed the same 17 contract methods and 19 harness checks. The matrix uses the compiled scenario map joined to the observed results. It follows the [coverage data model](data-model.md#coverage-and-eval-records).

Method IDs in the table refer to methods on `Booking.ContractTests.ContractCases` in the `Booking.ContractTests` assembly. Only business results with a catalog ID contribute. A contract pass records a passed method and remains distinct from the runtime verdict. A pending cell without a method has no mapped contract support.

| Scenario ID | Required behavior | H01 webhook | SAL POST | CAS poll |
| --- | --- | --- | --- | --- |
| INT-01 | First snapshot | Pending I | Pending I. Contract pass `FirstImport`, `SafeReferences` | Pending I |
| INT-02 | Duplicate snapshot (equal updatedAt, identical normalized data) | Pending I | Pending I. Contract pass `DuplicateSnapshot` | Pending I |
| INT-03 | Stale snapshot (older updatedAt) | Pending I | Pending I. Contract pass `StaleUpdate` | Pending I |
| INT-04 | Conflicting snapshot (equal updatedAt, different data) | Pending I | Pending I. Contract pass `ConflictingSnapshot` | Pending I |
| INT-05 | Invalid snapshot | Pending I | Pending I. Contract pass `InvalidSnapshot`, `WireRoundTrip` | Pending I |
| INT-06 | Concurrent delivery of the same booking | Pending I | Pending I | Pending I |
| H01-01 | Same booking ID in h01-central and h01-east | Pending H | Inapplicable (H01 only) | Inapplicable (H01 only) |
| H01-02 | Property not authorized for the integration | Pending H | Inapplicable (H01 only) | Inapplicable (H01 only) |
| H01-03 | Webhook retry of an accepted event | Pending H | Inapplicable (H01 only) | Inapplicable (H01 only) |
| H01-04 | Missing event | Pending H | Inapplicable (H01 only) | Inapplicable (H01 only) |
| SAL-01 | POST retried after client timeout | Inapplicable (SAL only) | Pending S. Contract pass `DuplicateSnapshot`, `FirstImport` | Inapplicable (SAL only) |
| SAL-02 | Unauthorized property | Inapplicable (SAL only) | Pending S. Contract pass `SafeReferences`, `ScopedResources` | Inapplicable (SAL only) |
| CAS-01 | Multi-page poll | Inapplicable (CAS only) | Inapplicable (CAS only) | Pending C |
| CAS-02 | Overlapping timestamp boundaries between polls | Inapplicable (CAS only) | Inapplicable (CAS only) | Pending C |
| CAS-03 | Poll interrupted mid-pagination | Inapplicable (CAS only) | Inapplicable (CAS only) | Pending C |
| CAS-04 | Restart with durable cursor | Inapplicable (CAS only) | Inapplicable (CAS only) | Pending C |
| BRK-01 | Crash after DB commit, before ack | Pending B | Pending B | Pending B |
| BRK-02 | Redelivery of an applied message | Pending B | Pending B | Pending B |
| BRK-03 | Repeated processing failure | Pending B | Pending B | Pending B |
| BRK-04 | Dead-letter handoff | Pending B | Pending B | Pending B |
| KFK-01 | Keyed ordering | Pending K | Pending K | Pending K |
| KFK-02 | Offset commit timing | Pending K | Pending K | Pending K |
| KFK-03 | Replay from offset | Pending K | Pending K | Pending K |
| PDS-01 | Successful registration | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` |
| PDS-02 | Missing data | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` |
| PDS-03 | Rejection, then correction | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` |
| PDS-04 | Known temporary outage | Pending P. Contract pass `RetryAndReconcile` | Pending P. Contract pass `RetryAndReconcile` | Pending P. Contract pass `RetryAndReconcile` |
| PDS-05 | Uncertain timeout | Pending P. Contract pass `RetryAndReconcile` | Pending P. Contract pass `RetryAndReconcile` | Pending P. Contract pass `RetryAndReconcile` |
| PDS-06 | Duplicate or delayed result | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` | Pending P. Contract pass `OperationOutcomes` |
| PDS-07 | Booking changes while registration pending | Pending P. Contract pass `RegistrationChange` | Pending P. Contract pass `RegistrationChange` | Pending P. Contract pass `RegistrationChange` |

## Pending runtime proof

- **I** — Await the durable PostgreSQL intake service, case-specific transaction tests and the selected path adapter. The shared local stand belongs to feature 002.
- **H** — Await the H01 webhook and reconciliation implementation with synthetic PMS evidence.
- **S** — Await the Salou REST intake host, authorization and durable retry behavior.
- **C** — Await the Cassis polling/cursor implementation and synthetic PMS evidence.
- **B** — Await the broker consumer and durable failure/retry tests with real PostgreSQL and the selected broker. Kafka is feature 003.
- **K** — Await the Kafka adapter and broker-level ordering, offset or replay proof in feature 003.
- **P** — Await the PDS simulator and durable registration workflows in feature 004.

For BRK, KFK and PDS, the catalog applies to all three paths. Shared PDS decision tests are listed under each applicable path and count once per method in the suite total. Their results exercise decisions in-process. Running services and durable effects require the separate proof in the pending reasons.
