# AI workflow

Roles, agent levels, stages and gates for Checkin Booking. The .specify/memory/constitution.md (v1.0.0), ratified by the Owner on 2026-11-24, governs; root AGENTS.md supplies consistent operational guidance; this file defines who does what and when. It configures no agent runtime.

## 1. Agent levels

Use Codex models with medium reasoning for all roles. Levels describe responsibilities;
L2 and L3 share the same routine-task model.

| Level | Model | Reasoning | Used for |
| --- | --- | --- | --- |
| L1 Thinker / Reviewer | GPT 6.2 Astra (`gpt-6.2-astra`) | medium | Specify, clarify, plan, task design, analyze, review and convergence judgment |
| L2 Builder | GPT 6.2 Sol (`gpt-6.2-sol`) | medium | Routine implementation of approved tasks, tests and scoped fixes |
| L3 Runner | GPT 6.2 Sol (`gpt-6.2-sol`) | medium | Run checks, summarize actual output and refresh graphs |

Rules:

- Escalate from Sol to Astra medium when work needs design reasoning, touches durability, freshness or PDS state rules, or the same stage fails twice. If Astra cannot resolve it, ask the Owner; do not silently increase reasoning effort.
- Reviews use Astra medium in a separate session from the author or implementer. If Astra authored the artifact, use a fresh Astra reviewer session; this is separate-session review, not cross-model or cross-harness verification.
- Record harness, model (if known), reasoning and level in private handoffs only. Never guess model identity or add attribution/session links to commits, PRs or other GitHub content. Public reports contain findings and verification evidence only.
- This document defines model selection policy; it does not configure a runtime, launch agents or change the current session's model. If a required model is unavailable, report that before substituting.

## 2. Codex model mapping

Select `gpt-6.2-astra` with `medium` reasoning for thinking and review; select
`gpt-6.2-sol` with `medium` reasoning for routine tasks. Verify supported selection
controls in the actual client before starting a role. No Claude or GLM mapping is required.

- Start each session with "Read AGENTS.md and agents/README.md first." Keep shared setup under agents/ with no project .codex/ or .claude/ configuration.
- Name the required agents/skills/<name>/SKILL.md explicitly rather than assuming automatic discovery.
- Reviewer sessions are read-only. Verification may write generated test results or graph output, but MUST NOT modify application source or fix reviewed code.

## 3. Roles

| Role | Level / model (medium reasoning) | Does | Never |
| --- | --- | --- | --- |
| Owner | Human | Approves gates, answers clarifications, merges, reserves spec numbers | - |
| Author | L1 / Astra | specify, clarify, plan | Write application code in these stages |
| Plan Reviewer | L1 / Astra, separate session | Reviews spec and plan against constitution and AGENTS.md | Edit reviewed files |
| Tasker | L1 / Astra | Designs tasks; every test task names its scenario IDs | Change approved spec or plan |
| Analyzer | L1 / Astra, separate session | Checks consistency of spec, plan and tasks | Edit reviewed files |
| Implementer | L2 / Sol | Implements an approved task group, tests before or alongside code; escalates design questions to Astra | Change contracts or weaken tests |
| Code Reviewer | L1 / Astra, separate session | Reviews the task-group diff | Edit code |
| Verifier | L3 / Sol runs; L1 / Astra judges | Runs checks, refreshes graph after source changes, builds coverage evidence; Astra evaluates convergence | Fix code |

Thinking and review stay on Astra medium even when the surrounding implementation runs
on Sol medium. Owner review and merge gates remain unchanged.

## 4. Worktrees

- Start every concrete repository problem, feature or task in an isolated worktree, including discussion, investigation, specification and planning. A request to discuss that work authorizes creating a local branch/worktree; it does not approve implementation, contracts, dependencies or later review gates.
- Complete these startup steps in order for every concrete repository task, including a small edit or follow-up:
  1. Read the repository guidance and inspect Git worktree, branch and status metadata in the starting checkout. Keep this step to guidance and Git routing. Start task-file reads, searches, diffs and edits only after step 3, even when tools can run in parallel.
  2. Reuse the matching task worktree when one exists. Otherwise create one with `rtk git worktree add -b codex/<task-name> ..\cb-wt\<task-name> main`. Verify the selected checkout's absolute path, branch and current Git status from that checkout.
  3. Send a progress update with the selected path, branch and status baseline. Then read or search task files and continue all follow-up work from that checkout. A tool result or final handoff does not satisfy the progress update.
- One feature/task = one branch = one worktree. Do not edit application code, tests, CI or documentation in the main worktree unless the Owner explicitly requests that specific edit there. If work accidentally starts in main, stop edits, preserve all existing changes and transfer only the task's changes into its isolated worktree before continuing. Never reset, overwrite or move unrelated Owner changes.
- Many agents share the worktree, one session at a time. Close a session before opening the next.
- Handoff through a scoped commit when authorized, or an explicit uncommitted diff. Never commit unrelated work. The next agent verifies the recorded base and current diff instead of relying on chat history.
- An initial discussion or investigation may use an unnumbered task worktree. Coordinate spec numbers with the Owner before assigning a number or creating specs/NNN-name; reuse the existing task worktree. For the installed generic specify skill, provide `SPECIFY_FEATURE_DIRECTORY=specs/NNN-name` explicitly; branch names do not select spec directories. Do not run a second branch-creation script after creating the worktree.
- Use a fresh PowerShell window per worktree. Verify the active feature directory before each stage; the installed workflow writes the per-checkout pointer to ignored .specify/feature.json.
- Each worktree needs its own git-ignored root secrets/ and, once Compose exists, isolated service/volume names and ports. A git-ignored .env may hold non-secret local settings; runtime secrets come from secrets/. Never share mutable databases or volumes between concurrent features.
- Run graph refresh inside the worktree; graphs are per worktree.
- Only the Owner merges, from the main worktree. Finish 001 contracts before parallel features. At most 2-3 parallel worktrees.
- After merging a completed feature, verify its branch tip is in the destination branch and its worktree is clean. Preserve any needed local evidence or files outside the worktree, then run `rtk git worktree remove <feature-worktree-path>` from the main worktree. Verify removal with `rtk git worktree list`. If Git refuses because local files remain, report the blocker and leave the worktree in place.

## 5. Pipeline per feature

The Owner ratified constitution v1.0.0 on 2026-11-24. The prerequisite for the first feature spec is satisfied; feature scope and later review gates still apply. Planned folder and dependency boundaries are in [LAYOUT.md](LAYOUT.md).

| # | Stage | Role | Output | Gate | Commit |
| --- | --- | --- | --- | --- | --- |
| 0 | Setup | Owner | Worktree, branch, reserved number | G0 Owner confirms intent | - |
| 1 | Specify | Author | specs/NNN-name/spec.md + spec diagrams (section 11) | - | `spec(NNN): draft` |
| 2 | Clarify | Author + Owner | Answers in spec; diagrams updated | G1 Owner approves spec + diagrams | `spec(NNN): clarified` |
| 3 | Plan | Author | plan.md, contracts, data model + plan diagrams | - | `plan(NNN): draft` |
| 4 | Plan review | Plan Reviewer | Findings, incl. diagram vs text mismatches | G2 Owner approves plan + diagrams | `plan(NNN): approved` |
| 5 | Tasks | Tasker | tasks.md; every in-scope scenario ID mapped to a test | - | `tasks(NNN)` |
| 6 | Analyze | Analyzer | Consistency findings | Blocking findings fixed | `tasks(NNN): analyzed` |
| 7 | Implement | Implementer | One task group: tests + code | Focused tests pass | `feat(NNN): Txxx-Tyyy` |
| 8 | Code review | Code Reviewer | Findings on the group diff | Blocking findings fixed (back to 7) | `fix(NNN): review` |
| 9 | Repeat 7-8 | - | Next task group | - | - |
| 10 | Verify | Verifier | Full test run, graph refresh, converge report, diagrams match code | G3 Owner merges | merge |

Fixes found in review are applied by the Implementer (or Author for spec/plan) in a new session, never by the reviewer.

## 6. Session rules

1. Complete the ordered worktree startup in section 4 before substantive task discussion, investigation or edits. Use the selected checkout for task commands and preserve existing changes.
2. Load in order: AGENTS.md -> agents/README.md -> constitution.md -> specs/NNN-name/ -> the one skill for this stage. Never the whole catalog, graph or caches.
3. One stage (or one task group) per session; the prompt names where to stop.
4. End with the handoff report (section 8) and a commit or a diff for the Owner.

## 7. Stop rules

The agent stops and asks the Owner when:

- A rule or contract conflicts with the constitution or AGENTS.md (record as `[NEEDS CLARIFICATION]`; never guess).
- A test would need weakening, or a contract change, to pass.
- An action would destroy data/volumes, affect unrelated services, or provision cloud/deploy without explicit authorization. Routine scoped edits already authorized by the Owner do not need another approval. Real guest data remains prohibited.
- The same stage fails twice (escalate level or ask).

## 8. Handoff report

```text
Feature/stage: NNN-name / <stage> (tasks Txxx-Tyyy)
Harness/model/reasoning/level: Codex / <gpt-6.2-astra|gpt-6.2-sol> / medium / <L1|L2|L3>
Commit: <sha> (or: uncommitted diff for Owner)
Files changed: <list>
Checks run: <command> -> <actual result>
Not verified: <list>
Open questions / blocking findings: <list>
Next: <stage, role, level, Codex model, medium reasoning>
```

## 9. Definition of done and spec numbers

Done when every in-scope scenario ID in e2e/fixtures/scenarios.md has a test that ran and passed; pending in-scope tests block completion. Future out-of-scope IDs remain planned with an explicit reason. Completion also requires that no blocking review findings remain, verification evidence without model attribution is in the PR or review record, the graph is refreshed after source changes, and the diff has no secrets or real guest data.

| Number | Feature | Worktree | Status |
| --- | --- | --- | --- |
| 001 | Booking intake contracts and BookingDto | - | proposed |
| 002 | Local stand setup/teardown and ports | - | proposed |
| 003 | Kafka path | - | proposed |
| 004 | PDS simulator and registration states | - | proposed |
| 005 | Test result visualization (coverage matrix, timelines) | - | proposed |
| 006 | Layered testing and development verification | `..\cb-wt\layered-testing-verification` | verification complete; G3 pending |

Rows marked proposed describe sequencing, not created features or existing worktrees. Feature 006 was explicitly numbered by the Owner on 2026-11-25. Check existing specs and reservations before allocation.

## 10. Human in the loop

The Owner decides; agents propose. Every decision point has a fixed review packet and a fixed answer format, so the Owner never has to read chat history.

### Decision points

| Point | When | Owner receives (review packet) | Owner answers |
| --- | --- | --- | --- |
| G0 Intent | Before specify | One-paragraph goal, in/out of scope, affected hotels/brokers | approve / reframe |
| Clarify | During clarify | Max 5 questions, each with options, a recommended option and its consequence | pick option or free text |
| G1 Spec | After clarify | spec.md summary, spec diagrams with change legend, open items | approve / changes / reject |
| G2 Plan | After plan review | plan summary, plan diagrams, reviewer findings, new dependencies with reason | approve / changes / reject |
| Contract change | Any stage | Before/after diagram, affected integrations, test impact | approve / reject |
| Escalation | Stop rule hit (section 7) | What happened, options, recommended option | pick option |
| Blocking review | After code review | Findings with file:line; proposed fix per finding | fix / revise scope through spec / propose constitution amendment |
| G3 Merge | After verify | Verifier report, coverage matrix, timelines for failures, diagrams-match-code result | merge / back to stage N |

### Answer format

Owner replies in the session, or as a comment in the reviewed file:

```text
G1: changes
- spec.md FR-004: polling cursor must also store page token.
- diagrams/001-intake/flow.drawio: Cassis West arrow goes to cursor store first.
```

- approve: the agent commits with the gate in the message, e.g. `spec(NNN): clarified [G1 approved]`.
- changes: the agent applies only the listed changes in a new session, updates diagrams, and returns to the same gate.
- reject: the agent stops; the Owner restarts from the named stage.
- No answer means no approval. Existing explicit authorization remains valid within its scope; do not ask for the same approval again. New scope or materially changed contracts require a new decision.
- Blocking constitution violations cannot be waived as accepted risk. Resolve them or use the constitution amendment process.

### What agents must not decide alone

- Contract, identity key, freshness or PDS state changes.
- New dependencies, new services or new ports.
- Accepting a blocking finding, skipping a test or marking a scenario out of scope.
- Anything in section 7.

## 11. Diagrams for review

Diagrams explain proposed changes to the Owner before approval. Text in spec.md and plan.md stays authoritative; a mismatch between diagram and text is a blocking finding.

### Where and format

- Editable draw.io only, under `diagrams/NNN-name/`. Shared system diagrams stay in `diagrams/` (e.g. Booking_Data_Flow.drawio).
- Save uncompressed XML with stable, semantic cell IDs (e.g. `id="kafka-consumer"`), as in Booking_Data_Flow.drawio, so git diffs stay readable.
- One concept per page; name pages after what they show.
- Synthetic labels only: no guest names, DOB, passports or credentials in diagrams.
- Optional preview: if the draw.io desktop CLI is installed, export PNG next to the source for quick review; the .drawio file remains the source.

### Change legend (every diagram proposing a change)

| Meaning | Style |
| --- | --- |
| Unchanged | Default colours |
| Added | Green fill `#d5e8d4`, stroke `#82b366` |
| Changed | Amber fill `#fff2cc`, stroke `#d6b656`, label suffix `(changed)` |
| Removed | Red dashed stroke `#b85450`, label suffix `(removed)` |
| Open question | Purple note `#e1d5e7` with the `[NEEDS CLARIFICATION]` ID |

Changing an existing shared diagram: add a page `Before` (copy of current) and `After` (proposal) to the feature diagram, and update the shared diagram only after G2 approval.

### Which diagrams per stage

| Stage | Required diagrams | Shows the Owner |
| --- | --- | --- |
| Specify | `flow.drawio`: actors, intake paths, data flow for the feature | What changes for hotels, PMS and PDS |
| Specify (if states) | `states.drawio`: reservation, check-in or PDS status transitions | Which transitions are new or changed |
| Plan | `sequence.drawio`: intake -> broker -> DB transaction -> outbox -> ack order | Where durability, acks, retries and dead letters happen |
| Plan (if data) | `data.drawio`: entities, restricted storage boundary, identity key | Where guest data lives and where it must not go |
| Implement | Update only if code diverges from the approved plan; that divergence is an escalation | - |
| Verify | Check diagrams still match code; list mismatches | - |

### UML convention

Use UML notation for sequence, state, activity, class and component diagrams. Choose
the smallest view that explains the decision; do not draw every class or method.
The stage table above determines required artifacts. Add other views only when
they clarify an in-scope design decision or the Owner requests them. Routine fixes
and documentation edits do not require new diagrams unless they change a modeled
interaction, state, contract or boundary.

| Question | Diagram and notation |
| --- | --- |
| Who calls whom, and in what order? | Sequence: named participant lifelines, ordered messages, dashed return arrows, and `alt`, `opt` or `loop` fragments with guards where needed. Distinguish asynchronous sends from synchronous calls. |
| Which status transitions are allowed? | State machine: initial state, named states and transitions labeled `trigger [guard] / effect` as applicable. Use a final node only for an actual terminal state. Keep reservation, check-in and PDS lifecycles separate. |
| How does a process branch or run concurrently? | Activity: actions, guarded decision/merge nodes and fork/join bars; use swimlanes when responsibility matters. |
| Which domain types relate to each other? | Class: relevant types, associations, multiplicities and relationship names. Use inheritance or composition only when the model requires those semantics. |
| Which projects or services own responsibilities? | Component: named components, provided/required interfaces and directed dependencies. Label external systems and storage boundaries. |

`flow.drawio` may remain a labeled data-flow overview, and `data.drawio` may use
entity-relationship notation for storage. Identify the notation in the page title
or legend; do not label a generic box-and-arrow view as UML. Keep different views
on separate named pages. The existing change colours describe review status;
arrowheads, line styles and labels must still convey the chosen notation without
depending on colour alone.

### Content and maintenance

- Read the relevant spec, plan, contracts and existing diagrams before drawing. During implementation, verify names and interactions against source. Mark proposals and unresolved details explicitly; a diagram does not approve a contract or make a proposed component exist.
- Use the same names as the authoritative artifacts. Link each feature diagram from its spec or plan, state its scope and design status, and include relevant scenario IDs for failure or recovery paths.
- For durability sequences, show transaction boundaries, durable acceptance, broker confirmation, offset commit, retries and durable dead-letter handoff where in scope. Show uncertain PDS outcomes and reconciliation when relevant; never imply exactly-once delivery.
- Update affected diagrams in the same change as an authorized design change. If implementation would diverge from an approved contract or plan, follow the existing escalation and approval gates before changing that design. Avoid unrelated layout or cell-ID churn.

### Verification and handoff

Before handoff, parse each changed `.drawio` file as XML and check that cell IDs
are unique within each page and edge endpoints reference existing cells. Inspect
the rendered pages for readable labels, visible arrowheads, overlaps and clipped
content using an available draw.io viewer or export tool. XML validation alone
does not verify UML semantics or visual layout. If rendering is unavailable,
report visual inspection as unverified.

Compare each changed view with the authoritative text and, when available, the
implementation. Report diagram paths/pages, checks actually run, mismatches and
unverified behavior in the review packet. A text/diagram mismatch remains a
blocking finding. Do not add a dependency or install a drawing tool merely to
create a preview; report the tooling limitation.

## 12. Scenario IDs and test visualization

### Scenario IDs

- e2e/fixtures/scenarios.md is the catalog of stable scenario IDs (INT, H01, SAL, CAS, BRK, KFK, PDS). It is adopted in feature 001.
- Specs reference IDs in acceptance scenarios; tasks name the IDs each test covers; tests carry scenario metadata supported by the selected test framework; fixtures are named or tagged with their IDs.
- BRK IDs are broker-agnostic and reused by forks; only KFK IDs are Kafka-specific.
- Adding or retiring an ID is a spec change (section 10, contract change point). Select the test framework and metadata-to-results mapping in the plan; an xUnit Trait is an example, not a selected dependency.

### Visualizations

TRX output alone is not assumed to preserve scenario tags. Verify the selected adapter or generate a sidecar mapping from test IDs to scenario IDs. Missing or skipped results are pending, never passed.

Generated from data, never hand-drawn; the catalog, fixtures and test output are the sources. draw.io (section 11) remains for design diagrams only.

| Visual | Source | Shows | Used at |
| --- | --- | --- | --- |
| Coverage matrix | scenarios.md + test results + explicit test-to-scenario mapping | Scenario ID x intake path (H01, SAL, CAS; `all` for BRK, KFK, PDS) -> passed / failed / pending / n/a | G3 packet, converge |
| Scenario timeline | Fixture files | Per event: updatedAt, receivedAt, arrival order, expected booking/outbox/PDS effect | Review of INT, CAS, BRK, KFK and PDS fixtures and failures |
| Failure detail | Test output | Expected vs actual state transitions for each failed ID | G3 packet, blocking review |

Rules:

- Output goes to git-ignored `/test-results/`; never commit generated reports.
- Show IDs, states and timestamps only; no guest identity fields.
- A pending or n/a cell needs a reason; an ID missing from the matrix is a blocking finding.
- Until feature 005 exists, the Verifier gives the matrix as a Markdown table in the handoff report.

## 13. Prompt templates

Author (L1, GPT 6.2 Astra, medium reasoning):

```text
Read AGENTS.md, agents/README.md, agents/WORKFLOW.md and .specify/memory/constitution.md.
Role: Author. Stage: <specify|clarify|plan> for feature NNN-name.
Load agents/skills/speckit-<stage>/SKILL.md and run only this stage: <input>.
Create or update the diagrams required for this stage in diagrams/NNN-name/ per WORKFLOW.md section 11, using the change legend.
Do not start the next stage or write application code. Commit as <message>, then give the review packet for the next gate (section 10) and the handoff report.
```

Reviewer / Analyzer (L1, GPT 6.2 Astra, medium reasoning; separate read-only session):

```text
Read AGENTS.md, agents/WORKFLOW.md, the constitution and specs/NNN-name/.
Role: <Plan Reviewer|Analyzer|Code Reviewer>. Review <spec+plan | spec/plan/tasks | diff <base>..HEAD>.
Do not edit files. List findings as blocking/non-blocking with file:line and the rule violated, then the handoff report.
```

Implementer (L2, GPT 6.2 Sol, medium reasoning):

```text
Read AGENTS.md, agents/WORKFLOW.md, the constitution and specs/NNN-name/.
Role: Implementer. Load agents/skills/speckit-implement/SKILL.md and implement only tasks Txxx-Tyyy.
Write tests before or alongside implementation. Do not change contracts or the spec. Run focused tests, commit as feat(NNN): Txxx-Tyyy, give the handoff report.
```

Verifier (L3 Sol medium runs; separate L1 Astra medium session judges):

```text
Read AGENTS.md and agents/WORKFLOW.md. Role: Verifier for feature NNN-name. Do not fix code.
Run the available feature test suite with the selected results adapter; report if no runnable suite exists. Refresh the graph only after source changes using ./agents/hooks/refresh-graph.ps1. Build the coverage matrix from e2e/fixtures/scenarios.md (section 12); then load agents/skills/speckit-converge/SKILL.md.
Report actual results, pending scenarios and unverified behavior in the handoff report.
```
