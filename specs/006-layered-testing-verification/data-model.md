# Verification evidence model

These are development records, not booking entities, broker messages or database tables. No runtime identity/contract change is proposed.

## Run manifest: run.json

- schemaVersion: explicit development evidence version.
- runId: fresh UUID; timestamps in UTC.
- source: Git commit, dirty boolean and SHA-256 source digest under the exact input policy below. Record the scope version and digest, not file contents or diff text.
- environment: operating system, PowerShell version, selected .NET SDK, configuration and helper/native selection.
- phases: preflight, restore, build, test, evidence and export; each has pending/running/passed/failed/notRun, timestamps, numeric exit if observed and fixed safe failureCode.
- result: running/passed/failed/incomplete. Passed requires all required phases and a valid child receipt/results.
- evidence: relative suite/export paths. No absolute user paths, environment dump, exception message, credential or payload.

Write initial state before child tool execution. Preflight/restore/build failures stop later execution phases, but finalization and safe manifest export are still attempted. A returned test failure must still permit evidence joining and safe export of valid failed results. Evidence failure skips joined-result export but still permits manifest/receipt export. Export failure fails the overall run without rewriting earlier outcomes. Cancellation without finalization is incomplete. A source change during a run invalidates source binding and blocks a success verdict; its safe failure manifest may still be exported.

## Source and build binding

Backend source scope v1 includes Git-tracked files and Git-unignored untracked files under src/, tests/, e2e/fixtures/, agents/hooks/ and .github/workflows/; the existing copied contract inputs matching specs/archive/001-booking-contracts/contracts/*.json; plus root global.json, Directory.Build.props, Directory.Build.targets, Directory.Packages.props, NuGet.Config and nuget.config when present. Hash sorted normalized relative paths, explicit present/missing markers and file bytes. Tracked deletion must alter the digest. Exclude generated bin/obj/test-results and secrets. Preflight rejects ignored source/build/fixture inputs that would be consumed but are absent from the declared snapshot, unexpected imports outside that scope and links escaping the worktree; intentionally amend the scope before using such inputs. Imported skill trees are not backend build inputs; eval baselines separately identify their selected skill/rules/source revision.

The parent records source digest before build, forces a rebuild using the restored lock and checks the source digest again. A build attestation binds that digest to an aggregate digest of the executed test assembly, referenced application/contract/domain assemblies, runtime/dependency manifests and copied inventory/fixtures/contracts/catalog in its output. The child recomputes the artifact digest for its own loaded assembly/output and compares the parent context. Recheck source before final success. Any mismatch fails evidence; source changed while running cannot be passed off as the original build.

Standalone --scenario-evidence without a verified parent attestation may report actual test outcomes but is explicitly source-unverified and verificationEligible=false. It cannot satisfy the full backend check, hosted V04 or G3 source-bound proof. This avoids labeling a stale binary with the current checkout's digest. Test this with a built binary after changing one source/fixture input, plus tracked deletion and an allowed untracked source file. Independently mutate an archived contract JSON copied by Booking.ContractTests.csproj and assert that source binding changes and stale output is rejected.

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

scenario-map.json continues to bind compiled test identities to scenario/feature/requirement IDs, evidence levels and fixture references. scenario-results.json continues to separate businessEvidence from harnessChecks. Preserve existing metadata semantics and fresh XML joining. booking-contracts.trx is now generated from allowlisted observed terminal test states rather than the stock reporter; it contains known method identities, generated run/test IDs, actual invocation times, statuses and counts only. No ErrorInfo, stdout or arbitrary metadata is created in that file. Duplicate/unknown/missing terminal events fail evidence, and the real platform exit remains independent of the safe report.

Export sanitized copies built from explicit field allowlists. Do not copy arbitrary new JSON keys or TRX diagnostic text into public artifacts. All export records bind to runId/source digest.

## Coverage and eval records

The review packet joins catalog IDs/path applicability with the explicit test mapping and real results. Each runtime verdict is passed/failed/pending/inapplicable with a reason where required; supporting contract passes remain separately labeled.

Behavioral records contain case/baseline/prompt/trace/checks/grade and live limitations. Private harness/model metadata and raw traces remain ignored under eval/results/. Public summaries contain findings and verification evidence without attribution/session links.

