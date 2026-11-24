# Workflow tooling smoke check — 2026-11-24

Scope: verify local tooling and references before feature 001. This is a tooling smoke
check, not a behavioral agent evaluation, application test run or constitution ratification.
Baseline HEAD: `7de6ae9`; the checkout already contained uncommitted guidance changes.

## Observed results

| Check | Actual result |
| --- | --- |
| `specify --version` | 1.4.4 |
| `specify integration status` | OK; generic default/installed integration, shared templates aligned; 0 modified/missing managed files, 0 invalid manifest paths, 0 unchecked manifests |
| `./.specify/scripts/powershell/resolve-template.ps1 constitution-template -Json` | Succeeded; active constitution template resolved |
| Constitution extension hooks | `.specify/extensions.yml` absent; no hooks registered there |
| Skill routing table | All 21 skills named in agents/README.md have a SKILL.md |
| Evaluation inventory | All 21 targets resolve; all 32 non-null skill/runbook paths exist; statuses remain not-run |
| `./agents/hooks/refresh-graph.ps1 -Check` | Succeeded; Graphify executable and workspace found |
| `graphify query 'refresh-graph.ps1' --budget 700` | Exit 0; found the current agents/hooks/refresh-graph.ps1 source; output explicitly truncated at the requested budget |
| Existing graph JSON | Parsed successfully: 435 nodes, 474 edges; all 41 distinct source_file paths exist |
| Feature path resolution without a selection | Reported missing feature context, as expected before a feature is selected |
| Feature path resolution with temporary explicit selection | `check-prerequisites.ps1 -PathsOnly -Json` succeeded with process-local SPECIFY_FEATURE_DIRECTORY=specs/001-booking-intake-contracts and SPECIFY_FEATURE_NO_PERSIST=1; no feature directory or .specify/feature.json was created |
| Ignore rules | `git check-ignore --no-index` matched synthetic probe paths under secrets/, test-results/, eval/results/ and graphify-out/; probe files were not created |

The feature path is a resolution probe, not a number reservation or created feature.
The graph query was verified against the actual refresh-hook source. Existing source
paths do not prove complete graph coverage, correct relationships or parser health.
No extraction was run for these documentation changes.

## Limits and next gate

The bullets below record the status at smoke-check time. Subsequent Owner decision on
2026-11-24: constitution v1.0.0 ratified; its pre-spec ratification prerequisite is satisfied.

- Behavioral Docker/Spec Kit/Graphify evaluations remain not run; target statuses are unchanged.
- No application projects or runnable application suite exist yet; no booking tests, builds or services were run.
- Native slash-command discovery, automatic hooks, model switching and agent handoffs were not exercised. The generic integration alone does not establish those capabilities.
- The constitution remains draft v1.0.0, pending explicit Owner ratification before the first feature spec.
- Folder conventions are documented in agents/LAYOUT.md; no application or infrastructure folders were scaffolded.
