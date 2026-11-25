# Implementation Plan: Layered testing and development verification

**Branch**: `codex/layered-testing-verification` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Status**: Draft for independent review and Owner G2 approval. G1 approved in commit be70aed.

## Summary

Reuse the existing backend suite, MSTest evidence joiner, PowerShell runner and Windows/Linux CI. Add source-bound phase reporting, an independent required-test inventory and scoped negative checks. Strengthen two sets of existing intake assertions. Register two manual worktree behavioral cases, validate inventory failures separately and keep all runtime proof pending.

The public CI artifact will contain only allowlisted metadata/results. Raw TRX diagnostics, assertion messages, console output and attachments will not be uploaded. This prevents a failing assertion from exposing a restricted synthetic input. No booking behavior, service, port, database, UI or dependency is added.

## Technical Context

| Concern | Decision |
| --- | --- |
| Languages/toolchains | Existing C# 14 / net10.0; SDK 10.0.404 from global.json, rollForward disabled. PowerShell 7 for orchestration; existing Python 3 standard-library checker. |
| Test platform | Existing MSTest.Sdk 4.5.3 and native Microsoft.Testing.Platform mode. Preserve package lock and existing framework. |
| Optional tools | Use RTK for supported local dotnet/git commands; native fallback must work. CI does not install RTK. Git is required to bind evidence to the source. |
| Storage | Per-invocation ignored test-results/ directory; ignored eval/results/ for behavioral traces. No application persistence or runtime secret required. |
| Platforms | Existing ubuntu-latest / windows-latest jobs, PowerShell shell, 15-minute job bound. Runner-image drift is documented; record actual OS/tool versions in evidence. |
| Dependencies | No new package or service. Existing action references retained; no new judge/model infrastructure. |
| Testing | Existing business/evidence tests; new runner checks in PowerShell; Python stdlib inventory regression checks; manual fresh-session evals. |
| Performance | No new latency/throughput target. Fail early at prerequisites; retain bounded CI and do not rerun successful checks without a reason. |
| Scope | V01–V08, 13 requirements; current booking test assertions and tool behavior only. |

## Constitution Check

Pre-research and post-design checks have the same outcome: no exception requested.

| Principle | Design response |
| --- | --- |
| I: restricted identity | No payloads in exported evidence. Reports/receipt fields are allowlisted. Test failure assertions use safe fixed messages/codes; no identity diagnostics or credentials. |
| II: durability | No runtime claim or change. Booking/outbox atomicity, offset ordering and crash recovery remain pending their service features. |
| III: freshness | Existing timestamp rules unchanged. Added assertions inspect already specified decisions, not source ordering alternatives. |
| IV: broker semantics | No broker abstraction or Kafka dependency added. |
| V: scenario coverage | Preserve catalog IDs, paths and evidence levels. Full matrix in verifier packet; missing/inapplicable cells carry reasons. Tool V-cases are separate acceptance IDs. |
| VI: small changes/secrets | Reuse pinned dependencies, disposable fixtures, no services or secrets. No global hooks or imported skill edits. |

## Project Structure

### Documentation

- This plan, [research.md](research.md), [data-model.md](data-model.md), [contracts/verification.md](contracts/verification.md), [quickstart.md](quickstart.md).
- Existing approved flow plus proposed [sequence](../../diagrams/006-layered-testing-verification/sequence.drawio) and [evidence boundaries](../../diagrams/006-layered-testing-verification/data.drawio).
- tasks.md is deliberately not created at the planning stage.

### Intended implementation changes

| Location | Change |
| --- | --- |
| agents/hooks/test-backend.ps1 | Fresh run root, source/toolchain/phase metadata, explicit failed phase and safe console summary; preserve native exits. |
| agents/hooks/check-backend-runner.ps1 | Standalone dependency-free checks of orchestration using disposable child-process command doubles; actual suite separately proves the real command. |
| tests/Unit/Booking.ContractTests/required-tests.json | Reviewed required method identities independent of reflection; no duplicated scenario metadata. |
| tests/Unit/Booking.ContractTests/ScenarioEvidence.cs, EvidenceEntryPoint.cs, EvidenceCases.cs, project file | Validate required inventory, write safe execution receipt, retain join protections and add missing negative assertions; copy inventory to output. |
| tests/Unit/Booking.ContractTests/IntakeCases.cs | Add opaque booking-event linkage and exact reconciliation-intent assertions using existing fixtures. |
| eval/test_check_index.py | Independently exercise valid inventory and each implemented invalid-inventory condition using stdlib temporary fixtures. |
| eval/cases/worktree-new-task.md, worktree-follow-up.md; eval/targets.json | Two independent manual behavioral eval cases and target registration. Use the existing speckit-specify skill reference as a relevant routed workflow target; never change imported skills. |
| .github/workflows/backend-tests.yml | Run runner self-check plus real suite; publish only exact current-run safe exports and job summaries, including phase failure metadata. |
| .github/workflows/skill-index.yml | Run inventory regression checks as well as the real checker; include agents/WORKFLOW.md in affected-guidance triggers. |
| agents/TESTING.md, eval/README.md | Commands, layer policies, safe diagnostics, behavioral setup/grading and limitations. |

## Test-gap inventory

This is a source inspection, not a fresh passing test run. The existing suite has 17 contract/business methods and seven evidence methods; these counts describe the baseline, not a permanent expected count.

| Evidence | Reuse | Scoped gap |
| --- | --- | --- |
| IntakeCases.cs FirstImport/StaleUpdate | Existing create/apply fixtures and event reference assertions | Assert event BookingId equals the accepted BookingId on create and newer update. |
| IntakeCases.cs ConflictingSnapshot | Existing conflict/erased fixtures and non-null intent checks | Assert exact scope, BookingId, source UpdatedAt, Open and SnapshotConflict/ComparisonUnavailable reason. No new business rule. |
| ScenarioEvidence.CompiledMap/Index/Join | Nonempty map, unique metadata, TRX timestamps/counters/identities, missing/skipped outcomes | Reflection alone cannot detect a removed required method. Add reviewed identity inventory and tests for deletion/empty/duplicate inventories. |
| EvidenceCases.cs | Existing duplicate/malformed/stale/missing result, failed-exit and incomplete-run cases | Add safe receipt tests and failure after platform execution but before evidence completion; assert no misleading pass/export. |
| test-backend.ps1 | Locked restore, Release build, RTK fallback, fresh GUID path and failure exit | No durable source/phase record or repeatable runner regression script. |
| eval/check_index.py | Real inventory validation implements five negative conditions | Historical combined fixture is not a maintained per-condition regression suite. |
| eval/cases | Existing routing/Kafka cases and trace protocol | No new-task or follow-up worktree case. |

## Design and execution flow

1. The runner creates a GUID run root under test-results/ and writes run.json before invoking dependency/build/test commands. It captures commit, dirty flag and a digest of the relevant current working files; no diff content or secret paths are persisted.
2. Preflight checks required tools, pinned SDK selection and valid source context. Restore/build phases use current locked commands. Preserve failed native exit codes; preflight/evidence validation failures return 1.
3. Invoke the existing executable evidence entry point in a fresh child suite/ directory. It validates an independently reviewed required identity set against compiled methods before executing tests. Added/removed/renamed methods require an intentional inventory update; scenario tags remain solely in compiled metadata.
4. A new execution.json receipt separates platform test exit/outcome from evidence validation outcome using fixed codes, never exception messages. Write/update it on reachable failure paths. A crash before receipt creation remains explicit: the parent records the process exit and missing receipt; it does not infer a test pass.
5. The joiner retains fresh-run timestamp, identity, fixture-reference and counter checks. A failed/skipped/missing result cannot pass the full entry point. Map/receipt corruption fails evidence. Test process output is not streamed into public CI logs; emit safe method IDs, counts and phase codes. Do not persist raw test stdout/stderr.
6. After validated receipt/results, build safe exports containing allowlisted fields only. Treat TRX, ErrorInfo, stack traces, attachments and arbitrary stdout as non-exportable. Existing tests and new fault fixtures must avoid guest identity assertion messages. Available local TRX remains an intermediate for the joiner, never a CI artifact.
7. The parent finalizes run.json in success/failure handling and exposes the current run's export location to CI. Upload metadata even when tests never started. Do not upload historical wildcard run directories. Cancelled/hard-terminated runs remain incomplete, with no fabricated final result.
8. Inventory checks remain separate from backend tests. Behavioral evals remain manual, isolated and freshly graded. No model execution is added to CI.

Metadata and export shapes are defined in the [verification contract](contracts/verification.md). Their introduction does not change booking APIs or events.

## Validation strategy and acceptance map

| Cases | Required evidence |
| --- | --- |
| V01 | Clean checkout real suite on Windows/Linux; optional RTK absent; copied project fixtures and correct pinned SDK. |
| V02 | Child-process doubles independently fail restore, build, platform test and evidence; assert phase/exit, not just console text. Missing prerequisite path tested without altering host installations. |
| V03 | Existing join tests plus missing/deleted required method, empty/duplicate inventory, receipt/report corruption, stale source/run mismatch, zero selection and incomplete execution. |
| V04 | Actual hosted passing jobs and disposable controlled failing hosted jobs; record URLs/source SHAs, outcome and safe artifact contents. No permanent failing change or workflow bypass. |
| V05 | Two independent run roots; safe export checks using a synthetic sentinel in failure details; cancellation remains incomplete. Exact artifact allowlist checked. |
| V06 | Valid real inventory plus each negative fixture independently; no imported assets edited. |
| V07 | Two new fresh-session manual cases in disposable Git repositories, independent prompts/traces/grades, dirty-file hash/status checks and command ordering. |
| V08 | Full catalog/path matrix in verifier handoff with runtime proof pending and supporting contract results attached separately; guide/review gate checks. |

Runner tests do not require Pester: launch child PowerShell sessions with fixed test-owned command doubles, capture only safe exit/phase data and inspect fresh output. Keep them independent of the production function bodies. Actual Windows/Linux hosted execution is still required; mocks cannot complete V04.

Inventory regression checks use unittest, tempfile and the current checker in child processes. Cases separately cover unrouted skill, routed missing skill/SKILL.md, duplicate target ID, missing skill path and missing runbook path.

Behavioral evals load the project rules and approved worktree workflow in writing-capable disposable repositories. They withhold grading criteria, preserve unrelated dirty sentinel bytes and verify first substantive command/edit occurs inside the selected isolated worktree. A routing/static pass is not a substitute. Do not publish model/session attribution in Git history; keep required private run metadata in ignored traces.

## Scenario/path coverage policy

Use the unchanged 30-ID catalog and three intake paths: baseline 90 cells, 70 applicable and 20 inapplicable. Compute/review applicability from catalog text, not those historical counts. BRK/KFK/PDS apply to every path; existing PDS decision evidence tagged all is not adapter execution.

The G3 handoff contains the complete Markdown matrix under the existing workflow process. Supporting contract results and runtime verdicts are separate. No automated visualization/dashboard is built here. Missing cells or reasons block verification; pending runtime cells remain expected because runtime implementation is outside this feature, and are not described as completed scenarios.

## Delivery order

1. Required identity inventory, evidence receipt and focused negative tests; strengthen the identified intake assertions.
2. Runner phase/source metadata and disposable regression checks; safe export and privacy checks.
3. Static eval regression suite plus manual worktree cases/registration/procedure.
4. CI wiring and safe artifact paths; actual hosted pass/failure evidence.
5. Full local/CI/eval verification and scenario/path review packet.

Tasks and implementation begin only after independent plan review and G2 approval. No application implementation or test execution was performed to create this plan.

## Risks and completion limits

- Hosted CI access may block V04: report the blocker and keep completion open.
- Disposable writing-capable agent sessions may be unavailable: V07 stays unverified.
- Inventory is a reviewed expectation, not a generated mirror. Do not automatically rewrite it when tests disappear.
- A cancelled runner cannot reliably finalize output; incomplete evidence never passes.
- Unexpected sensitive assertion output must never be exported; fixed safe assertions and explicit exports are mandatory.
- No claim of token savings is made; measuring that benefit is separate.

## Complexity Tracking

No constitution violation or additional dependency is proposed. One required-identity file, one execution receipt and one parent manifest address distinct failure boundaries; existing result/fixture metadata remains authoritative.

