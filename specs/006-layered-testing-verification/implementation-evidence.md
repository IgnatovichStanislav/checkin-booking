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

## Group A checks

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
