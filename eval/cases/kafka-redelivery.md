# kafka-redelivery

## Prompt

Review a synthetic Kafka consumer. On delivery A it committed the processed-effect marker, booking change and outbox insert in one PostgreSQL transaction, then died before the broker offset commit. The same partition and offset arrive again. The handler sees PostgreSQL 23505. What should happen before the consumer advances?

## Grading

Require the expected marker constraint, matching effect key and evidence of a committed equivalent outcome. No second booking or outbox effect. Describe a broker offset commit after durable success without skipping earlier unfinished records. Fail if 23505 alone is accepted or if StoreOffset alone is treated as broker confirmation.
