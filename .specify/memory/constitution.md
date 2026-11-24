# Checkin Booking Constitution

**Status**: Ratified v1.0.0 by the Owner on 2026-11-24.
This constitution governs project specifications, plans, tasks and implementation.
AGENTS.md supplies operational guidance consistent with these principles.

## Core Principles

### I. Synthetic Data and Restricted Identity

All application scenarios, fixtures and PDS interactions MUST use synthetic data only.
Guest identity data, including names, dates of birth and passport details, MUST remain
in restricted registration storage and MUST NOT appear in broker records or logs.
Access MUST respect hotel ownership. Broker records and diagnostics MUST use opaque
references where identity linkage is necessary. This keeps learning scenarios from
exposing guest information.

### II. Durability First

Intake acknowledgments MUST occur only after durable acceptance: broker confirmation on
broker-first paths, or database commit on database-first paths. Consumer acknowledgments
and offset commits MUST occur only after the consumer's database transaction commits.
A booking state change and its outbox row MUST be written atomically in the same transaction.
Failed work MUST NOT be acknowledged as successful; dead letters MUST be handed off durably
before acknowledgment.

Retries and redelivery MUST be safe against duplicate business effects. Tests MUST cover
a crash after database commit but before acknowledgment. Documentation and implementation
MUST NOT claim exactly-once delivery; they MUST describe the actual durability and
idempotency guarantees at each boundary.

### III. Source Timestamp Freshness

Freshness MUST use the source booking's updatedAt in UTC, compared atomically per stable
booking identity. Numeric source versions, arrival time and event IDs MUST NOT replace
this freshness rule. Newer snapshots may change state; older snapshots MUST NOT overwrite
it. Equal timestamps with identical normalized business data are duplicates and MUST NOT
produce another booking update or outbox effect.

Equal timestamps with different normalized business data MUST be recorded as conflicts
and reconciled with the authoritative source. They MUST NOT be tie-broken. Conflict
records MUST follow the restricted-storage rules. Source timestamp precision and
monotonicity assumptions MUST be documented; UTC alone does not guarantee either.

### IV. Visible Broker Semantics

The Kafka path MUST expose its keys, ordering, offset commits, retries, dead letters and replay explicitly. It MUST NOT be hidden behind a generic queue abstraction. Booking rules, mappers, freshness, outbox and PDS logic MUST stay independent of the broker so forks can replace the broker project without changing them. Shared synthetic fixtures and scenario IDs MUST be broker-agnostic.

### V. Scenario Coverage

Every scenario in AGENTS.md's Required scenarios list MUST receive an executable test
before or alongside its implementation. The applicable specification and task list MUST
map scenarios to tests. A scenario MUST NOT be reported as implemented until its tests
have run and passed; unimplemented scenarios remain explicitly pending.

Coverage MUST include intake duplicates, stale/conflicting snapshots and concurrency;
hotel isolation and authorization; webhook retries and reconciliation; REST timeout
retries; polling pagination and cursor recovery; broker crash/redelivery, bounded retries
and durable dead-letter handoff; and PDS success, missing data, rejection, outages,
uncertain outcomes, duplicates and booking changes while registration is pending.
Actual test results and unverified behavior MUST be reported separately.

### VI. Small Changes and Runtime Secrets

Changes MUST remain focused and reviewable. New dependencies MUST have a documented need
that existing project capabilities cannot reasonably meet. Unrelated refactoring and
speculative infrastructure MUST NOT be bundled into a feature.

In local development, secrets MUST be supplied at runtime from the repository-root secrets/
directory. Any deployment MUST use a managed secret store and requires explicit authorization.
The directory MUST remain excluded from Git and Docker build contexts. Source code,
tracked configuration, fixtures, database records, images and logs MUST NOT contain
credentials or encryption keys. Tracked configuration may contain file paths or safe
placeholders only. Filesystem access MUST be restricted; ignore rules are not security
controls.

## Project Constraints

Checkin Booking is a local .NET learning test stand for PMS booking intake, fictional
Police Department System registration and registration status. It MUST NOT be presented
as a real hotel's production architecture or a real police integration.

Use the agreed project stack and business rules in AGENTS.md within these principles.
Reservation, guest check-in and PDS registration status MUST remain distinct. Shared
synthetic end-to-end fixtures belong in e2e/fixtures/.

## Development and Review

Specifications MUST state contracts, acceptance scenarios and unresolved decisions.
Plans and tasks MUST identify relevant constitution checks before implementation.
Contributors and coding agents MUST read AGENTS.md and the relevant shared skill rather
than loading the whole skill catalog. Review MUST check data boundaries, transaction and
acknowledgment order, freshness conflicts, broker semantics, scenario coverage and secrets.

Run checks appropriate to each change and report observed results. Do not silently change
contracts or weaken tests to make an implementation pass. Outstanding decisions MUST
remain visible in the specification rather than becoming undocumented assumptions.

## Governance

Contracts MUST change only through a specification update. The update MUST describe the
behavioral change, affected integrations and acceptance tests before the changed contract
is implemented. Implementation convenience is not authority to change a contract.

This constitution governs project specifications, plans, tasks and implementation.
Conflicting project guidance MUST be surfaced and reconciled explicitly; it MUST NOT
silently override these principles. AGENTS.md supplies operational guidance consistent
with this constitution.

Amendments MUST record the rationale, affected principles and migration or documentation
impact, and receive project-owner approval. Update the version and last-amended date for
every amendment. Use a major version for incompatible principle or governance changes,
a minor version for new or materially expanded requirements, and a patch version for
clarifications without behavioral change. Review each specification and implementation
change for compliance; unresolved violations MUST be addressed before acceptance.

**Version**: 1.0.0 | **Ratified**: 2026-11-24 | **Last Amended**: 2026-11-24
