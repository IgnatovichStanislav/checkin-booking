# .NET skills source

- Repository: https://github.com/dotnet/skills
- Commit: `9d2962064a5387978ad1fcdcba45f31a18fb10d1`
- Imported: 2026-11-24
- License: MIT; see [LICENSE](LICENSE).

Complete skill directories are preserved unchanged under `agents/skills/`.
Source paths are `plugins/<plugin>/skills/<skill>/`:

| Plugin | Skills |
| --- | --- |
| dotnet | csharp-expert, csharp-refactoring, msbuild, setup-local-sdk |
| dotnet-aspnetcore | dotnet-webapi |
| dotnet-data | create-datadriven-aspnetcore, optimizing-ef-core-queries |
| dotnet-test | run-tests, scaffold-dotnet-test-project, test-tagging, code-testing |

Updates must preserve complete directories and record the new source commit.
Copilot agents, host LSP configuration and MCP server setup are not included.
