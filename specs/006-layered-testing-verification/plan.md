# Implementation Plan: Layered testing and development verification

**Branch**: `codex/layered-testing-verification` | **Date**: 2026-11-25 | **Spec**: [spec.md](spec.md)

**Status**: G2 approved by the Owner on 2026-11-25 after independent review resolved all blocking findings. G1 approved in commit bf47a41 after rebasing onto the Owner's main at a594070. See [plan review](plan-review.md).

## Summary

Reuse the existing backend suite, MSTest evidence joiner, PowerShell runner and Windows/Linux CI. Add source-bound phase reporting, an independent required-test inventory and scoped negative checks. Strengthen two sets of existing intake assertions. Register two manual worktree behavioral cases, validate inventory failures separately and keep all runtime proof pending.

The public CI artifact will contain only allowlisted metadata/results. The supported full check will never generate raw TRX diagnostics or retain assertion messages, console output or attachments. A small consumer of the installed test-platform messages produces safe XML for the existing joiner. No booking behavior, service, port, database, UI or dependency is added.

## Technical Context

| Concern | Decision |
| --- | --- |
| Languages/toolchains | Existing C# 14 / net10.0; SDK 10.0.404 from global.json, rollForward disabled. PowerShell 7 for orchestration; existing Python 3 standard-library checker. |
| Test platform | Existing MSTest.Sdk 4.5.3 and native Microsoft.Testing.Platform 2.5.3 from the lock. Use its installed builder hook/IDataConsumer API for safe result capture. Preserve package lock/framework. |
| Optional tools | Use RTK for supported local dotnet/git commands; native fallback must work. CI does not install RTK. Git is required to bind evidence to the source. |
| Storage | Per-invocation ignored test-results/ directory; ignored eval/results/ for behavioral traces. No application persistence or runtime secret required. |
| Platforms | Existing ubuntu-latest / windows-latest jobs, PowerShell shell, 15-minute job bound. Runner-image drift is documented; record actual OS/tool versions in evidence. |
| Dependencies | No new package or service. Existing action references retained; no new judge/model infrastructure. |
| Testing | Existing business/evidence tests; new runner checks in PowerShell; Python stdlib inventory regression checks; manual fresh-session evals. |
| Performance | No new latency/throughput target. Fail early at prerequisites; retain bounded CI and do not rerun successful checks without a reason. |
| Scope | V01–V08, 14 requirements including the approved attachment policy; current booking test assertions and tool behavior only. |

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
| agents/hooks/check-attachment-policy.ps1 | Inspect owned managed PE metadata without loading target assemblies, before test launch; reject forbidden result-attachment/unsupported execution routes and emit fixed safe policy verdicts. Uses bundled MetadataReader/PEReader, no new package. |
| agents/hooks/check-backend-runner.ps1 | Standalone dependency-free checks of orchestration using disposable child-process command doubles; actual suite separately proves the real command. |
| tests/Unit/Booking.ContractTests/required-tests.json | Reviewed required method identities independent of reflection; no duplicated scenario metadata. |
| tests/Unit/Booking.ContractTests/ScenarioEvidence.cs, EvidenceEntryPoint.cs, SafeTestEvidence.cs, EvidenceCases.cs, project file | Register an installed-platform safe data consumer, validate required inventory, write receipt/minimal safe XML, retain join protections and add negative/privacy assertions; copy inventory to output. |
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
2. Preflight checks required tools, pinned SDK selection and valid source context. Use locked restore and a forced rebuild, then bind source and executed outputs through the attestation policy in data-model.md. Preserve failed native exit codes; preflight/evidence validation failures return 1.

   After build, run the explicit policy phase before starting the test executable. Inspect all owned module/method metadata with the approved attachment-policy guard. Reject AddResultFile references (including helpers/function pointers), unsupported indirect/custom/lifecycle/data/inherited paths, unresolved metadata and user module initializers before load. Bind the passed policy verdict to the exact artifact digest. On rejection, test remains notRun and platform exit unset; export only a safe phase record. See the approved [amendment](attachment-policy-proposal.md). This is a closed-suite guard, not a filesystem sandbox.
3. Invoke the existing executable evidence entry point in a fresh child suite/ directory. It validates an independently reviewed required identity set against compiled methods before executing tests. Added/removed/renamed methods require an intentional inventory update; scenario tags remain solely in compiled metadata.
4. Register SafeTestEvidence through the existing TestingPlatformBuilderHook and IDataConsumer APIs. Consume TestNodeUpdateMessage terminal states. Match TestMethodIdentifierProperty namespace/type/method to the selected compiled test assembly; the pinned adapter may leave AssemblyFullName empty, so never infer identities from display names. Reject absent/ambiguous identifiers and duplicate/unknown terminal results. Ignore progress/discovery messages and never read or serialize failure exceptions/explanations, DisplayName, arbitrary properties, stdout or attachments.
5. Keep the stock TRX reporter disabled in full-check mode. From observed safe terminal identities/states, real invocation times and counts, generate a minimal allowlisted TRX-shaped XML report for the existing joiner. Preserve the actual platform exit independently. A new execution.json receipt separates platform outcome from evidence validation using fixed codes. A crash before receipt creation remains incomplete, not passing. Existing fresh-run identity/fixture/counter protections remain, and a failed/skipped/missing result cannot pass the full entry point.
6. The parent drains/discards raw child stdout/stderr in memory without echoing or writing it; it emits safe IDs/counts/phase codes. No raw TRX, exception messages, attachments or arbitrary output are created as retained evidence. Use explicit field allowlists for exports and test the privacy sentinel against every retained file and local/hosted log summary, including interruption/no-report cases. Safe generated XML may remain local for the joiner but is not uploaded.
7. The parent finalizes run.json in success/failure handling and exposes the current run's export location to CI. Preflight/restore/build failures stop later execution but still attempt safe manifest export. A returned failed test still permits evidence joining and safe failed-result export. Evidence rejection permits manifest/receipt export only. Export failure fails the overall run without changing prior results. Upload safe metadata even when tests never started; never upload historical wildcard run directories. Cancelled/hard-terminated runs remain incomplete.
8. Inventory checks remain separate from backend tests. Behavioral evals remain manual, isolated and freshly graded. No model execution is added to CI.

Metadata and export shapes are defined in the [verification contract](contracts/verification.md). Their introduction does not change booking APIs or events.

## Validation strategy and acceptance map

| Cases | Required evidence |
| --- | --- |
| V01 | Clean checkout real suite on Windows/Linux; optional RTK absent; copied project fixtures and correct pinned SDK. |
| V02 | Child-process doubles independently fail restore, build, platform test and evidence; assert phase/exit, not just console text. Verify early failure still exports a safe manifest, failed tests still join/export valid failures, and successful tests with invalid evidence still fail. Missing prerequisites tested without altering host installations. |
| V03 | Existing join tests plus missing/deleted required method, empty/duplicate inventory, receipt/report corruption, stale source/run mismatch, zero selection and incomplete execution. Include stale built binary after a source/fixture change, tracked deletion, an allowed untracked source input and independent mutation of an archived contract JSON copied by the project. |
| V04 | Actual hosted passing jobs and disposable controlled early-failure/test-failure jobs; record URLs/source SHAs, outcomes and safe artifact contents. No permanent failing change or workflow bypass. |
| V05 | Two independent run roots; reject disposable compiled direct/helper/unsupported attachment canaries before test-body execution, with no platform exit or staged file. Keep canary code outside normal scanned modules. Separately inject assertion/stdout/metadata sentinels in real returned failures and check retained files/summaries/hosted logs/artifacts, including interruption. Check actual platform exit, incomplete cancellation and exact artifact allowlist. |
| V06 | Valid real inventory plus each negative fixture independently; no imported assets edited. |
| V07 | Two new fresh-session manual cases in disposable Git repositories, independent prompts/traces/grades, dirty-file hash/status checks and command ordering. |
| V08 | Full catalog/path matrix in verifier handoff with runtime proof pending and supporting contract results attached separately; guide/review gate checks and explicit FR-012 entry-criteria review. |

Runner tests do not require Pester: launch child PowerShell sessions with fixed test-owned command doubles, capture only safe exit/phase data and inspect fresh output. Keep them independent of the production function bodies. Actual Windows/Linux hosted execution is still required; mocks cannot complete V04.

Inventory regression checks use unittest, tempfile and the current checker in child processes. Cases separately cover unrouted skill, routed missing skill/SKILL.md, duplicate target ID, missing skill path and missing runbook path.

FR-012 is a documentation deliverable in agents/TESTING.md under Runtime test entry criteria. Its V08 acceptance review must independently check all five items: approved interfaces/infrastructure, isolated setup, bounded readiness, owned teardown, and explicit failure controls with observable durable outcomes. Provide a pending owner/feature reference for each absent capability. This review does not create or execute runtime tests.

Behavioral evals load the project rules and approved worktree workflow in writing-capable disposable repositories. They withhold grading criteria, preserve unrelated dirty sentinel bytes and verify first substantive command/edit occurs inside the selected isolated worktree. The follow-up fixture precreates a matching path and branch and records the worktree count; passing requires that exact path/branch and no additional worktree. These are workflow grades, independent of any skill-routing grade. A routing/static pass is not a substitute. Do not publish model/session attribution in Git history; keep required private run metadata in ignored traces.

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
- Unexpected sensitive assertion output must never be retained or exported; a source-allowlisted collector, in-memory discard and explicit exports are mandatory. If installed-platform integration cannot deliver this without raw diagnostics, stop for a plan revision rather than silently enabling raw reporting.
- No claim of token savings is made; measuring that benefit is separate.

## Complexity Tracking

No constitution violation or additional dependency is proposed. One required-identity file, one safe test-platform consumer, one execution receipt and one parent manifest address distinct failure boundaries; the existing joiner and result/fixture metadata remain authoritative. The consumer is a reporter extension within the current test project, not a test-framework replacement.

## Approved attachment amendment

The Owner approved pre-execution attachment rejection on 2026-11-25 after the actual canary proved MSTest stages files before publishing events. The guard is an internal metadata inspection step using the existing runtime. Group B delivers/test-drives the guard and receipt policy field; group C integrates its before-load parent phase and artifact attestation. No genuine forbidden attachment is executed in the supported full check. Standalone source/policy flags remain false until a verified parent attestation is supplied. See [decision and independent review](attachment-policy-proposal.md).

