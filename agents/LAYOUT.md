# Folder and project conventions

These are intended boundaries for future features, not existing projects or approved
feature contracts. Create only the directories and projects needed by the current
approved task. Final project names and dependencies are confirmed in that feature's
plan; do not scaffold this entire layout in advance.

## Application boundaries

| Planned location | Responsibility | Dependency boundary |
| --- | --- | --- |
| `src/Booking.Contracts/` | Normalized BookingDto and shared application contracts | No Kafka client types, offsets or transport envelopes in shared contracts |
| `src/Booking.Domain/` | Booking identity, freshness decisions and booking/PDS state rules | No broker, database, HTTP or UI dependencies |
| `src/Booking.Application/` | Provider mappers, booking and PDS use cases, outbox orchestration and reconciliation | Uses contracts and domain rules; no Kafka client dependencies |
| `src/Booking.Persistence/` | PostgreSQL transactions, atomic freshness guard, outbox storage, restricted registration data and durable cursors | Implements application storage needs; no Kafka offset ownership |
| `src/Booking.Kafka/` | Kafka producer/consumer integration, key selection, partition ordering, offsets, retries, dead letters and replay | Uses shared application logic; application/domain/contracts do not reference this project |
| `src/Booking.Api/` | Authenticated intake and safe status endpoints; host composition | Delegates mapping and business decisions to shared logic; wires concrete adapters |
| `src/Pds.Simulator/` | Fictional PDS endpoints, synthetic responses and failure controls | Separate runnable boundary; production-side PDS rules remain in shared application/domain logic |
| `src/Booking.Monitoring/` | Optional Blazor monitoring/test UI | Uses safe status contracts; no direct restricted identity or Kafka access |

Dependency direction: contracts/domain <- application <- concrete adapters and hosts.
Persistence and Kafka coordinate at the consumer boundary: finish the database
transaction before committing the corresponding Kafka offset. Keep Kafka operations
explicit. Application interfaces for storage or PDS operations may be introduced when
needed; do not add a generic queue interface or broker-selection framework.

Provider payload contracts and Kafka envelopes belong with their respective integration;
map to the shared normalized contract at the boundary. Keep polling cursors outside the
mapper. A broker fork replaces Booking.Kafka and updates host composition, local
infrastructure and broker-specific tests; it preserves booking rules, mappers, freshness,
outbox and PDS logic, shared fixtures and scenario IDs. No other broker project belongs
in this repository.

## Tests and local infrastructure

| Planned location | Purpose |
| --- | --- |
| `tests/Unit/` | Pure rules, mappers and application behavior without running services |
| `tests/Integration/` | PostgreSQL, Kafka and PDS boundary tests, including failure and recovery behavior |
| `tests/EndToEnd/` | Executable full-path scenarios mapped to the stable scenario catalog |
| `e2e/fixtures/` | Shared synthetic inputs, expected outcomes and stable scenario IDs; payloads follow agreed contracts |
| `infra/local/` | Future Compose configuration and setup/teardown scripts; ports, project/volume names and cleanup behavior agreed in feature 002 |
| `secrets/` | Git-ignored runtime-only local secrets; never fixtures or build inputs |
| `test-results/` | Git-ignored test output, scenario mappings, coverage matrices and failure timelines |

Keep executable tests under tests/ and shared data under e2e/fixtures/. Choose the test
framework and results adapter in the plan. Infrastructure must use isolated service,
volume and port settings per worktree. No services or volumes are created by this document.

## Guidance and design records

- `AGENTS.md`: root operational rules and scope.
- `agents/`: shared skills, workflow, layout and explicitly invoked hooks.
- `.specify/`: managed Spec Kit tooling plus the ratified project constitution; do not edit managed templates to customize a feature.
- `specs/NNN-name/`: feature specs, plans, contracts and task records, created at their stages.
- `diagrams/NNN-name/`: editable feature draw.io diagrams; shared diagrams stay in diagrams/.
- `eval/`: harness evaluation inventory, procedures and reviewed smoke-check records; generated evaluation artifacts go to ignored eval/results/.
- `graphify-out/`: generated, ignored navigation graph; refresh after source changes, not documentation-only edits.

No project .codex/ or .claude/ configuration and no empty placeholder application trees.
