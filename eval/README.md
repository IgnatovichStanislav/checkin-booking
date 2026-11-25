# Agent and skill evaluation

This folder indexes evaluation targets in the shared agents harness. Skills remain in agents/skills; do not duplicate them here. No executable agent definitions have been created: Graphify and Spec Kit are workflows/skills, not configured Docker Agent runtimes.

## Run manually

For local task scores and traces, see the [Langfuse deployment and persistence check](langfuse/README.md).

1. Pick a target in targets.json and open its skill and runbook, if provided.
2. Use a disposable test workspace and synthetic fixtures. Load the relevant project guidance and skill into the agent being evaluated.
3. Run one runbook prompt and inspect its expected behaviors, must-not criteria and verification commands. Review commands for the selected Windows shell before running them.
4. Record the target, source commit, agent/model, prompt, actual transcript, checks, pass/fail/blocked result and limitations. Never record credentials or real guest data.

The Docker prompts and verification assets are imported unchanged inside each skill's evals/ and checks/ directories. They are not automatically executed. Installing them does not prove agent behavior. Docker's upstream task eval is static asset checking; live judge evaluations are a separate workflow requiring agent CLIs and a model provider. No judge runner, credentials or external-model execution is configured here.

## Project harness checks

- Graphify: query a real current file; report missing or stale coverage; never invent graph relationships. Rebuild only when appropriate and requested by project guidance.
- Spec Kit: select the requested stage, preserve user decisions, and check against constitution v1.0.0, ratified by the Owner on 2026-11-24. Tooling checks do not replace Owner approval. Do not start implementation during a specification-only request.
- All targets: use agents/skills and agents/hooks, avoid project .codex/.claude folders, and do not recreate deleted broker/schema folders without a request. Follow the booking-to-PDS test stand scope and UTC timestamp rules in AGENTS.md.

Run `python eval/check_index.py` to check both directions of skill routing and all registered target paths. Run `python -m unittest discover -s eval -p test_check_index.py` for six disposable inventory regression cases. The skill-index GitHub workflow runs both commands when the index, workflow guidance, skills or evals change.

Start a fresh session per case in its disposable setup. Give the agent only the Prompt section and the fixture location. Capture the command and file-read trace and the final answer, then grade against the case's Grading section. Record skipped or blocked runs separately.

The [new-task](cases/worktree-new-task.md) and [follow-up](cases/worktree-follow-up.md) cases grade worktree behavior independently of skill routing. Compare worktree paths, refs, Git status and file hashes before and after each session. Static CI validates registration and inventory only. Retain the behavioral trace and private run metadata under ignored eval/results/.

[Skill review results](Skill_review_2026-11-24.md) record the first three behavioral runs. The [original target results](Original_targets_2026-11-24.md) record one behavioral case per original target, with live verification limits.

Latest tooling smoke check: [2026-11-24](Workflow_smoke_2026-11-24.md).
