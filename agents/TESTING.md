# Backend testing and CI

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
| Browser E2E, src/Booking.Monitoring/e2e/ | Important user flows through the monitoring UI | Pending UI implementation | Add with the UI using Playwright |

Unit decisions cannot prove database atomicity, authorization through running HTTP
endpoints, Kafka offset order or recovery after process failure. Stable scenario
IDs from e2e/fixtures/scenarios.md must map to executable tests and actual results
at the layer that proves each requirement. Contract passes leave runtime proof
pending. Until feature 005 exists, include the full catalog/path coverage matrix
and pending reasons in the verifier handoff per WORKFLOW.md section 12.

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
