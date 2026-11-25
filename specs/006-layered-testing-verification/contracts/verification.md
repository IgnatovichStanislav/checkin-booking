# Development verification contract

This contract applies to local/CI development tooling. It changes no booking API, event or scenario contract.

## Entry points

- agents/hooks/test-backend.ps1 [-Configuration Debug|Release]: existing public shape retained; default Release. Uses RTK when available for supported commands and native dotnet otherwise.
- agents/hooks/check-backend-runner.ps1: run disposable orchestration/privacy checks; no services or global configuration changes.
- agents/hooks/check-attachment-policy.ps1: inspect owned built PE metadata without loading it, returning fixed safe policy verdicts. The full-check parent invokes it after build and before the test executable; output binds to the checked artifact digest. It does not run tests or claim source verification by itself.
- python eval/check_index.py: validate the current skill inventory.
- python -m unittest discover -s eval -p test_check_index.py: validate inventory checker regressions with disposable fixtures.
- Existing --scenario-evidence <fresh-suite-directory> executable mode remains supported. Parent invocation adds explicit --run-context <parent-run-json-path> for source/build binding. Direct legacy mode is explicitly source-unverified and verificationEligible=false; it may report actual test outcomes but cannot satisfy the full verification entry point or G3 source-bound proof. The exact source/build binding policy is in data-model.md.

The implementation must document any final argument parsing change and test invalid/missing/duplicate arguments. No generic broker interface is introduced.

## Output locations

Each runner invocation owns test-results/unit-contracts-<uuid>/:

- run.json: parent lifecycle/source record, created before restore/build.
- suite/: fresh directory owned by the executable; minimal safe TRX-shaped XML produced from observed platform events, scenario map/results and execution receipt. No stock/raw TRX or attachments are generated in full-check mode.
- export/: explicitly constructed safe records only, containing run metadata, execution receipt, scenario metadata and verdicts with source/run binding.

The runner returns the exact current export location to CI via a fixed GitHub output when GITHUB_OUTPUT exists; otherwise prints its local location. Never enumerate old directories to select an artifact. Validate all emitted relative paths remain under the owned root.

## Exit and phase rules

Native restore/build failures retain their observed nonzero codes; preflight/structural evidence failures use 1. Platform test exits are separate; incomplete required results force failure even if platformExitCode is zero. Preflight/restore/build failure stops later execution, while manifest finalization/export is still attempted. A returned test failure still runs evidence joining and may export valid failed results. Evidence failure allows only safe manifest/receipt export. Export failure fails the overall run without changing prior outcomes. Missing receipt/partial execution cannot pass. Tests must exercise each combination, including valid evidence from failed tests and failed evidence after passing tests; hosted validation must cover early failure and test failure as distinct paths.

The approved post-build policy phase runs before executable load. Reject AddResultFile references and unsupported/unresolved execution paths/module initializers, returning a fixed reason with test notRun and platform exit unset. Direct legacy evidence remains policyVerified=false and verificationEligible=false, regardless of syntactically valid source hashes. A passed policy marker must bind to the exact executed artifacts; it does not replace source/build verification.

Do not print or persist raw test output. Drain/discard child stdout/stderr in memory; show phase names, safe codes, known method/scenario IDs and counts only. The installed-platform IDataConsumer captures terminal TestNodeUpdateMessage identities/states into a minimal safe XML report; stock TRX remains disabled. Never inspect failure exceptions/explanations, arbitrary metadata, DisplayName or attachments. New assertions must compare opaque IDs/states with safe messages, never dump restricted input fixtures. Missing/ambiguous identity and mismatched real platform exit cannot pass evidence.

## Export rules

Allowlist documented fields from run/receipt/map/results. Exclude raw TRX, arbitrary file attachments, assertion messages, stdout/stderr and environment values. No wildcard over historical run roots. Upload available safe metadata after a failure; when export is unavailable, publish a fixed safe summary of that phase failure rather than a success.

Tests inject a distinctive synthetic secret/identity sentinel into actual failing assertion/stdout/metadata candidates and verify it appears in no retained generated file, local/hosted summary/log or artifact. Attachment calls are prohibited: disposable compiled attachment canaries outside normal scanned modules must fail policy before their test bodies execute, with no platform exit or staged file. Cover success, failure and interruption/no-report paths. No temporary raw diagnostic file is permitted: deleting it afterward cannot guarantee privacy if the process is killed.

## Manual eval contract

Two registered targets have separate cases: worktree-new-task and worktree-follow-up. Both use a disposable Git repository, copied relevant project rules, writing capability, unrelated dirty sentinels and before/after status/hash checks.

The evaluated agent receives only its prompt and ordinary project context; grading/target expectations are withheld. The follow-up fixture already contains a matching task worktree with recorded absolute path, branch and total worktree count. Grade reuse of that exact path/branch and no additional worktree, independently of skill routing. Capture rule/skill reads, worktree commands, working directories and edits. Grade command order, task isolation and unchanged unrelated bytes. Record unavailable capabilities separately; missing traces cannot pass.

## Review packet

Include source IDs, actual commands/outcomes, safe report locations, a complete catalog/path matrix with proof levels and reasons, behavioral case grades/limits, diagram consistency and unresolved findings. This is existing workflow evidence, not feature 005 visualization.
