# Development testing, verification and CI

Run the current backend checks from the repository root with PowerShell 7:

```powershell
./agents/hooks/test-backend.ps1
```

The script restores locked dependencies, builds in Release and runs the existing
MSTest contract suite. SDK selection comes from global.json. It uses RTK for
supported build commands when available and native dotnet otherwise.

Each invocation creates a fresh directory under ignored test-results/. Its run
manifest records preflight, restore, build, attachment policy, test, evidence
and export outcomes. The attachment policy inspects the built assemblies before
loading the test executable. Source and artifact digests bind the approved
policy verdict to the binaries and fixtures used by the child. Test attachments
are unsupported in this full-check mode. The child validates the binding and
records platform exit and evidence status in its execution receipt.

The suite directory holds the safe test-event XML, compiled scenario map and
joined results. The export directory holds the allowlisted JSON records for that
run. The parent discards raw child stdout and stderr in memory. Early failures
can export the manifest alone. A returned test failure retains its actual exit
and qualifying joined evidence. Missing, skipped or stale evidence fails
verification. A standalone evidence run lacks the parent's binding and reports
its source and policy verification as false.

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
python -m unittest discover -s eval -p test_check_index.py
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

.github/workflows/backend-tests.yml runs the disposable runner regressions and
the full suite on Windows and Linux on pull requests and pushes. It also
supports manual execution. The runner emits the exact current export directory
through GITHUB_OUTPUT, and the upload step uses it even after a failed run when
available. Artifacts expire after seven days. Use synthetic fixtures. Keep guest
identity fields and credentials out of diagnostics and reports.

Hosted checks have verified successful, early-failure and returned-test-failure
runs. See the [verification evidence](../specs/006-layered-testing-verification/implementation-evidence.md#hosted-ci-verification-group-d).
Integration and backend E2E coverage remain pending.
The Owner must configure branch protection to require both unit/contract jobs if
merge enforcement is desired; a workflow file alone does not block merging.

## Runtime test entry criteria

Before introducing a runtime suite, its feature owner must record:

| Criterion | Required evidence | Current owner or dependency |
| --- | --- | --- |
| Approved interfaces and infrastructure | Accepted runtime contracts, local dependency versions, ports and secret supply. | Feature 002 for the local stand. The intake persistence/API work and each provider adapter own their runtime contracts. |
| Isolated setup | Unique per-run service names, volumes, ports and test data for local runs and CI. | Feature 002. |
| Bounded readiness | Finite readiness checks with observable dependency and application health before sending requests. | Feature 002 and the service feature that owns each readiness endpoint. |
| Owned teardown | Teardown can identify and remove only resources created for that invocation. Preserve safe evidence after failure. | Feature 002 and the runtime CI owner. |
| Explicit failure controls | A reproducible trigger for retries, timeouts, crashes, redelivery or simulator failures at the relevant boundaries. | Intake/persistence and each provider adapter for intake failures, feature 003 for Kafka, and feature 004 for PDS outcomes. |
| Observable durable outcomes | Assertions inspect the expected durable writes, delivery/offset effects, cursors or registration state. Bound waits on observed state. | Intake/persistence, feature 003 and feature 004. The future UI feature owns monitoring tests. |

The [scenario coverage report](../specs/006-layered-testing-verification/scenario-coverage.md)
tracks pending runtime evidence by scenario and intake path.

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
