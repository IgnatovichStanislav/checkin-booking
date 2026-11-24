# Skill review, 2026-11-24

The baseline was working tree HEAD `80c7f7f` with the uncommitted team skills and routing changes. Each case ran once with Codex CLI 0.171.0-alpha.13.2 in an ephemeral read-only session using the same disposable copy of AGENTS.md, agents/README.md and agents/skills. The CLI used its configured model. The JSONL traces did not identify a model version. Each agent saw only the prompt. Grading used the completed answer and, for routing, the recorded shell commands.

| Case | Grade | Observed behavior |
| --- | --- | --- |
| route-ef-core | Pass | Read the index and opened optimizing-ef-core-queries directly. No generic C# router or bulk skill read. Identified N+1 queries and projected the count before materialization. The trace includes a redundant second read of part of the EF skill. |
| kafka-redelivery | Pass | Required the named marker constraint, exact key and equivalent committed effect. Skipped repeat booking and outbox writes and distinguished StoreOffset from a broker commit. Protected earlier unfinished offsets. |
| kafka-unrelated-constraint | Pass | Identified the outbox collision as a processing failure. Required transaction rollback, bounded retries and durable handoff before advancing past failed work. |

All three CLI processes exited successfully. Local transcripts and answers are in the ignored eval/results/skill-review directory. These runs grade agent guidance. Application behavior and token savings require separate measurements.

The static check resolved 52 skill routes and 24 eval targets. A negative fixture produced a nonzero exit for an unregistered skill, a nonexistent route, a duplicate target ID and missing skill/runbook paths. The original 21 behavioral targets were checked for inventory integrity only.
