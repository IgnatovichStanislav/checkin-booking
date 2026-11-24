# Validating feature 001

The plan contains executable interface descriptions. The C# projects below will exist after G2, task design and implementation. No host, database, Kafka process or PDS simulator is required for the contract suite.

## Planned run

Use SDK 10.0.404, ICU with IANA zone data, and the repository root as the working directory. The implementation pins MSTest.Sdk/4.5.3 in the test project and selects Microsoft.Testing.Platform under test.runner in global.json. Initial restore creates the lock files. Subsequent runs use locked restore.

```powershell
rtk dotnet restore tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --locked-mode
rtk dotnet test --project tests/Unit/Booking.ContractTests/Booking.ContractTests.csproj --report-trx --results-directory test-results/001-current
```

Use a fresh result directory per execution. Expect individually named test methods in namespace Booking.ContractTests, class ContractCases, with the method names below. Give methods TestCategory tags for their scenario IDs and evidence level. Use multiple assertions or internal fixture loops with safe case IDs. Keep stable method identities for reporting.

| Test method | Scenario references | Contract assertion | Outstanding runtime proof |
| --- | --- | --- | --- |
| FirstImport | INT-01, SAL-01 | Create decision, immutable key, 201 mapping and Location | Commit once under response loss, retry returns original guest and booking IDs |
| DuplicateSnapshot | INT-02, SAL-01 | Equal UTC instant and structurally equal fields, reordered guests/JSON → duplicate | No duplicate mutation or outbox row |
| StaleUpdate | INT-03 | Earlier tick → stale PUT 200, even within same microsecond | No persisted regression |
| ConflictingSnapshot | INT-04, A01 | Equal-time business difference → conflict on PUT and POST, restricted reconciliation intent | Atomic conflict record and authoritative reconciliation |
| InvalidSnapshot | INT-05 | Size and field limits, invalid dates, duplicate guest ID, missing vs null passport, zone/DST, safe errors | HTTP size enforcement and zero committed writes |
| ExistingImportAndUnknownUpdate | A01 | Different POST older/newer → 409 with owned update link, unknown PUT → 404, immutable fields rejected | Ownership and transaction outcomes |
| ScopedResources | SAL-02, A05, A09, A10 | Deny wrong hotel, parent or permission across booking, attendance, operations and subscriptions | Real credential and query isolation |
| GuestMembership | A02, A03 | Omission, reappearance, cancellation and reactivation preserve IDs and history, reset attendance | Transactional episode history |
| AttendanceChronology | A04 | Transition table, late arrival, exact retry, contradictory time, unseen older report | Atomic append and deduplication |
| CorrectAttendance | A05 | Target, actor, reason and occurrence distinguish retries, preserve old record | Scoped append under concurrency |
| OperationOutcomes | PDS-01, PDS-02, PDS-03, PDS-06 | Confirmation leaves attendance intact, missing/expired/rejected and corrected data, ignore obsolete result | 004 simulator responses and durable correlation |
| RetryAndReconcile | PDS-04, PDS-05 | Five attempts, delay sequence, exhausted vs uncertain, reconcile before resending | 004 outage and ambiguous outcome, no duplicate submission |
| RegistrationChange | PDS-07, A06, A09 | Pinned destination, unsent edits, uncertain change, amendment and cancellation | 004 destination protocol |
| PublicProjection | A07, A10 | Priority and null cases, cancellation list, callback allowlist, stable notificationId, separate delivery state | Scoped HTTP GET and signed duplicate/delayed delivery |
| IdentityDeadline | A08 | Checkout/cancellation clocks, removal, reactivation, expired replay; no resurrected identity | Physical purge of restricted versions and conflicts |
| SafeReferences | FR-08, FR-09 | Compare JSON fields to exact allowed sets for event, GET, receipt, history and notification. Redact diagnostics and record formatting. Snapshot ID remains pinned across replay | Broker message/log inspection and erasure |
| WireRoundTrip | FR-01–FR-09 | Compare C# serialization to fixed independently authored JSON fixtures and required/null/enum/unknown-member rules in types.schema.json. Verify all OpenAPI operation/response references | Running HTTP and destination compatibility |

Planned C01 fixtures in IdentityDeadline, FirstImport, DuplicateSnapshot, ConflictingSnapshot and ExistingImportAndUnknownUpdate cover every retention-table row at the exact deadline and on either side. Assert a mandatory owned updatePath on every POST 409. After erasure, different source times still give POST 409. Equal-time POST/PUT open or join only an active scoped intent, a closed case cannot absorb a later arrival, and a failed durable handoff cannot return 410. Preserve an existing live booking on rejected newly expired input. Verify no incoming identity writes and independent erasure of previously accepted data. Runtime evidence requires actual transactions, purge and reconciliation.

Planned C02 fixtures in PublicProjection, RegistrationChange and WireRoundTrip cover initially disabled, later disabled with pinned/confirmed work, and visible processing stopped before submission. Reject mismatched GET null tuples, a completed outstanding cancellation, ordinary callbacks with null status, a stopped callback missing its historical operation ID, and a stopped reason paired with an ordinary status. Accept null in the subscription filter and emit a stopped callback only for a real transition. Runtime evidence requires atomic notification publication and scoped delivery.

For every row add both permitted and rejected fixtures where applicable. Share synthetic inputs using JSON case files with caseId, scenarioIds, path, ordered inputs, source instants, receipt instants and expected decisions/effects. Separate restricted input from public expectations. Tests never dump entire payloads on failure.

## Evidence

Produce an explicit sidecar at test-results/<run>/scenario-map.json from the compiled test metadata. Each record carries assembly, class, method, case references, path (SAL or all) and evidence level (contract or runtime). Keep feature-local A01–A10 distinct from the catalog. Normalize TRX TestDefinitions/UnitTest/TestMethod className and name, join definition ID to UnitTestResult.testId and match the sidecar by assembly/class/method. Verify the adapter with a known tagged test and compare the sidecar to the table above. Exercise join failures, missing results, skips and a deliberate sample failure using synthetic result documents. Fail the report if entries collide or a result has no mapping. Report a test failure as failed, a missing/skipped result as pending. A catalog cell passes only after all required runtime proofs for that path ran and passed. Existing TRX files from other invocations are excluded.

All tests and runtime proofs are pending at plan time. INT-06 requires simultaneous first imports and newer updates against real PostgreSQL, with unique identity, ordered decision and transactional outbox checks. No pure test claims this ID. INT-01–05 and SAL-01–02 remain pending for Salou runtime; H10/CAS intake equivalents and H10-01–04/CAS-01–04 await separate adapters. BRK-01–04 and KFK-01–03 require the 003 crash, replay and dead-letter harness. PDS-01–07 await 004. Prior to 005 include the complete catalog/path matrix and reasons for pending or inapplicable cells in the verifier handoff.
