# Planning research: feature 006

## Baseline and scope

Source inspected at the approved-spec baseline, now bf47a41 after rebasing onto a594070. The rebase brings in the Owner's H10-to-H01 rename and UML guidance. No tests, evals, Docker services or hosted workflows were executed during this research. The feature worktree has no Graphify graph, so navigation fell back to targeted source reads. No graph rebuild is required for these documentation changes.

## Decisions

| Decision | Rationale | Alternative rejected |
| --- | --- | --- |
| Reuse SDK 10.0.404, MSTest.Sdk 4.5.3 and existing lock | Existing project/global.json already select a compatible runner; scope requests reuse | Framework migration or new test packages add no needed capability |
| Keep local and CI runner shared | Existing runner already restores locked dependencies and builds the actual project | Parallel local/CI command implementations drift |
| Add parent run.json before execution | Existing fresh suite directory only begins after restore/build; earlier failures leave no durable report | Inferring phase success from absent TRX or final exit alone |
| Add safe execution.json inside suite | Platform failure and evidence rejection must retain separate outcomes | Treating the wrapper's nonzero code as proof the application tests failed |
| Keep independent required method identities | Reflection sees only remaining methods, so a deletion can shrink expectations | Generating the required list from the current assembly at run time |
| Collect safe test events at source; export safe JSON | Raw TRX failure details/attachments may contain arbitrary asserted values. Installed MTP supports a data consumer without new packages | Generating then deleting raw TRX cannot guarantee privacy if interrupted; wildcard uploads and console-detail flags alone are insufficient |
| Add plain PowerShell and unittest checks | Existing runtimes provide the necessary process/tempfile features | Adding Pester/pytest just for a small orchestration checker |
| Keep worktree evals manual | Existing eval protocol already requires isolated fresh sessions and trace grading | Configuring a new live judge or model-provider execution in CI |
| Keep matrix in verifier packet | Workflow already requires complete scenario/path reporting | Pulling feature 005 dashboard implementation into this feature |

## Source-backed inventory

- agents/hooks/test-backend.ps1: locked restore/build/native exit handling exists; no source/phase manifest.
- .github/workflows/backend-tests.yml: Windows/Linux matrix, job timeout and always-upload exist; paths currently match all GUID run directories and include raw TRX.
- tests/Unit/Booking.ContractTests/ScenarioEvidence.cs: CompiledMap reflects current TestMethod attributes; Index rejects an empty map; Join validates timestamps, definitions, counters and outcomes; RunAsync fails incomplete results.
- EvidenceCases.cs: seven methods already exercise metadata/fixtures, skipped/missing results, duplicates, malformed reports, stale timestamps, failed exits and incomplete runs. Reuse these protections.
- IntakeCases.cs: FirstImport/StaleUpdate cover create/newer update but do not assert event BookingId linkage. ConflictingSnapshot checks intent existence but not its complete scope/time/reason identity.
- src/Booking.Domain/BookingIntake.cs: existing Create/Apply events use the accepted BookingId; equal-time branches specify scoped SnapshotConflict/ComparisonUnavailable intent. These are already specified behavior, not new contracts.
- Archived 001 approved-decisions.md and tasks.md document opaque event references and exact-instant scoped reconciliation.
- eval/check_index.py: five independently testable negative conditions exist. eval/Skill_review_2026-11-24.md records a historical combined fixture, not a maintained per-condition suite.
- eval/README.md: fresh disposable case sessions and trace grading already apply; no automatic live judge configured.
- Existing eval cases cover specialist routing and Kafka advice, not worktree creation/reuse.
- e2e/fixtures/scenarios.md: baseline 30 IDs across three intake paths. Runtime requirements remain outside the implemented libraries.

## Resolved uncertainties

The pinned Microsoft.Testing.Platform 2.5.3 package supports TestingPlatformBuilderHook and TestHost.AddDataConsumer. An IDataConsumer can subscribe to TestNodeUpdateMessage and use terminal-state types plus known TestMethodIdentifierProperty metadata. The adapter's AssemblyFullName may be empty: resolve namespace/type/method against the selected compiled assembly and reject ambiguity, never use display names. Source feasibility is verified; actual local/hosted collector behavior still requires V04/V05 execution evidence.

Primary references at the locked Microsoft source revision: [managed test identity conversion](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestNodeConverter.cs), [builder hook](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Platform/Microsoft.Testing.Extensions.TrxReport/TestingPlatformBuilderHook.cs), [stock reporter options](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Platform/Microsoft.Testing.Extensions.TrxReport/TrxCommandLine.cs). No external judge/model calls were made.

All design choices above use existing repository capabilities. No unresolved dependency, port or service choice remains. Actual hosted results and behavioral traces are implementation-stage evidence, not assumptions of success.
