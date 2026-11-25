# Skill routing

Resolve a task against this table first. Open `skills/<name>/SKILL.md` relative to this file. Pick the closest match, then read references only as the task requires. For an EF Core query review, go directly to `optimizing-ef-core-queries`. Use `csharp-expert` only for general C# questions without a specialist match. Look up any specialist it names here before considering an external install.

| Task | Skill name |
| --- | --- |
| Code navigation / graph refresh | graphify |
| Open the local Langfuse dashboard or give me the score analytics link | langfuse-dashboard |
| General C# fallback | csharp-expert |
| C# refactoring | csharp-refactoring |
| .NET build diagnosis | msbuild |
| Local .NET SDK setup | setup-local-sdk |
| ASP.NET Core APIs | dotnet-webapi |
| EF Core CRUD scaffolding | create-datadriven-aspnetcore |
| EF Core query optimization | optimizing-ef-core-queries |
| .NET test execution | run-tests |
| .NET test project setup | scaffold-dotnet-test-project |
| Test scenario tagging | test-tagging |
| Kafka / .NET idempotency review | kafka-idempotency |
| Kafka local setup, partitions, consumer groups and lag | kafka |
| PostgreSQL locks, indexes and query plans | postgres-pro |
| Schema design, constraints and transactional SQL | sql-pro |
| Redis data structures, expiration and disposable cache | redis-core |
| React components and TypeScript types | react-expert / typescript-pro |
| Frontend build and development server | vite |
| Routes and navigation | react-router |
| HTTP requests and server state | axios / tanstack-query |
| Form state and input validation | react-hook-form / zod |
| Utility CSS and UI components | tailwindcss / shadcn |
| Component tests and user interactions | vitest / testing-library |
| Browser end-to-end tests | playwright-expert |
| Frontend linting and formatting | eslint / prettier |
| Author or strengthen .NET tests | code-testing |
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

For staged work, see [WORKFLOW.md](WORKFLOW.md). For planned project boundaries, see [LAYOUT.md](LAYOUT.md). Use only the requested Spec Kit stage. For Docker, choose the row for the current operation.

The repository owns the Kafka idempotency review. Imported skill revisions and licenses are under [sources/](sources/). Preserve complete upstream directories when updating. This index supports lookup by file path. Native plugin registration is separate.

Graph refresh and evaluation instructions are in the [root guidance](../AGENTS.md) and [evaluation guide](../eval/README.md).
