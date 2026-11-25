# Development testing, verification and CI

Run the current backend checks from the repository root with PowerShell 7:

```powershell
./agents/hooks/test-backend.ps1
```

The script restores locked dependencies, builds in Release and runs the existing
MSTest contract suite with scenario evidence. It uses RTK for supported commands
when installed and native dotnet otherwise. SDK selection comes from global.json.
Each invocation creates a fresh directory under ignored test-results/ containing
booking-contracts.trx, scenario-map.json and scenario-results.json. Restore, build,
test and evidence failures return a nonzero exit code. Skipped or missing tests
also fail the evidence check.

For a focused development check:

```powershell
rtk dotnet test --project tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --filter 'FullyQualifiedName~FirstImport'
```

Run the full script after focused checks and before submitting a change. Read
failure diagnostics and the affected source before changing code or expectations.
Preserve contracts and assertions when fixing failures.

## Test layers

| Layer | Scope | Current state | Execution policy |
| --- | --- | --- | --- |
| Unit and contract, tests/Unit/ | Pure rules, wire representations, validation, mappers and application decisions without running services | Executable Booking.ContractTests suite | Focused checks during development; full suite on every PR and push |
| Integration, tests/Integration/ | Real PostgreSQL transactions, atomic booking/outbox changes, concurrent freshness decisions, Kafka and PDS boundaries | Pending implementation of the relevant adapters | Add alongside each adapter; run on every PR and push once available |
| Backend E2E, tests/EndToEnd/ | Running API and workers with PostgreSQL, Kafka and the PDS simulator, driven through external interfaces | Pending hosts, adapters and simulator | Add alongside runtime features; require before merge once available |
| Frontend unit/component, alongside src/Booking.Monitoring/ code | UI logic, rendering, forms and user interactions | Pending UI implementation | Add with the UI using Vitest and React Testing Library |
| Browser E2E, src/Booking.Monitoring/e2e/ | Important user flows through the monitoring UI | Pending UI implementation | Add with the UI using Playwright |
| Eval: static harness checks, eval/check_index.py | Skill routing and registered skill/runbook paths remain consistent | Executable check with a dedicated CI workflow | Run after index, skill or eval changes; CI uses those path filters |
| Eval: agent/skill behavior, eval/cases/ and registered runbooks | The agent selects relevant skills, respects task scope and produces the expected answer or artifact | Recorded manual cases; no automated live judge runner configured | Run relevant cases in disposable workspaces after changing the guidance or skill behavior they exercise |
| Eval: tooling smoke checks, eval/Workflow_smoke_2026-11-24.md | Tool availability, workflow/template resolution and graph-query behavior | Historical manual smoke-check record | Repeat affected checks after tool or workflow changes; report unavailable capabilities |

Unit decisions cannot prove database atomicity, authorization through running HTTP
endpoints, Kafka offset order or recovery after process failure. Stable scenario
IDs from e2e/fixtures/scenarios.md must map to executable tests and actual results
at the layer that proves each requirement. Contract passes leave runtime proof
pending. Until feature 005 exists, include the full catalog/path coverage matrix
and pending reasons in the verifier handoff per WORKFLOW.md section 12.

## Evals and their evidence

Application tests exercise booking software. Behavioral evals exercise the agents
and skills used to develop and review it. Static harness checks validate their
inventory and references. Tooling smoke checks verify individual tool operations.
Each result supports only the behavior or operation actually exercised.

Use [eval/README.md](../eval/README.md) for the procedure and
[eval/targets.json](../eval/targets.json) for registered targets. The historical
[original-target results](../eval/Original_targets_2026-11-24.md) and
[skill-review results](../eval/Skill_review_2026-11-24.md) record behavioral cases
and their limitations. A target marked pass is evidence for its recorded case
and source baseline; it does not prove every runbook case, current agent behavior
or a live Docker integration. Imported runbooks alone are not executed results.

For a behavioral eval, use a fresh session and a disposable synthetic workspace.
Record the source commit, target, prompt, harness/model when known, transcript or
file-read trace, actual verification checks and pass/fail/blocked outcome. Keep
unexecuted cases pending and retain live verification limits separately from the
behavioral grade. Generated traces belong under ignored eval/results/; never
include credentials or real guest data. No external-model execution or judge
service is configured by this guide.

The static eval check can run locally:

```powershell
python eval/check_index.py
```

This command does not run behavioral cases, application tests or live integration
checks. The backend test script also does not run behavioral evals.

## Verification across development

| Stage/check | Evidence | Current support |
| --- | --- | --- |
| Specification and plan review | Contracts, acceptance scenarios, dependency boundaries and diagrams checked against project rules | Review gates in WORKFLOW.md; separate reviewer session required |
| Task consistency analysis | In-scope scenario IDs mapped to implementation tasks and executable tests | Analyze stage in WORKFLOW.md |
| Restore and build | Pinned SDK, locked dependency restore and successful Release compilation | Shared backend script and backend CI |
| Static code checks | Configured analyzers, formatting, lint and architecture rules | Backend build diagnostics available; no separate comprehensive static-analysis gate configured; frontend ESLint/Prettier pending |
| Application tests | Assertions and actual results at the appropriate test layer | Unit/contract suite available; runtime and UI suites pending |
| Development-tool evals | Static inventory results, scoped behavioral grades and tooling smoke results | Static CI available; behavioral and smoke checks manual |
| Code review | Findings on correctness, data boundaries, durability and maintainability | Separate reviewer session required by WORKFLOW.md |
| Final feature verification | Full in-scope results, scenario/path coverage matrix, pending reasons and diagram/code consistency | Verifier handoff required; automated coverage visualization pending feature 005 |
| Graph maintenance | Refreshed navigation graph and reported parser/coverage limitations | Explicit refresh hook after source changes; graph relationships are not correctness evidence |
| Merge decision | Review findings resolved and required test/verification evidence assessed | Owner gate; branch protection must be configured separately |

Use the smallest relevant test or eval during iteration, then complete the
feature's required broader checks. A passing build, static inventory check or
behavioral eval cannot substitute for missing application runtime evidence.

## Current CI

.github/workflows/backend-tests.yml runs the same script on Windows and Linux for
every pull request and push, and supports manual execution. Both jobs have bounded
execution time and publish only the named test reports, including available
reports after a failure. A build failure may leave no reports. Generated reports
remain outside Git; artifacts expire after seven days. Fixtures and failure
diagnostics must remain synthetic and must not disclose guest identity fields or
credentials.

The workflow runs the current unit/contract suite. Integration and backend E2E
coverage remain pending; no placeholder jobs report them as passed. The workflow
is ready for repository CI, but its hosted execution must be verified after push.
The Owner must configure branch protection to require both unit/contract jobs if
merge enforcement is desired; a workflow file alone does not block merging.

## Adding runtime coverage

Feature 002 must define isolated local and CI stack setup, readiness checks,
runtime secret supply and teardown. Each run owns its service names, volumes and
ports; cleanup targets only those resources and must preserve diagnostic evidence
on failure. Do not prune Docker resources. Local secrets remain under secrets/;
CI credentials must be supplied at runtime without appearing in tracked files or
artifacts.

Add PostgreSQL integration tests with persistence and intake implementation,
Kafka integration and backend E2E tests with feature 003, and simulator-backed
registration scenarios with feature 004. Each feature plan selects and pins any
new test dependencies, container images and runner settings before use.

The runtime suites must exercise concurrent arrivals, timeout retries, a crash
after database commit before offset commit, redelivery without duplicate booking
or outbox effects, bounded retries, durable dead-letter handoff and replay.
PDS scenarios must verify stable request correlation and reconciliation of
uncertain outcomes. Use bounded waits for observable state instead of fixed
sleeps. Assert persisted state and broker effects as well as HTTP responses.

CI must publish available safe reports even when runtime tests fail, fail on
missing in-scope evidence and tear down its isolated resources. Keep pending
scenarios visible until their tests have run and passed.
