---
name: kafka-idempotency
description: Review Checkin Booking Kafka consumers, PostgreSQL transactions and replay for duplicate effects or premature offset commits.
---

# Kafka durability review

Trace one record from its partition and offset through the database transaction to the broker commit. Check the handler, schema constraints and consumer configuration together.

1. Identify the key for the specific business effect. A booking can change many times under the same Kafka key. Check the source timestamp and normalized snapshot under the database concurrency guard. Equal-time differences require a durable conflict record.
2. When applying a snapshot, commit the booking and its outbox row together. If the design uses a processed-effect marker, write it in that transaction. Prefer a conflict target scoped to the exact marker constraint. If handling a uniqueness exception, roll back the failed transaction and verify the expected constraint, effect key and prior committed outcome before classifying a redelivery. Preserve conflicting payloads for reconciliation.
3. An unrelated unique violation on the booking or outbox is a processing failure. Roll back, leave the record unacknowledged and follow bounded retry. Advance past failed work only after confirmed durable dead-letter handoff. Keep guest identity out of broker payloads and diagnostics.
4. Verify the configured broker commit happens after the database commit. In the .NET client, `StoreOffset` records local progress. Identify the subsequent automatic or explicit broker commit. A later success must never advance the partition past an earlier unfinished record.

## Evidence to request

| Exercise | Expected observation |
| --- | --- |
| Crash after DB commit and redeliver the record (BRK-01, KFK-02) | One booking change and one outbox effect. Processing resumes and the offset can advance. |
| Force a different unique constraint to fail on a new record (BRK-03, BRK-04, KFK-02) | No partial booking or outbox write. The consumer reports failure and does not advance past the record until recovery or durable dead-letter handoff. |
| Deliver competing snapshots (INT-04, INT-06) | No regression. Equal timestamps with different normalized data produce a conflict. |
| Replay retained offsets (KFK-03) | No repeated business effect, including after any marker cleanup permitted by the design. |

Report the relevant transaction and offset evidence for each scenario tested. Mark unexecuted cases as pending.
