# Checkin agent evaluations in Langfuse

Start Docker Desktop with Linux containers, then run from any Checkin worktree with PowerShell 7.2+:

```powershell
pwsh -File eval/langfuse/stack.ps1 start
```

Open [the local UI](http://localhost:3300) and sign in as `owner@checkin.test`. Get the local owner's password from `OWNER_PASSWORD` in the main checkout's ignored `secrets/langfuse.env`. The first start creates this file and initializes the **Checkin Agent Quality** project. Keep the file private and back it up with the data. Subsequent starts reuse it.

## Storage and lifecycle

From the repository root, run `pwsh -File eval/langfuse/dashboard.ps1` to start Langfuse if needed and print the dashboard and score analytics links. Add `-Open` to open score analytics in your browser. You can also ask a project agent, “Give me the Checkin Langfuse dashboard and score analytics links.”

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
