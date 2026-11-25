# Development verification contract

This contract applies to local/CI development tooling. It changes no booking API, event or scenario contract.

## Entry points

- agents/hooks/test-backend.ps1 [-Configuration Debug|Release]: existing public shape retained; default Release. Uses RTK when available for supported commands and native dotnet otherwise.
- agents/hooks/check-backend-runner.ps1: run disposable orchestration/privacy checks; no services or global configuration changes.
- python eval/check_index.py: validate the current skill inventory.
- python -m unittest discover -s eval -p test_check_index.py: validate inventory checker regressions with disposable fixtures.
- Existing --scenario-evidence <fresh-suite-directory> executable mode remains supported. Parent invocation adds explicit --run-context <parent-run-json-path> for source/run binding; direct legacy mode validates its own source/run context rather than fabricating a parent pass.

The implementation must document any final argument parsing change and test invalid/missing/duplicate arguments. No generic broker interface is introduced.

## Output locations

Each runner invocation owns test-results/unit-contracts-<uuid>/:

- run.json: parent lifecycle/source record, created before restore/build.
- suite/: fresh directory owned by the executable; local TRX intermediate, scenario map/results and execution receipt.
- export/: explicitly constructed safe records only, containing run metadata, execution receipt, scenario metadata and verdicts with source/run binding.

The runner returns the exact current export location to CI via a fixed GitHub output when GITHUB_OUTPUT exists; otherwise prints its local location. Never enumerate old directories to select an artifact. Validate all emitted relative paths remain under the owned root.

## Exit and phase rules

Native restore/build failures retain their observed nonzero codes; preflight/structural evidence failures use 1. Platform test exits are recorded separately; incomplete required results force failure even if platformExitCode is zero. A failing phase records safe code/timestamps and later phases stay notRun. A missing receipt or partial run cannot pass.

Do not print raw test output to CI or upload it. Show phase names, safe failure codes, method/scenario IDs, counts and local diagnostic paths only. New assertions must compare opaque IDs/states with safe messages, never dump restricted input fixtures.

## Export rules

Allowlist documented fields from run/receipt/map/results. Exclude raw TRX, arbitrary file attachments, assertion messages, stdout/stderr and environment values. No wildcard over historical run roots. Upload available safe metadata after a failure; when export is unavailable, publish a fixed safe summary of that phase failure rather than a success.

Tests inject a distinctive synthetic secret/identity sentinel into candidate failure details and verify it appears in neither exported files nor safe console summaries. Check both success and failure paths.

## Manual eval contract

Two registered targets have separate cases: worktree-new-task and worktree-follow-up. Both use a disposable Git repository, copied relevant project rules, writing capability, unrelated dirty sentinels and before/after status/hash checks.

The evaluated agent receives only its prompt and ordinary project context; grading/target expectations are withheld. Capture successful rule/skill reads, worktree commands, working directories and edits. Grade command order, correct task isolation/reuse and unchanged unrelated bytes. Record unavailable capabilities separately; do not convert missing traces into pass.

## Review packet

Include source IDs, actual commands/outcomes, safe report locations, a complete catalog/path matrix with proof levels and reasons, behavioral case grades/limits, diagram consistency and unresolved findings. This is existing workflow evidence, not feature 005 visualization.

