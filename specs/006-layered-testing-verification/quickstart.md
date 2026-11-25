# Validating feature 006

This is the approved validation procedure, not a record of passing feature checks. Independent plan review and Owner G2 approval are complete; implementation and actual verification remain required.

## Prerequisites

Use the dedicated feature worktree with Git, PowerShell 7, Python 3, SDK 10.0.404 and the existing platform prerequisites including ICU/IANA time-zone support. No Docker, database, Kafka, PDS, credentials or model-provider provisioning is required for automated checks. Hosted checks need repository access; manual behavioral cases need an available writing-capable agent session.

## Local checks after implementation

Run from the worktree root:

```powershell
./agents/hooks/check-backend-runner.ps1
./agents/hooks/test-backend.ps1
python eval/check_index.py
python -m unittest discover -s eval -p test_check_index.py
```

Expect each regression suite to succeed because it correctly detects its deliberately invalid fixtures. The real backend check must execute all required compiled tests with fresh joined evidence; no skipped/missing required result is acceptable.

For focused application development, the existing native MTP syntax remains:

```powershell
rtk dotnet test --project tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --filter 'FullyQualifiedName~FirstImport'
```

A focused pass is not the full feature verification result. Re-run the required broader checks after changing production/test logic; do not rerun all suites solely to reproduce already recorded output.

Inspect the current run.json and allowlisted export, verifying source, phases and invocation binding. Confirm that removed required methods, missing/corrupt reports, failed phases and a zero-test selection cannot pass. Exercise the native fallback without changing global PATH or host installations.

The approved attachment amendment adds a post-build policy phase before test executable loading. Backend result attachments and unsupported execution routes are rejected. Forbidden-call canaries are separate disposable compiled inputs, not methods in the normal scanned suite: confirm they leave test bodies unexecuted, platform exit unset and no staged file. Legacy direct evidence is policy-unverified and cannot satisfy the full-check gate. Real assertion/stdout failures without attachments still execute and must preserve their actual failure outcome safely.

## Hosted checks

Use a disposable scoped branch/change to exercise the real workflow's passing and controlled failing paths on both operating systems. Record actual run URLs and source SHAs. The failing test must use a safe fixed diagnostic and return failure without publishing raw assertion data. Verify run-specific exports exist, older run directories are excluded and available phase metadata uploads even when tests never start.

A local mock or a prepared YAML file cannot satisfy V04. Unavailable repository access remains a blocker; do not mark completion.

## Behavioral cases

Run the two new eval cases independently with fresh sessions in disposable repositories. Follow eval/README.md: provide only the case prompt and normal project rules, withhold grading, capture command/file-read traces and grade each case separately. Check unrelated sentinel bytes/status before and after. Never run the evaluated write actions in a real project worktree.

No behavioral/model execution is automatically launched by the commands above.

## Final packet

Use the unchanged catalog and real test/result metadata to fill all scenario/path cells under the existing verifier handoff. At the baseline there are 90 cells; derive actual applicability from the catalog. Contract decisions may pass while runtime proof stays pending. Include both behavioral grades and live limitations, safe report locations, actual hosted outcomes and diagram consistency.

Refresh the worktree graph after implementation source changes using the existing hook, reporting parser failures; planning documentation alone does not require rebuilding it. Resolve blocking review findings before asking the Owner to merge.

