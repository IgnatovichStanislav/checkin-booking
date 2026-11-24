# Skill routing

Read only the skill needed for the task. Paths below are relative to agents/; every skill entry resolves to skills/<name>/SKILL.md. Root AGENTS.md governs the shared harness. This index does not register plugins or execute hooks.

| Task | Skill name |
| --- | --- |
| Code navigation / graph refresh | graphify |
| Project principles | speckit-constitution |
| Feature requirements | speckit-specify |
| Clarify requirements | speckit-clarify |
| Implementation plan | speckit-plan |
| Task breakdown | speckit-tasks |
| Consistency review | speckit-analyze |
| Requirements checklist | speckit-checklist |
| Implement requested tasks | speckit-implement |
| Assess remaining gaps | speckit-converge |
| Initial Docker setup | docker-project-foundations |
| Dockerfiles / builds | docker-build-strategies |
| Compose stacks | docker-compose-patterns |
| Destructive Docker operations | docker-destructive-guardrails |
| Official Docker docs | docker-docs (experimental) |
| Docker Agent config / run / distribution | docker-agent-config / docker-agent-run / docker-agent-deploy |
| Sandbox lifecycle / network and credentials / kits | docker-sandboxes-lifecycle / docker-sandboxes-network-credentials / docker-sandboxes-kits (kits experimental) |

For combined Docker tasks: foundations -> builds -> Compose; agent-config -> agent-run -> agent-deploy; sandbox lifecycle -> network/credentials -> kits. Load only relevant steps.

Keep Spec Kit generated skills managed; use supported customization. Verify with specify integration status. Docker source pin/license: sources/docker-skills/SOURCE.md and LICENSE. Manual copies require reviewed updates of complete folders.

Hook: hooks/refresh-graph.ps1 (-Check for prerequisites); run after source changes per root guidance, not documentation edits. No automatic watcher or vendor-specific project configuration.

Evaluation: ../eval/README.md and ../eval/targets.json. These index skill/workflow targets, not configured agent runtimes. Register new skills here without copying their full instructions.
