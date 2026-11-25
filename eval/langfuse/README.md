# Checkin agent evaluations in Langfuse

Start Docker Desktop with Linux containers, then run from any Checkin worktree with PowerShell 7.2+:

```powershell
pwsh -File eval/langfuse/stack.ps1 start
```

Open [the local UI](http://localhost:3300) and sign in as `owner@checkin.test`. Get the local owner's password from `OWNER_PASSWORD` in the main checkout's ignored `secrets/langfuse.env`. The first start creates this file and initializes the **Checkin Agent Quality** project. Keep the file private and back it up with the data. Subsequent starts reuse it.

## Storage and lifecycle

By default the host folder is a sibling of the main checkout, named `<main-checkout-name>_Langfuse`. For the Checkin_Booking checkout this is `../Checkin_Booking_Langfuse`. To use another disk, set an absolute path before the first start and keep using that same setting:

```powershell
$env:LANGFUSE_DATA_DIR = 'E:\AgentData\Checkin_Booking_Langfuse'
```

The host folder holds PostgreSQL metadata, ClickHouse logs, MinIO events and media, and the Redis ingestion queue. ClickHouse stores live traces and scores in the Docker-managed volume `checkin-langfuse_clickhouse-data`. This gives ClickHouse Linux filesystem semantics for renaming data directories. All worktrees of this clone use the same Compose project. Each teammate's local deployment has its own history. Ports 3300 (Langfuse) and 3390 (object storage) are loopback-only.

`stack.ps1 status` lists services. `stack.ps1 stop` stops them and preserves data. Use `stack.ps1 start` to resume. For a complete backup, stop the stack, back up the host folder, export the ClickHouse volume, and preserve the matching credentials privately. On a different machine restore all three before starting.

## Verify a synthetic score

Run after the UI is ready. Recreation briefly interrupts this evaluation stack.

```powershell
$run = [guid]::NewGuid().ToString()
python eval/langfuse/check_score.py write $run
pwsh -File eval/langfuse/stack.ps1 recreate
python eval/langfuse/check_score.py read $run
```

The first command submits one tagged synthetic task trace and a numeric score, then queries the score through the local API. The final command reads the same score without submitting it again. Exclude the `integration-smoke` tag and `smoke_task_success` metric from feature quality reports.

For reviewed feature work, attach the feature, task group, scenario IDs, revision and first-handoff or verified checkpoint to task evidence. Keep credentials, restricted registration fields and private transcripts outside evaluation payloads. This deployment provides the storage and API for explicit evaluations. Enabling full-session capture is a separate configuration decision.

[Upstream Compose for Langfuse 4.86.0](https://github.com/langfuse/langfuse/blob/v4.86.0/docker-compose.yml)

## Trace a Checkin session

The opt-in Codex Stop hook sends completed session turns, including prompts and tool output, to the local project. Use synthetic sessions suitable for capture. The tested exporter is `tracing@codex-observability-plugin` version 0.4.3 with Node.js 22 or newer.

1. Install the marketplace with `codex plugin marketplace add langfuse/codex-observability-plugin`, then run `codex plugin add tracing@codex-observability-plugin`. Confirm the installed version matches the wrapper.
2. From the main checkout, copy [the wrapper](../../agents/hooks/langfuse-checkin-hook.cjs) to `secrets/langfuse-checkin-hook.cjs` beside `langfuse.env`. All opted-in worktrees can reference this same private copy.
3. For each opted-in worktree, add a command to its local `.codex/hooks.json` under `hooks.Stop[].hooks[]`. Use `type` of `command`, a timeout of 30 seconds, and the command `node "<absolute path to main checkout>/secrets/langfuse-checkin-hook.cjs"`. Preserve existing hook entries. Keep the machine-specific hook file local using the repository's private Git exclude file.
4. Open Codex in that worktree, run `/hooks`, and trust the project Stop hook after reviewing the command. Start a fresh session, complete a short turn and check [Checkin traces](http://localhost:3300/project/checkin-agent-quality/traces) for the matching session ID. A trace records activity. Quality scores require separate evaluations.
