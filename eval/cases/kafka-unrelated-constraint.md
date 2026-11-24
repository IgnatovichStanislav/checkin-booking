# kafka-unrelated-constraint

## Prompt

Review a synthetic Kafka consumer. For a new effect, SaveChanges fails with PostgreSQL 23505 on uq_outbox_sequence. The marker constraint is uq_processed_effect. The catch block treats every 23505 as a duplicate and commits the offset. Evaluate the catch block and describe recovery.

## Grading

Require rollback of the entire failed transaction and classification as processing failure. Require bounded retry with the offset held, then durable dead-letter handoff before advancing on terminal failure. Fail if any uniqueness error is accepted as proof of prior processing or a later offset skips the failed record.
