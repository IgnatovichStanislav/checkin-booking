# Checkin Booking

A system that receives bookings from property management systems (PMS), registers stays and guests with a Police Department System (PDS), and tracks registration status.

Hotel/PMS -> normalize booking -> broker -> save booking -> fictional PDS registration -> status returned to the hotel and monitoring UI.

Planned stack: .NET, PostgreSQL, Redis, webhooks and a React + TypeScript monitoring/test UI. All hotels, guest data and PDS integrations are synthetic. Booking contracts, domain decisions, Salou mapping and their unit/contract tests are implemented. Runtime hosts, PostgreSQL, Kafka and the PDS simulator remain planned.

The confirmed frontend stack is listed in [AGENTS.md](AGENTS.md#confirmed-frontend-stack).

## Workspace

- [AGENTS.md](AGENTS.md): scope, business rules and coding guidance.
- [agents/README.md](agents/README.md): shared Graphify, Spec Kit and Docker skill routing.
- [agents/WORKFLOW.md](agents/WORKFLOW.md): AI workflow, review gates and handoffs.
- [agents/LAYOUT.md](agents/LAYOUT.md): planned folder and project boundaries; create folders as features need them.
- [agents/TESTING.md](agents/TESTING.md): local test command, test layers, current CI and pending runtime coverage.
- [tests/Unit/Booking.ContractTests/](tests/Unit/Booking.ContractTests/): executable unit/contract tests and scenario-evidence runner.
- [e2e/fixtures/scenarios.md](e2e/fixtures/scenarios.md): stable scenario IDs and expected outcomes; runtime coverage remains pending.
- [.specify/](.specify/): Spec Kit templates and workflows; the constitution at .specify/memory/constitution.md (v1.0.0) was ratified by the Owner on 2026-11-24 and governs specs, plans, tasks and implementation.
- [diagrams/](diagrams/): editable draw.io diagrams.
- [eval/README.md](eval/README.md): manual skill evaluation workflow and recorded tooling smoke checks; behavioral evaluations remain not run.

Run `./agents/hooks/test-backend.ps1` in PowerShell 7 to restore, build and test the current backend. GitHub Actions runs the same checks on Windows and Linux for pull requests and pushes. Continue with isolated local infrastructure and the runtime end-to-end flow; project folders are created only as needed.

Graphify output is generated locally and excluded from Git. With Graphify installed, run `./agents/hooks/refresh-graph.ps1` from the repository root. RTK is optional host tooling for compact command output.

Docker skill provenance and license are retained in [agents/sources/docker-skills/](agents/sources/docker-skills/).
