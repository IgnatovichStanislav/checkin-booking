# Tasks: Layered testing and development verification

**Input**: Approved [spec](spec.md), [plan](plan.md), [evidence model](data-model.md), [tool contract](contracts/verification.md), [research](research.md) and [validation guide](quickstart.md).
**Gate**: G1/G2 approved. [Independent consistency analysis](tasks-analysis.md) passed after task-only citation corrections; group A may begin.
**Scope**: V01–V08; supporting booking assertions are contract-level only. No runtime services, new dependencies or booking rule changes.
**Baseline**: Isolated codex/layered-testing-verification worktree. Task IDs are execution checkpoints, not claims of passing behavior.

## Format and evidence rules

All tasks use checkbox/ID/optional [P]/story/path format. Tests are required by this feature. Write the relevant failing check before or alongside implementation; never weaken a contract to pass.
A task may be checked only after its named evidence exists. Application runtime scenario proof remains pending even when related contract assertions pass.

## Phase 1: Setup

- [X] T001 Record the source/worktree/toolchain baseline and existing required method identities in specs/006-layered-testing-verification/implementation-evidence.md; inspect global.json and tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj, run the existing selected suite, and record actual counts without persisting raw failure details. Scenarios: V01, V03; current contract suite, not runtime proof.

## Phase 2: Foundation — independently required tests

- [X] T002 Add failing required-identity tests to tests/Unit/Booking.ContractTests/EvidenceCases.cs for empty/duplicate expected inventories and removed/unexpected discovered methods, independently of reflection. Scenarios: V03; no booking scenario change.
- [X] T003 Add reviewed tests/Unit/Booking.ContractTests/required-tests.json, copy it through Booking.ContractTests.csproj and enforce exact expected/discovered identities in ScenarioEvidence.cs; cite [data-model.md §Required identity inventory](data-model.md#required-identity-inventory), Required identity inventory.assembly/class/method and its unique/nonempty/normalization constraints. Scenarios: V03.
- [X] T004 Strengthen tests/Unit/Booking.ContractTests/IntakeCases.cs create/newer-update event BookingId linkage and exact conflict/erased-comparison reconciliation scope, BookingId, source UpdatedAt, Open and reason using existing fixtures; run these assertions and the full current suite. Scenarios: V01, INT-01, INT-03, INT-04, SAL-01 at contract level only.

**Checkpoint / group A**: T001–T004 pass focused checks and receive a separate read-only code review before any further source group. No receipt/reporter/runner/CI implementation in this group.

## Phase 3: User Story 1 — reliable local verification (P1, MVP)

**Goal**: Trustworthy local full-check result with safe source-bound evidence.
**Independent test**: Real clean local check plus negative inventory, source/build, phase and privacy cases, without services.

- [x] T005 [US1] Add safe platform-event collector tests in tests/Unit/Booking.ContractTests/EvidenceCases.cs for known terminal identities/states, absent/ambiguous/unknown identifiers, duplicate terminal updates and ignored discovery/progress/unsafe fields. Scenarios: V02, V03, V05; no booking runtime proof.
- [x] T006 [US1] Add tests in tests/Unit/Booking.ContractTests/EvidenceCases.cs for receipt outcomes, missing/malformed/mismatched context, invalid/missing/duplicate CLI arguments, actual nonzero platform exit, incomplete evidence and standalone verificationEligible=false; cite [data-model.md §Child execution receipt](data-model.md#child-execution-receipt-suiteexecutionjson), Child execution receipt.schemaVersion/platformExitCode/testOutcome/evidenceOutcome/failureCode/runId/sourceDigest/joinedRunId and nullable/outcome/binding rules. Scenarios: V02, V03, V05.
- [ ] T007 [US1] Complete SafeTestEvidence.cs and its installed builder hook in tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj, keeping stock TRX disabled and only safe XML. Implement agents/hooks/check-attachment-policy.ps1 using bundled PEReader/MetadataReader without target assembly loading; inspect all owned method/helper/constructor/async bodies and reject AddResultFile references, unsupported indirect/custom/lifecycle/data/inherited paths, unresolved metadata and module initializers. Add disposable compiled direct/helper/unsupported/malformed canaries outside normal scanned modules and assert safe rejection before test-body effects, platform exit or staging; no new package/project required. Keep required-tests.json intentional. Parent before-load integration belongs to T010–T011; group B alone cannot claim full policy/source eligibility. Scenarios: V01, V02, V03, V05; FR-014, approved attachment amendment.
- [ ] T008 [US1] Complete safe execution receipt, explicit --run-context parsing with invalid/missing/duplicate argument rejection and standalone source-unverified behavior in tests/Unit/Booking.ContractTests/ScenarioEvidence.cs and EvidenceEntryPoint.cs; preserve existing join invariants and fixed safe errors. Explicitly implement Child execution receipt.schemaVersion/platformExitCode/testOutcome/evidenceOutcome/failureCode/runId/sourceDigest/joinedRunId/policyVerified and nullable/outcome/binding rules in [data-model.md](data-model.md#child-execution-receipt-suiteexecutionjson), plus legacy semantics in [contracts/verification.md](contracts/verification.md#entry-points). Prior parser/receipt implementation at 0cf8605 is retained; this task reopens only for the approved policyVerified amendment. Scenarios: V02, V03, V05.
- [ ] T009 [US1] Create agents/hooks/check-backend-runner.ps1 with disposable child-process doubles checking missing prerequisites, native/helper paths, restore/build/policy/test/evidence/export failure transitions, zero selection, invalid context and exact exits; policy failure must leave tests notRun and platform exit unset. Do not mock away the separate real-suite check. Scenarios: V01, V02, V03, V05.
- [ ] T010 [US1] Implement run lifecycle and source/build binding in agents/hooks/test-backend.ps1; cite Run manifest.schemaVersion/runId/source/environment/phases/result/evidence and all status/time/path rules in [data-model.md](data-model.md#run-manifest-runjson), plus exact scoped inputs, ignored/outside-scope rejection, forced rebuild, artifact digest and standalone limits in [source/build binding](data-model.md#source-and-build-binding). Add explicit post-build policy phase invoking check-attachment-policy.ps1 BEFORE executable load; bind its verdict to checked artifacts and do not start rejected test code. Scenarios: V01, V02, V03, V05; FR-014.
- [ ] T011 [US1] Enforce the parent build and before-load policy attestations against loaded assembly/copied output and source context in tests/Unit/Booking.ContractTests/ScenarioEvidence.cs; extend agents/hooks/check-backend-runner.ps1 for stale binary after source/fixture change, tracked deletion, allowed untracked source, archived contract JSON mutation and mismatched/missing policy binding. Cite [data-model.md §Source and build binding](data-model.md#source-and-build-binding), Run manifest.source and Child execution receipt.runId/sourceDigest/policyVerified; legacy direct policyVerified/verificationEligible stay false. Scenarios: V03, V05; FR-014.
- [ ] T012 [US1] Add in-memory child stdout/stderr discard, safe phase/ID/count summaries and field-allowlisted export to agents/hooks/test-backend.ps1; test synthetic assertion/stdout/metadata sentinels against retained files/summaries, concurrent run roots and interruption/no-report paths in agents/hooks/check-backend-runner.ps1 and tests/Unit/Booking.ContractTests/EvidenceCases.cs. Independently verify attachment-policy rejection creates no test-body effects/staging; do not run forbidden attachment canaries as normal suite methods. Cite safe record/export rules in [data-model.md](data-model.md#existing-records-retained) and [contracts/verification.md](contracts/verification.md#export-rules). Scenarios: V02, V03, V05.
- [ ] T013 [US1] Run the actual local full check and all local negative/privacy checks; record commands, source IDs, safe report paths and outcomes in specs/006-layered-testing-verification/implementation-evidence.md and ignored test-results/. Distinguish local proof from hosted V04. Scenarios: V01, V02, V03, V05.

**Checkpoints**: Group B T005–T008 (safe collector/receipt); group C T009–T013 (runner/source/export). Each group receives code review and focused checks before the next group. US1 is the smallest complete verification MVP; group A alone is a foundation, not completion.

## Phase 4: User Story 2 — hosted verification (P1)

**Goal**: Reproduce source-bound safe checks on both hosted operating systems.
**Independent test**: Actual passing and controlled early/test-failing hosted jobs with inspected logs/artifacts.

- [ ] T014 [US2] Extend agents/hooks/check-backend-runner.ps1 with CI output/path and artifact-allowlist assertions: current-run export only, failure metadata present, historical roots/raw reports excluded and failed tests still fail. Scenarios: V02, V04, V05.
- [ ] T015 [US2] Update .github/workflows/backend-tests.yml to run runner checks plus real suite on Windows/Linux and publish the exact current-run safe export with bounded jobs/retention and safe failure summaries. Use the GITHUB_OUTPUT contract in specs/006-layered-testing-verification/contracts/verification.md. Scenarios: V04, V05.
- [ ] T016 [US2] Execute actual passing hosted Windows/Linux checks and record run URLs, commit IDs, jobs and inspected safe artifacts in specs/006-layered-testing-verification/implementation-evidence.md. Missing access remains blocked; YAML/local mocks are not proof. Scenarios: V01, V04, V05.
- [ ] T017 [US2] Use disposable controlled changes to execute hosted early-failure and returned-test-failure paths; verify phase outcomes, nonzero jobs, safe metadata/failed-result exports and no privacy sentinel in retained artifacts or hosted logs; record source-linked results in specs/006-layered-testing-verification/implementation-evidence.md without retaining a permanent failing change. Scenarios: V02, V04, V05.

**Checkpoint / group D**: T014–T017 code review plus actual hosted evidence. Repository/runner access may keep V04 pending; do not fake completion.

## Phase 5: User Story 3 — development-tool evals (P2)

**Goal**: Independently validate inventory behavior and grade worktree creation/reuse.
**Independent test**: Stdlib valid/invalid fixtures and two fresh-session behavioral traces in disposable Git repositories.

- [ ] T018 [P] [US3] Add eval/test_check_index.py using unittest/tempfile/child processes for valid inventory and independent unrouted skill, routed missing skill/SKILL.md, duplicate target ID, missing skill path and missing runbook cases; preserve imported assets. Scenarios: V06.
- [ ] T019 [P] [US3] Write independent prompts/setup/grading in eval/cases/worktree-new-task.md and eval/cases/worktree-follow-up.md; use unrelated dirty sentinels and recorded before/after hashes/status, exact precreated follow-up path/branch/count, writing capability and command/read traces. Cite Behavioral eval record.case/baseline/prompt/trace/checks/grade/limits from [data-model.md](data-model.md#coverage-and-eval-records). Scenarios: V07.
- [ ] T020 [US3] Register both cases in eval/targets.json with existing relevant skill references, document their independent workflow grading in eval/README.md, and run real inventory plus T018 regressions; retain target paths and explicitly unexecuted statuses until graded. Scenarios: V06, V07.
- [ ] T021 [US3] Update .github/workflows/skill-index.yml to run regression and actual inventory checks and include agents/WORKFLOW.md in affected-guidance path triggers. Verify actual static job results in specs/006-layered-testing-verification/implementation-evidence.md without claiming behavioral execution. Scenarios: V06.
- [ ] T022 [US3] Execute and independently grade worktree-new-task in a fresh session/disposable Git repository, verifying isolation-before-substantive-work and preservation of unrelated bytes; retain private evidence under eval/results/ and safe findings/limits in specs/006-layered-testing-verification/implementation-evidence.md. Scenarios: V07.
- [ ] T023 [US3] Execute and independently grade worktree-follow-up in a separate fresh session/disposable Git repository; verify exact existing path/branch reuse, unchanged worktree count and preserved unrelated bytes; retain private evidence under eval/results/ and safe findings/limits in specs/006-layered-testing-verification/implementation-evidence.md. Scenarios: V07.

**Checkpoint / group E**: T018–T023. Automated inventory pass does not complete either behavioral case. Missing writing-capable sessions are explicit blockers.

## Phase 6: User Story 4 — complete review picture (P2)

**Goal**: Owner sees evidence levels, pending runtime proof and applicable review gates.
**Independent test**: Full guide/category review and complete catalog/path matrix using actual metadata/results.

- [ ] T024 [US4] Update agents/TESTING.md and eval/README.md with available test/eval categories, entry points, evidence/phase/source/privacy semantics and review gates. Add Runtime test entry criteria covering approved interfaces/infrastructure, isolated setup, bounded readiness, owned teardown and explicit failure controls with observable durable outcomes; identify the future owner/feature for absent capabilities. Scenarios: V08; FR-006, FR-012, FR-013.
- [ ] T025 [US4] Produce specs/006-layered-testing-verification/scenario-coverage.md from the unchanged e2e/fixtures/scenarios.md catalog and actual compiled mapping/results: every scenario/path cell, proof level, supporting test IDs, runtime verdict and reason for pending/inapplicable. Cite Coverage cell scenario/path/proof/results/status/reason semantics in [data-model.md](data-model.md#coverage-and-eval-records). Scenarios: V08; all INT/H01/SAL/CAS/BRK/KFK/PDS IDs represented, absent runtime proof remains pending.
- [ ] T026 [US4] Review the V08 guide/matrix packet against all FR-006 categories, each FR-012 criterion and Owner/separate-session gate rules; record pass/finding per criterion in specs/006-layered-testing-verification/implementation-evidence.md, with no empty matrix cell or unreasoned pending/inapplicable state. Scenarios: V08.

**Checkpoint / group F**: T024–T026. No automated coverage dashboard, runtime scaffolding or booking contract change.

## Phase 7: Final verification and handoff

- [ ] T027 Refresh the per-worktree graph after source changes using agents/hooks/refresh-graph.ps1, report parser/coverage failures and compare diagrams/006-layered-testing-verification/*.drawio with implementation; parse XML and inspect rendered layout if an existing viewer/export is available, otherwise retain the unverified visual-layout limitation. Scenarios: V08; no correctness claim from graph topology.
- [ ] T028 Run only required final local/hosted/static/behavioral checks not already valid for the current source, using specs/006-layered-testing-verification/quickstart.md; record literal results and remaining blockers in implementation-evidence.md. Scenarios: V01–V08; do not rerun unchanged successful checks just for output.
- [ ] T029 Obtain separate read-only final verification/convergence judgment against spec.md, plan.md, task results, scenario-coverage.md and actual safe evidence; resolve blocking findings through scoped implementer/reviewer handoffs and prepare the Owner G3 merge packet in specs/006-layered-testing-verification/implementation-evidence.md. Scenarios: V01–V08; merging remains Owner-only.

## Dependencies and execution groups

Setup T001 → foundation T002–T004 → US1 T005–T013 → US2 T014–T017.
After foundation, US3 T018–T023 may be developed independently of US1/US2; actual worktree writes remain one session at a time. US4 documentation T024 may start after the contract is stable, but T025–T026 require actual result metadata; final verification depends on all stories and explicit blockers.

| Group | Tasks | Review boundary |
| --- | --- | --- |
| A | T001–T004 | Independent identities and scoped intake assertions |
| B | T005–T008 | Safe platform consumer/receipt; source binding completed in C |
| C | T009–T013 | Source/build lifecycle, native exits and safe retained output |
| D | T014–T017 | Hosted pass/early-failure/test-failure proof |
| E | T018–T023 | Static inventory and separate manual workflow grades |
| F | T024–T026 | Layer guide, runtime entry criteria and complete coverage packet |
| Final | T027–T029 | Graph/diagram consistency, actual results, G3 packet |

Cross-story partial work is not story completion. If a group's checks reveal a design/contract problem, use the existing escalation rule; do not silently change approved artifacts.

## Requirement coverage

| Requirements | Tasks |
| --- | --- |
| FR-001–FR-002 | T001, T009–T010, T013, T015–T017 |
| FR-003 | T002–T003, T005–T012, T014, T017 |
| FR-004 | T014–T017 |
| FR-005 | T005–T012, T014–T017, T019, T022–T023 |
| FR-006 | T024, T026 |
| FR-007 | T018, T020–T021 |
| FR-008–FR-009 | T019–T020, T022–T023 |
| FR-010 | T003, T008, T025–T026 |
| FR-011 | Existing approved plan gap inventory; T004 |
| FR-012–FR-013 | T024, T026, T029 |
| FR-014 | T007–T012, T014–T017, T024 |

## Constrained-field traceability

Canonical entity names below refer to the corresponding titled sections of data-model.md; no new constraints are introduced here.

| Canonical fields/constraints | Implementing tasks |
| --- | --- |
| Run manifest.schemaVersion/runId/source/environment/phases/result/evidence, enums, UTC times, safe relative paths | T010, T012 |
| Source/build binding scoped inputs, present/deleted/untracked handling, sourceDigest/artifactDigest and verification eligibility | T010–T011 |
| Child execution receipt.schemaVersion/platformExitCode/testOutcome/evidenceOutcome/failureCode/policyVerified, nullable exit, run/source/TRX/policy binding | T006, T008, T011 |
| Required identity inventory.assembly/class/method normalization, uniqueness, nonempty exact set | T003 |
| Existing records: safe metadata/outcome allowlists, business/harness separation, current invocation | T007–T008, T012 |
| Coverage cell scenario/path/proof/results/status/reason; Behavioral eval record.case/baseline/prompt/trace/checks/grade/limits | T019, T022–T025 |

## Parallel opportunities and implementation strategy

T018 and T019 touch different files and have no incomplete dependency on one another. They can be assigned independently when isolated/session ownership is coordinated. Within US1, the collector and receipt share the same test/evidence source, so do not mark them parallel. US2 hosted proof follows real local behavior. US4 requires actual source-bound results for its final packet.

Deliver A first, then B/C as the local MVP. Review each group before continuing. Complete E and D when their required local behavior and access are available, then F/final. No extra Owner approval is introduced between reviewed groups; existing G3 ownership and contract-change gates remain.

