# Implementation evidence for feature 006

## Group A baseline

On 2026-11-25, the isolated feature worktree `D:/Development/cb-wt/layered-testing-verification` was clean on `codex/layered-testing-verification` at `d798622e593d3c97bc773b7b155887df01cabf51`. The requirements checklist had 17 checked items and zero unchecked items.

- Host reported Microsoft Windows 10.0.26300 on x64, PowerShell 7.6.9, Git 2.55.0.windows.3 and RTK 0.50.0.
- `global.json` pins SDK 10.0.404 with roll-forward disabled and selects native Microsoft.Testing.Platform mode. The installed SDK selected 10.0.404.
- `Booking.ContractTests.csproj` uses MSTest.Sdk 4.5.3, net10.0, C# 14, a lock file and the existing custom evidence entry point. The lock resolves Microsoft.Testing.Platform 2.5.3.

| Command | Observed result |
| --- | --- |
| `rtk dotnet test --project tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj` | Exit 0. Passed 24, failed 0, skipped 0. |
| Same project with `--no-build --list-tests` | Exit 5. Native test command enabled the TRX reporter, which rejects discovery-only mode. |
| `dotnet tests/Unit/Booking.ContractTests/bin/Debug/net10.0/Booking.ContractTests.dll --list-tests` | Exit 0. Discovered the 24 methods below. |

The assembly was `Booking.ContractTests`. The methods are listed under their fully qualified class names.

| Class | Existing methods |
| --- | --- |
| `Booking.ContractTests.ContractCases` | `AttendanceChronology`, `ConflictingSnapshot`, `CorrectAttendance`, `DuplicateSnapshot`, `ExistingImportAndUnknownUpdate`, `FirstImport`, `GuestMembership`, `IdentityDeadline`, `InvalidSnapshot`, `OperationOutcomes`, `PublicProjection`, `RegistrationChange`, `RetryAndReconcile`, `SafeReferences`, `ScopedResources`, `StaleUpdate`, `WireRoundTrip` |
| `Booking.ContractTests.EvidenceCases` | `FirstImportJoinsCompiledMetadata`, `IncompleteRunCannotReturnSuccess`, `KeepsFailuresAndPreservesSkippedAndMissingAsPending`, `KeepsHarnessChecksSeparateAndInt06RuntimePending`, `RejectsDuplicateTestAndMapIdentities`, `RejectsUnmappedAndMalformedTrx`, `UsesOnlyFreshRunFiles` |

These results establish the current contract and harness suite baseline for V01 and V03. Booking runtime scenarios remain pending.

## Group A checks at `6f22d1b`

The [required identity inventory](data-model.md#required-identity-inventory) records the 24 baseline identities and four new harness methods. The built copy matched the tracked file byte for byte. The validator requires both inventories to be nonempty and unique, with exact membership after normalizing assembly case. Class and method comparisons are case-sensitive. Existing scenario and fixture checks still run.

The Debug build used `rtk dotnet build tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --no-restore` and completed with zero errors and warnings. The test commands below use `dotnet tests/Unit/Booking.ContractTests/bin/Debug/net10.0/Booking.ContractTests.dll` without the stock TRX reporter.

| Check | Selection | Observed result |
| --- | --- | --- |
| T002 controlled red run with a no-op validator | `--filter` selected the four inventory harness methods | Platform exit 2. Passed 0, failed 4, skipped 0. |
| T002–T004 focused run after implementation | `--filter` selected the four inventory harness methods and the three intake methods below | Platform exit 0. Passed 7, failed 0, skipped 0. |
| T004 full contract suite | Unfiltered | Platform exit 0. Passed 28, failed 0, skipped 0. The suite includes 17 contract and 11 harness methods. |
| T003 executable entry point | `dotnet <copied assembly> --scenario-evidence <fresh directory>` with an empty inventory, a duplicate, one removed row or one added row | Each invocation exited 1 with its fixed inventory error before test execution. The invocation directories contained no map or report. The disposable build copy is under the test project's ignored `bin/` directory. |

| Scenario | Passing methods and assertions |
| --- | --- |
| V03 | `RejectsEmptyOrDuplicateRequiredIdentities`, `RejectsEmptyOrDuplicateDiscoveredIdentities`, `RejectsMissingOrUnexpectedDiscoveredIdentities` and `MatchesNormalizedAssemblyAndExactClassAndMethod` use explicit synthetic identities. |
| INT-01, INT-03, SAL-01 | `FirstImport` and `StaleUpdate` verify that the create and newer-update events carry the accepted BookingId. |
| INT-04 | `ConflictingSnapshot` verifies scope, BookingId, source UpdatedAt, open state and the reason for both live conflicts and unavailable comparisons after erasure. |

`./agents/hooks/refresh-graph.ps1` exited 0 and produced a parseable `graphify-out/graph.json` with 1,116 nodes and 2,272 edges. A bounded query found `CompiledMap` and `ValidateRequiredIdentities`. The code-only extractor reported six files with no symbols, including the refresh hook and Spec Kit PowerShell scripts.

The passing intake assertions are contract evidence. Runtime and hosted verification remain pending, as do the later full-check receipt, reporter and source-binding checks.

## Group A review fix

The evidence entry point now compares the required inventory with reflected assembly, class and method identities before reading categories, fixtures or scenario metadata. Those checks still run once the identities match.

`RejectsInvalidInventoryBeforeReadingMissingFixtures` starts the evidence entry point from a disposable copy of the executable dependencies and catalog with an empty inventory and missing fixtures. It checks for the fixed inventory diagnostic, exit 1, empty standard output and absence of the map, TRX and joined report.

| Check | Observed result |
| --- | --- |
| `rtk dotnet build tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --no-restore` | Exit 0. Zero errors and warnings. |
| Direct executable filtered to `RejectsInvalidInventoryBeforeReadingMissingFixtures` before the ordering fix | Platform exit 2. Passed 0, failed 1, skipped 0. The assertion expecting the inventory error failed. |
| Direct executable with `--filter 'FullyQualifiedName~Booking.ContractTests.EvidenceCases'` after the fix | Platform exit 0. Passed 12, failed 0, skipped 0. |
| Direct executable unfiltered after the fix | Platform exit 0. Passed 29, failed 0, skipped 0. The suite includes 17 contract and 12 harness methods. |
| `./agents/hooks/refresh-graph.ps1` | Exit 0. The graph parsed with 1,119 nodes and 2,280 edges. The bounded query found `ReflectedIdentity`. The incremental extraction reported no parser errors for the changed files. |

## Group B checks

The Windows worktree started at `8c7491babf841d05912ee9056e30ef2873cbe5e8`. The requirements checklist had 17 checked items. The existing SDK and package lock were used.

| Check | Observed result |
| --- | --- |
| `rtk dotnet build tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --no-restore` | Exit 0, zero warnings and errors. |
| Direct Debug DLL with `--filter 'FullyQualifiedName~Booking.ContractTests.EvidenceCases'` | Exit 0. Passed 19, failed 0, skipped 0. The initial 18/19 run exposed an invalid synthetic test setup that supplied two states to a property bag that accepts one. |
| Direct Debug DLL, unfiltered | Exit 0. Passed 36, failed 0, skipped 0. |
| Direct Debug DLL with `--scenario-evidence <fresh-directory>` | Process and platform exit 0. The four files were `booking-contracts.trx`, `execution.json`, `scenario-map.json` and `scenario-results.json`. The joined XML and JSON had the same run ID as the receipt and covered 17 contract and 19 harness passes. `sourceVerified` and `verificationEligible` were both false. |
| Direct Debug DLL with `--scenario-evidence <run-directory>/suite --run-context <run-directory>/run.json` | Process and platform exit 0. All 36 results joined and the receipt matched the parent UUID and digest. Source verification and eligibility remained false. |
| Direct Debug DLL in a disposable source copy with one failing harness test | Process and platform exit 2, test outcome failed, evidence outcome passed. The probe placed a synthetic sentinel in the assertion, stdout, stderr and a `TestContext.AddResultFile` candidate. The four constructed records contained no sentinel. Native MSTest copied the attachment into the suite and the platform listed its path on stdout. This run exposed a privacy blocker. |
| `./agents/hooks/refresh-graph.ps1` | Exit 0. Graph parsed with 1,176 nodes and 2,454 edges. A bounded query found `SafeTestEvidence`, `JoinObservedResults` and `ReadRunContext`. No parser errors were reported. |

| Requirement | Passing methods |
| --- | --- |
| Capture allowlisted terminal outcomes and ignore diagnostic properties and nonterminal updates | `CapturesOnlyKnownTerminalFields` |
| Reject absent, unknown or ambiguous method identities, duplicate terminal events and unsupported states | `RejectsMissingAmbiguousAndUnknownTerminalIdentities`, `RejectsDuplicateTerminalEventsAndInvalidStates` |
| Reject invalid arguments and absent, malformed or mismatched context | `RejectsInvalidEvidenceArguments`, `RejectsMissingMalformedAndMismatchedRunContext` |
| Preserve the observed exit and bind the receipt to the context and joined run | `BindsReceiptToContextAndJoinedRunWhilePreservingPlatformFailure`, `UsesOnlyFreshRunFiles` |
| Reject mismatched exits, incomplete results and interruption as verification passes | `ReceiptsDistinguishStandalonePassMismatchedExitAndInterruptedRun`, `IncompleteRunCannotReturnSuccess` |

The child accepts `--scenario-evidence <fresh-directory> [--run-context <parent-run-json-path>]`. A context is version 1 with a nonempty UUID `runId`, lowercase SHA-256 `source.digest`, and `evidence.suite` equal to `suite` beneath the `run.json` directory. Standalone executions leave the run and source bindings null. Both source verification flags remain false pending the actual source/build attestation.

The pinned adapter stages files before it publishes the terminal event, so the data consumer cannot prevent that copy. The remaining attachment behavior blocks the privacy requirement. Parent stdout/stderr capture and source/build attestation remain part of group C. [MSTest staging implementation](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MSTestTestFramework.cs) and [result recorder](https://github.com/microsoft/testfx/blob/0b1d8b5f7b2186835e2cf26292f3478f7cca087e/src/Adapter/MSTest.TestAdapter/TestingPlatformAdapter/MtpTestResultRecorder.cs).

## Group B attachment-policy amendment

The amended implementation started on Windows at `9f3c8257884d4f535a0d22842c624ad2bf9820fb`. The Owner-approved guard inspects the closed set of owned assemblies by reading PE metadata and method bodies. A runtime signature or test shape outside the reviewed set fails the guard. Ordinary compilation-generated pointer data is supported, while indirect calls, attachment function targets and user module initializers are rejected. The guard does not restrict arbitrary file or process operations in reviewed test code.

| Check | Observed result |
| --- | --- |
| `rtk dotnet build tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --no-restore` | Exit 0, zero warnings and errors. |
| `./agents/hooks/check-attachment-policy-tests.ps1` | Exit 0, all 18 checks passed. The accepted run checked four DLLs and 1,654 methods, with ordered SHA-256 values matching the files. The rejected inputs covered an absent owned reference, direct/helper/constructor/async/function-target attachment references, reflection/calli/dynamic invocation, custom executors, lifecycle and data attributes, inheritance, module initialization, malformed PE and managed metadata, and a stale helper source check. The canaries entered no platform run, recorded no platform exit and left no body marker or staged files. |
| Direct Debug DLL with `--filter 'FullyQualifiedName~Booking.ContractTests.EvidenceCases'` | Exit 0, passed 19, failed 0, skipped 0. The tests verified the receipt includes a literal JSON `policyVerified: false` without a verified parent. |
| Guard followed by direct Debug DLL `--scenario-evidence <fresh-directory>` | Guard and platform exit 0. The joined report accounted for all 36 passes and matched the receipt and XML run IDs. The four allowlisted files were retained. Source, policy and eligibility flags remained false in the standalone receipt. |
| Guard followed by direct Debug DLL in a disposable copy with one failing assertion and stdout/stderr sentinel, without an attachment | Guard exit 0. Process and platform exit 2, 35 passed and one failed. The receipt reported failed tests with a valid joined report. The sentinel occurred in none of the four retained evidence files or either in-memory captured stream. |
| `./agents/hooks/refresh-graph.ps1` | Exit 0. Parsed 1,250 nodes and 2,595 edges. A bounded query found `AttachmentPolicy.Inspect` and `ValidateBody`. The extractor reported no symbols for the top-level PowerShell guard wrapper. |

The first fixture build exposed a missing direct reference to the pinned framework extensions assembly that contains `TestContext`. After that was added, the dynamic fixture was rejected as unresolved before known dynamic dependencies were given their explicit unsupported code. It remained rejected throughout. Earlier staging evidence and the policy-blocked cleanup of the old ignored probe remain recorded in the amendment.

For group C, call `check-attachment-policy.ps1 -AssemblyPath` with the ordered test, application, contracts and domain DLL paths. Exit 0 accompanies a single JSON verdict with `policyVersion: 1`, `outcome: "passed"`, a null `failureCode`, positive `assembliesChecked` and `methodsChecked` counts, and `assemblySha256` containing lowercase hashes in input order. Exit 1 accompanies a fixed safe failure verdict. The hashes cover the checked DLL subset. The parent must compare them against its own declared inputs in the same order and bind the passed verdict to the larger artifact digest, which also covers the copied data and manifests. Only the future before-load parent phase and matching child attestation can set `policyVerified` and verification eligibility. Parent integration, Linux and hosted execution, and full-check source binding remain unverified.

## Group B review fixes

At `ba1298c`, the baseline guard incorrectly accepted a custom data source and two owned method references without matching declarations. A copy of that guard reproduced all three findings against disposable compiled inputs. No fixture assembly was loaded or executed.

The guard now rejects owned interface declarations that reach the MSTest `ITestDataSource` interface, including custom attributes that implement it through another owned interface. Owned method references must match a single definition, including the calling convention, generic arity, required parameter count, parameter and return types. The comparison preserves the assembly-qualified type identity, generic arguments, modifiers and array shape.

| Check | Observed result |
| --- | --- |
| `./agents/hooks/check-attachment-policy-tests.ps1` | Exit 0, 22 checks passed. `CustomDataSource` was rejected with `unsupported-execution`. `MissingOwnedMethod` and `MismatchedOwnedGenericSignature` were rejected with `unresolved-metadata` despite same-name overloads. `MatchingOwnedGenericMethod` and the four real DLLs passed. Rejected fixtures left no body marker, platform receipt or staged output. |
| Direct Debug DLL with `--scenario-evidence <fresh-directory>` | Process and platform exit 0. All 36 results passed, and the receipt, XML and joined report had matching run IDs. Only the four allowlisted records were generated. Source verification, policy verification and eligibility remained false for the standalone invocation. |
| `./agents/hooks/refresh-graph.ps1` | Exit 0, 1,398 nodes and 2,830 edges. The bounded query found `FindOwnedDefinition` and `ValidateOwnedMethod`. The extractor produced no symbols for the top-level guard wrapper and the existing Langfuse stack script. |
