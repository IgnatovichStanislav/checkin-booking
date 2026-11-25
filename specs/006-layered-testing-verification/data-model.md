# Verification evidence model

These are development records, not booking entities, broker messages or database tables. No runtime identity/contract change is proposed.

## Run manifest: run.json

- schemaVersion: explicit development evidence version.
- runId: fresh UUID; timestamps in UTC.
- source: Git commit, dirty boolean and SHA-256 source digest. Digest sorted relative paths plus contents in the relevant tracked/allowed task source scope; exclude secrets/, generated output and imported caches. Record the scope definition/version, not file contents or diff text.
- environment: operating system, PowerShell version, selected .NET SDK, configuration and helper/native selection.
- phases: preflight, restore, build, test, evidence and export; each has pending/running/passed/failed/notRun, timestamps, numeric exit if observed and fixed safe failureCode.
- result: running/passed/failed/incomplete. Passed requires all required phases and a valid child receipt/results.
- evidence: relative suite/export paths. No absolute user paths, environment dump, exception message, credential or payload.

Write initial state before child tool execution. On normal failure finalize the relevant phase and mark later phases notRun. Cancellation without finalization is incomplete. A source change during a run invalidates its source binding and blocks pass/export of a success verdict.

## Child execution receipt: suite/execution.json

- schemaVersion, platformExitCode (nullable if platform never returned), testOutcome and evidenceOutcome.
- Each outcome is pending/passed/failed/incomplete; fixed failureCode identifies safe causes.
- Receipt binds to the parent runId/sourceDigest through explicit invocation context and to the joined TRX run ID when available.
- Failed platform execution remains failed even if a valid joined report is available.
- Missing/malformed/mismatched receipt means parent evidence failure, never success.
- Receipt carries no ErrorInfo, assertion values, stack trace or stdout.

## Required identity inventory

A tracked tests/Unit/Booking.ContractTests/required-tests.json holds unique assembly/class/method identities, normalized with the existing comparison rules. It contains no scenario tags or fixture data. Compare the exact required/discovered sets; an empty inventory, missing method, duplicate or unexpected identity requires a failing check and intentional reviewed correction. Update it explicitly when adding approved tests; never auto-update during execution.

## Existing records retained

scenario-map.json continues to bind compiled test identities to scenario/feature/requirement IDs, evidence levels and fixture references. scenario-results.json continues to separate businessEvidence from harnessChecks. Preserve existing metadata semantics and fresh TRX joining.

Export sanitized copies built from explicit field allowlists. Do not copy arbitrary new JSON keys or TRX diagnostic text into public artifacts. All export records bind to runId/source digest.

## Coverage and eval records

The review packet joins catalog IDs/path applicability with the explicit test mapping and real results. Each runtime verdict is passed/failed/pending/inapplicable with a reason where required; supporting contract passes remain separately labeled.

Behavioral records contain case/baseline/prompt/trace/checks/grade and live limitations. Private harness/model metadata and raw traces remain ignored under eval/results/. Public summaries contain findings and verification evidence without attribution/session links.

