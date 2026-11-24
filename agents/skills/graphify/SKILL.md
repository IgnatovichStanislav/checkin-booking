---
name: workspace-graphify
description: Use for Checkin_Booking codebase questions, architecture, file relationships, graph exploration, and graph refresh after source changes. Prefer the existing shared graph for navigation.
---

# Workspace Graphify

Adapted on 2026-11-24 from [Graphify v8 Codex skill](https://github.com/Graphify-Labs/graphify/blob/v8/tools/skillgen/expected/graphify__skill-codex.md). This is a workspace-specific adaptation, not a complete upstream installation.

## Workspace contract

Run from `D:\Development\Checkin_Booking`, or the equivalent root in another checkout. Use one `graphify-out/graph.json` across Kafka, shared booking logic, mappers, exports and the React monitoring UI.

The root AGENTS.md routes agents here through agents/README.md. Keep all harness files under agents/. Do not create .codex/ or .claude/, run platform installers, or register background watchers as part of ordinary usage. The refresh script is explicitly invoked, not an automatic lifecycle hook.

Use the installed Windows executable via `Get-Command graphify`. Do not copy Bash heredocs, shebang detection, or Linux installation commands into PowerShell. Verify available flags with `graphify --help` before unfamiliar operations. Slash commands in upstream documentation describe agent workflows; they are not literal PowerShell commands.

## Upstream workflow principles

- For questions with an existing graph, query immediately instead of rebuilding. Use graph vocabulary to refine empty results, and cite source locations.
- Use path for connections and explain for individual concepts. Preserve uncertainty; never manufacture relationships.
- Separate deterministic code parsing from semantic document analysis. Code parsing needs no model key.
- For broader extraction, inspect corpus size and exclusions first. Validate semantic results and actual file coverage before merging; failed chunks must not silently become complete work.
- Preserve provenance, caches and manifest consistency. Do not overwrite a useful graph with empty output or bypass shrink protection without investigating.
- Surface parser and graph-integrity warnings. Report actual outputs and measured token usage; unavailable usage is unknown, not zero.
- Clustering, updates and querying are distinct operations. Optional exports and watchers should follow the requested scope. Warn before visualizing more than 5,000 nodes.

## Local commands

Run from the workspace root:

```powershell
# Read-only navigation:
graphify query 'BookingUpdate' --budget 1500
graphify explain 'BookingUpdate'
graphify path 'Program.cs' 'BookingUpdate'

# Check prerequisites without rebuilding:
./agents/hooks/refresh-graph.ps1 -Check

# Refresh the local code-only baseline after source changes:
./agents/hooks/refresh-graph.ps1
```

The helper currently invokes `graphify extract . --code-only --no-cluster --max-workers 1`. It is a code-only extraction workflow, not the full upstream semantic/clustering/report pipeline. Documentation-only edits do not require it. If semantic content is added later, review this helper before reuse so a code-only run cannot discard that work.

Read relevant source before editing. If the graph is absent, stale, partially parsed, or cannot answer the question, use direct searches and explain the limitation. Do not change application code solely to accommodate Graphify's parser.

## Expanded analysis, when requested

Keep routine work local and code-only. If the user requests documentation semantics, a full analysis, clustering or visual exports, consult the upstream workflow and its linked references for that operation and verify compatibility with the installed CLI. These references are remote, not bundled beside this file; fetch and read them before using their procedures. Do not claim this adaptation implements those pipelines.

Use only capabilities available in the current session. Do not change user-level settings or require a particular agent vendor to follow an upstream example. A skill-maintenance task is not a request to run semantic extraction or launch subagents. No external model calls are part of the default refresh.

## Known baseline and completion checks

Initialization produced 56 nodes and 58 edges; counts are historical, not assertions for later builds. Program.cs was partially parsed near line 24, so relationships may be missing. No community report or semantic coverage was established.

After a requested rebuild, check the command exit code, parse graph.json and verify a representative query. Report the real graph path, counts and limitations. Only claim HTML, reports or other exports when those files were actually generated and inspected. Application builds/tests remain separate from graph verification.
