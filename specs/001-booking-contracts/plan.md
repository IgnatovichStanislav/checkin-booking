# Booking intake contracts and BookingDto

**Feature 001 · 2026-11-24 · G2 approved**
Branch `feature/001-booking-contracts`. The Owner approved the [specification](spec.md) and [contract decisions](approved-decisions.md) at G1. The formats, technical choices and boundaries below were approved at G2 on 2026-11-24.

## Summary

Deliver the Salou VIP wire contracts, a mapper to the shared full-snapshot BookingDto, and executable decisions for validation, freshness, membership, attendance, registration and disclosure. Other integrations can reuse the typed rules without importing HTTP, database or Kafka types. A hotel can verify its request and response formats before the local stand exists.

## Technical context

| Area | Choice |
| --- | --- |
| Target | C# 14, net10.0, SDK 10.0.404 pinned with rollForward disabled. Windows and Linux with ICU and IANA zone data. |
| Dependencies | System.Text.Json, TimeZoneInfo, DateOnly, DateTimeOffset and Guid from the base class library. One direct test-only SDK, MSTest.Sdk 4.5.3 with its MTP runner and TRX reporter. Lock restored test-package versions. |
| Limits | 1 MiB of UTF-8 request bytes, at most 100 guests per snapshot. No performance throughput assumption. Use deterministic clocks and injected IDs in tests. |
| Data | Typed records in memory for individual pure comparisons. Persistence obligations belong to the real PostgreSQL transaction in the later intake host. |
| Interfaces | OpenAPI 3.1.1, JSON Schema 2020-12, strongly typed C# contracts. No external runtime package in the contract libraries. |

## Project structure

| Planned path | Responsibility |
| --- | --- |
| src/Booking.Contracts | Shared BookingDto with scoped source key, source times, dates, status, cancellation instant and full guest input. Separate safe response, reference and notification records. Keep restricted records out of diagnostic formatting, including generated record ToString. |
| src/Booking.Domain | Pure typed decisions over prior and incoming state. Return effects such as apply, ignore, conflict, reconcile, amend or cancel. No counters claiming delivery, mutable repository or host. Depends only on shared contract types. |
| src/Booking.Application/Salou | Provider wire records, bounded decoder, Salou-to-BookingDto mapping and HTTP result mapping from decisions. References contracts/domain. Future H10 and Cassis adapters own their formats. |
| tests/Unit/Booking.ContractTests | Wire round-trips, mapping, rules and safe-output assertions. One MSTest project references the three libraries. |
| e2e/fixtures/001-booking-contracts | JSON input and expected decisions grouped by scenario and case. Shared synthetic IDs and deterministic times. Restricted synthetic fields only in designated input fixtures. Failure messages use paths and codes. |
| test-results/ | Per-run TRX and identity-free scenario evidence. Ignored by Git. |

Review [research](research.md), the [data model](data-model.md), [HTTP contract](contracts/openapi.json), [types](contracts/types.schema.json), [test and evidence guide](quickstart.md), [sequence](../../diagrams/001-booking-contracts/sequence.drawio) and [data boundaries](../../diagrams/001-booking-contracts/data.drawio).

## Delivery boundaries

001 runs without credentials, ports or processes beyond the tests. It tests decisions and wire representations. The stand in 002 supplies isolated services and ports. A separately scoped intake host and persistence implementation using that stand must authenticate, authorize, transact, reconcile, erase and serve HTTP before the Salou runtime cases can pass. A local Compose stack alone cannot establish any of those properties.

003 consumes the committed outbox, publishes immutable references and tests broker redelivery, offset order and durable dead letters. 004 implements fictional destination protocols, pinned submissions, notification delivery and reconciliation against actual simulator results. A future delivery host owns callback signing, subscription administration and attempt history. 005 reads safe results to visualize coverage. H10 and Cassis need their own provider contracts. None of these projects or services is scaffolded in 001.

## Constitution check

| Principle | Before research | After design |
| --- | --- | --- |
| I · Restricted identity | Synthetic only, scope every access | Full snapshot and conflict detail have restricted storage and deadlines. Public, Kafka, fixtures for reports and status outputs have explicit allowlists. |
| II · Durable acceptance | DB-first Salou path approved | Intake commit precedes response and publication. Later consumer DB commit or durable dead-letter handoff precedes offset advancement. Runtime evidence pending. |
| III · Freshness | Per scoped booking, source updatedAt | Exact UTC ticks, structural business comparison, explicit equal-time conflict. Future unique identity guard and locking are required. |
| IV · Broker boundary | No queue abstraction | Shared libraries contain no Kafka client, partition, offset or delivery policy. 003 binds scoped keys and replay to immutable events. |
| V · Coverage | Catalog IDs and feature-local cases retained | Quickstart maps each case to a named test and distinguishes contract evidence from outstanding runtime proofs. All results pending. |
| VI · Small changes and secrets | Libraries and tests only | One direct test SDK. Provisioned secrets are runtime file references. No service or real data required. |

The G1 decision record reconciles the older broker-first example and provisional state names with constitution v1.0.0. The pre-research check passed. At G2 on 2026-11-24 the Owner approved the plan and contract changes C01 and C02. See the [approved decisions](approved-decisions.md#g2-contract-changes), [decision tables](data-model.md) and [before/after diagrams](../../diagrams/001-booking-contracts/data.drawio). Implementation follows task design and analysis.
