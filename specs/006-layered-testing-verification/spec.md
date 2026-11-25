# Feature Specification: Layered testing and development verification

**Feature Branch**: `codex/layered-testing-verification`

**Created**: 2026-11-25

**Status**: G1 approved by the Owner on 2026-11-25; feature number 006 confirmed.

**Input**: Establish repeatable local/CI verification covering application tests, agent/skill evals and review evidence. Make failures and unverified behavior explicit, reusing existing capabilities.

## User Scenarios & Testing *(mandatory)*

Feature-local IDs V01–V08 identify verification acceptance cases, not new booking scenario IDs. The plan/tasks must map these to executable checks or recorded behavioral evaluations.

### User Story 1 - Verify an application change locally (Priority: P1)

A developer runs one documented backend check and receives a trustworthy outcome with inspectable evidence.

**Why this priority**: Reliable local feedback provides the foundation for CI.

**Independent Test**: Run from a clean checkout with documented prerequisites, then exercise controlled failures without weakening application assertions.

**Acceptance Scenarios**:

1. **V01 — Reproducible check.** Given a clean checkout and required toolchain, when verification runs, dependencies restore reproducibly, implemented projects compile and all required selected tests execute. Success requires every required phase to succeed.
2. **V02 — Honest failures.** Given a prerequisite, restore, build, test or evidence failure, verification fails, identifies the phase and retains available safe diagnostics. Earlier phase failures are not presented as passing tests.
3. **V03 — Fresh complete evidence.** Stale, malformed, duplicate, unmapped, missing or skipped required results cannot satisfy the current run. An empty required suite is not success.

### User Story 2 - Obtain the same evidence in CI (Priority: P1)

Contributors and reviewers receive repeatable hosted outcomes and available reports after failures.

**Why this priority**: Local success does not establish reproducibility in a clean hosted environment.

**Independent Test**: Exercise actual passing and controlled failing hosted runs, recording revisions, outcomes and available reports.

**Acceptance Scenarios**:

1. **V04 — Hosted verification.** A pull request, push or manual request starts bounded Windows/Linux checks using the shared process. Jobs have independent outcomes. Failed required checks fail the relevant job. Available named reports survive test failure; failure before report creation is explicit.
2. **V05 — Safe isolation.** Concurrent runs and retries use fresh output without overwriting another run or changing unrelated worktrees/services/volumes. Published evidence excludes credentials and guest identity fields.

   **Approved attachment amendment (2026-11-25)**: Backend result attachments are unsupported. The full-check parent rejects forbidden attachment calls and unsupported execution paths by inspecting built metadata before loading the test executable. Attachment canaries are rejected before test bodies run; real assertion/stdout failure cases remain separate. Legacy direct evidence stays policy-unverified and ineligible for full verification. This is a closed-suite policy, not an arbitrary-code filesystem sandbox.

### User Story 3 - Evaluate the development workflow (Priority: P2)

A maintainer checks harness inventory and agent behavior while keeping static validation and behavioral proof distinct.

**Why this priority**: Application tests do not prove that agents respect task scope or preserve another task's changes.

**Independent Test**: Validate valid/invalid inventory inputs, then grade worktree behavior in fresh sessions against disposable synthetic workspaces using command/file-read traces.

**Acceptance Scenarios**:

1. **V06 — Static validation.** Valid routes/targets pass; missing routes, invalid references and duplicate target identities fail. A static pass does not claim behavioral execution.
2. **V07 — Worktree behavior.** Case A starts a concrete task in main with unrelated dirty files: the agent establishes an isolated task worktree before substantive investigation/edits, reports path/branch/baseline and preserves unrelated changes. Case B gives a follow-up with an existing matching task worktree: the agent reuses it. Each case has an independent trace/grade. Explicit Owner permission for a specific main-worktree edit is recorded as an exception; starting in main is not permission.

### User Story 4 - Review the complete verification picture (Priority: P2)

The Owner can see passed checks, pending proof and remaining review gates without recovering decisions from chat history.

**Why this priority**: Green totals must not hide unimplemented runtime scenarios or replace review.

**Independent Test**: Inspect a packet containing contract results, pending runtime proof and behavioral grades, each with its own meaning and source baseline.

**Acceptance Scenarios**:

1. **V08 — Coverage visibility.** The packet accounts for every catalog scenario/path cell with proof level, related results and passed/failed/pending/inapplicable status. Pending/inapplicable cells have reasons. Contract/eval passes leave absent runtime proof pending. The guide identifies specification, plan, consistency, code-review and final-verification responsibilities.

### Edge Cases

- Required toolchain absent, dependency retrieval unavailable or dependency lock mismatch.
- Optional output-compaction helper absent; verification must still work with the base toolchain.
- Restore/build failure before reports exist; failed/hanging/skipped tests or zero selected cases.
- Stale reports, removed required methods, unmatched identities and interrupted/concurrent output.
- Historical eval grades cover one case while live capabilities remain unverified.
- An agent starts in main with unrelated dirty files or finds an existing matching worktree.
- Candidate diagnostics contain credentials or guest identity fields; they must not be published.
- Hosted access unavailable: record the blocker; local success cannot satisfy V04.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Provide one documented full backend check shared locally/by CI, with prerequisites and focused alternatives. It MUST work without optional compaction tooling. Covers V01–V02.
- **FR-002**: Restore reproducible dependencies, compile implemented projects and execute required selected tests. Failed prerequisite phases MUST stop verification and preserve failure. Covers V01–V02.
- **FR-003**: Associate evidence with its source baseline/invocation, distinguish phase from test failures and reject stale, malformed, duplicate, unmapped, missing/skipped required results. Empty required suites MUST fail. Covers V02–V03.
- **FR-004**: Run bounded Windows/Linux checks on pull requests/pushes and support manual execution. Retain available named safe reports after failure and reflect failed required checks in job outcomes. Covers V04–V05.
- **FR-005**: Use synthetic data and fresh isolated output. Generated evidence MUST remain outside tracked output and exclude guest identity fields/credentials. Cleanup MUST NOT affect another task's resources. Covers V05.
- **FR-006**: Document unit/contract, integration, backend E2E, frontend component/browser tests; static harness checks; behavioral evals; tooling smoke checks; and review gates. State purpose, availability and execution policy for each. Covers V08.
- **FR-007**: Retain executable local/scoped-CI inventory validation rejecting invalid references and duplicate targets without claiming behavioral execution. Covers V06.
- **FR-008**: Register new-task and existing-task worktree eval cases with independently gradable prompts, expected behavior, prohibited actions and trace checks in disposable synthetic workspaces. Covers V07.
- **FR-009**: Record behavioral target/case, source baseline, prompt, known harness/model, actual trace/checks, grade and live limitations. Unavailable capabilities MUST remain blocked/unverified; behavioral grades MUST NOT become runtime proof. Covers V07–V08.
- **FR-010**: Preserve booking scenario IDs/intake paths with test/result mappings, proof levels and reasons for pending/inapplicable cells. Account for the full catalog; contract passes MUST NOT satisfy required runtime proof. Covers V03/V08.
- **FR-011**: Inventory implemented behavior and concrete uncovered assertions in the plan before adding application tests. Added tests MUST address documented gaps without changing contracts, weakening assertions or claiming proof for absent services. Covers V01/V08.
- **FR-012**: Define runtime test entry criteria: approved interfaces/infrastructure, isolated setup/readiness/teardown, failure controls and observable durable outcomes. Runtime features MUST supply their tests before completion. Covers V08.
- **FR-013**: Preserve Owner gates and separate-session review. Distinguish CI execution from branch protection. Passing tests/evals MUST NOT authorize merging. Covers V08.
- **FR-014**: Prohibit backend result attachments in full-check mode. Inspect owned compiled metadata before test executable loading; reject forbidden attachment calls, unresolved/unsupported execution paths and user module initializers. Record a distinct post-build policy phase and bind success to the executed artifact digest. Forbidden canaries MUST leave test bodies unexecuted and platform exit unset. Legacy direct evidence MUST remain policyVerified=false and ineligible for full verification. Covers V02, V03, V05; implements the Owner-approved attachment amendment.

### Key Entities

- **Verification run**: Source baseline, invocation, selected checks, phase outcomes and fresh output.
- **Check result**: Application test, inventory check, tooling operation or behavioral case and its scoped outcome; these categories are not interchangeable.
- **Coverage cell**: Booking scenario/path, required proof level, related results, status and pending/inapplicable reason.
- **Behavioral record**: Case identity, baseline, prompt, trace, grade and live limitations.
- **Review packet**: Evidence, limitations and unresolved findings for the applicable Owner gate.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A clean checkout completes the full check on both supported operating systems with all required selected tests executed and fresh evidence. Demonstrates V01/V04.
- **SC-002**: Every exercised phase-failure/invalid-evidence case fails; zero are reported as completed passes. Demonstrates V02–V03.
- **SC-003**: Passing and controlled failing hosted runs provide source-linked outcomes and available safe failure evidence, with zero published credentials or guest identity fields. Demonstrates V04–V05.
- **SC-004**: Valid inventory passes; each existing invalid-inventory regression case fails with an actionable finding. Demonstrates V06.
- **SC-005**: Both worktree cases have fresh-session traces and independent passing grades, zero unrelated file changes and matching-worktree reuse for follow-ups. Demonstrates V07.
- **SC-006**: The packet accounts for 100% of catalog scenario/path cells with proof levels and reasons for every pending/inapplicable cell; zero absent runtime proofs are reported as passed. Demonstrates V08.
- **SC-007**: One linked guide covers all FR-006 categories, their availability/execution policy and applicable review gates. Demonstrates V08.

## Assumptions

- The Owner approved G0 intent, confirmed number 006 and approved G1/G2 on 2026-11-25. Implementation and actual verification remain outstanding.
- Existing libraries, suite, result mapper, shared check and CI are starting capabilities. The plan confirms reuse and necessary changes rather than scaffolding replacements.
- Existing .NET/MSTest remains the default. New dependencies/services/ports require plan approval; none are selected here.
- Hosted execution requires repository access/runner availability. The Owner controls branch protection/merge. Missing hosted execution remains a completion blocker.
- Evals use the manual disposable-workspace process. No live judge, automatic model execution or external-model provisioning is introduced.
- Faster feedback and less repeated investigation are intended benefits. Token savings are not claimed without a measured comparison.

## Scope and dependencies

**In scope**: Trustworthy local/CI checks for implemented behavior; a test-gap inventory; safe evidence; existing static eval checks; worktree behavioral cases; the development verification guide; and the full scenario/path packet through the existing handoff process.

**Out of scope**: Booking contract/state changes; implementing hosts/persistence/Kafka/PDS/UI; placeholder runtime tests; broad refactoring; new test framework; automated judge service; deployment; global hook changes; and the automated coverage dashboard/timelines assigned to feature 005.

**Booking scenario boundary**: Existing INT-01–05 and SAL-01–02 contract assertions and PDS-01–07 decision assertions may run as current evidence; their runtime proof remains pending. INT-06, H01-01–04, CAS-01–04, BRK-01–04 and KFK-01–03 require absent runtime components. H01 follows the Owner's rename on main (a594070); this feature introduces no rename itself. All intake paths remain visible. No booking scenario is implemented/retired here. The full matrix is a verification deliverable, not a catalog replacement or a claim that every scenario passed.

**Dependencies**: Feature 001 supplies contracts/decision tests; 002 supplies isolated infrastructure; later intake/persistence work supplies transactions/HTTP; 003 supplies Kafka proof; 004 supplies PDS proof; UI work supplies component/browser proof. This feature establishes verification without waiting for those services.

**Design review**: [Verification flow](../../diagrams/006-layered-testing-verification/flow.drawio) shows existing capabilities, proposed verification and future runtime layers. No hotel/PMS/PDS data flow or state transition changes are proposed.

