# Contributing

This repository is a shared learning test stand for PMS booking intake and fictional PDS registration. Application implementation is currently paused. The committed guidance, skills, templates and diagrams are the starting point for contributors.

## Start here

1. Clone the repository and read [AGENTS.md](AGENTS.md) for business rules and scope.
2. Read [agents/README.md](agents/README.md) to select the skill for your task. The 21 shared skills live in agents/skills/: Graphify, nine Spec Kit workflows and eleven Docker skills.
3. Ask your coding agent to read AGENTS.md and the selected SKILL.md explicitly. The custom agents/ folder does not automatically register skills or slash commands in every client. No project .codex/ or .claude/ setup is required.
4. Create a feature branch and state the intended change, acceptance criteria and files in scope before editing. Keep unrelated work intact.
5. Create an empty local secrets/ folder if needed. Obtain development credentials separately; never copy another contributor's keys.

Example task prompt:

> Read AGENTS.md and agents/skills/speckit-specify/SKILL.md. Specify the agreed booking intake feature and its acceptance scenarios. Record unresolved decisions. Do not implement the service or start containers.

## Shared workflow

- Establish agreed principles with the constitution workflow before the first feature specification; the current constitution is a template, not adopted policy.
- Use the requested Spec Kit stage: specify, clarify, plan, tasks, then implementation when authorized. Review requirements and acceptance scenarios before moving to code.
- Load only relevant skills and references. Use targeted searches and bounded output; do not send the entire repository or generated graph into an agent's context.
- Treat Graphify as navigation assistance. Verify findings against source. With its CLI installed, run ./agents/hooks/refresh-graph.ps1 -Check for prerequisites and run the script without -Check after source changes. Documentation-only edits need no refresh.
- Hooks are explicit scripts, not automatic background actions. Review scripts and imported skill instructions before executing them; installing a skill does not authorize deployment or destructive commands.
- Keep imported skills intact and preserve their source pins/licenses. Review complete upstream updates. Docker provenance is in agents/sources/docker-skills/.
- Run checks appropriate to the change. Report commands, actual results and anything unverified. The eval/ runbooks describe manual checks; their presence does not mean they passed.
- Review every diff before committing. Contributors own the correctness and security of changes produced with their tools. Use small commits and pull requests with the behavior change and verification results; never add AI attribution or session links.

## Local tools

Git is enough to read and edit the shared setup. Install optional CLIs only for the workflow being used: Specify for Spec Kit, Graphify for graph generation and RTK for compact output. Shared skill files do not install these binaries. The setup was generated with Specify 1.4.4; check compatibility before refreshing managed files and review the resulting diff. Use specify integration status to inspect the generic integration.

The planned application uses .NET, PostgreSQL, Redis and Docker Desktop, with Blazor monitoring. SDK versions, ports and startup commands will be documented with the implementation. There is currently no application build or test command. Do not run initialization with --force over a clone just to use the checked-in skills.

## What belongs in Git

Commit reusable source, specifications, synthetic fixtures, editable diagrams, skills, hooks, templates and setup documentation. Keep configuration examples free of credentials. Git does not preserve empty directories.

Keep secrets/, .env files, production guest records, identity documents, database dumps, private logs and agent session transcripts out of commits and agent prompts. Use only synthetic fixtures in e2e/fixtures/. Review new fixtures for accidental personal data.

Generated graphify-out/, evaluation results, build output, caches and machine-local Spec Kit state stay ignored. Recreate these locally instead of publishing them. The root .dockerignore excludes secrets from the root build context; any future alternate build context needs equivalent protection. Ignore rules are not encryption, access controls or a secret scanner.

Before publishing, inspect git status, the staged diff and the staged file list. Check suspicious files without printing credentials into logs. If a real credential is exposed, revoke/rotate it and report the incident; deleting it in a later commit does not remove it from history.
