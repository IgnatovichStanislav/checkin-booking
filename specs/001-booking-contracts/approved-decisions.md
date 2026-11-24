# Booking contract decisions

These decisions apply to the first Salou VIP interface and the shared rules used after provider-specific mapping. CHECKIN is the hotel's service. The Police Department System (PDS) is an internal fictional destination.

## Ownership and identity

Provision integration API credentials with explicit hotel scope and permissions. Resolve the provider from authenticated configuration. Salou resolves its single hotel from that configuration. H10 will validate its supplied property against the authenticated integration. Enforce ownership on every lookup, guest-parent relationship, worker action, subscription and monitoring view. The MVP provides no self-service hotel registration or credential issuance. Load local secrets at runtime from the ignored root `secrets/` directory.

The stable business identity and logical Kafka key are `providerId:propertyId:srcBookingId`. Preserve component boundaries when encoding the key. Generate a separate UUIDv7 `bookingId` with `Guid.CreateVersion7`. Each required `srcGuestId` is stable and unique within the source booking. Generate a `guestId` on first appearance. Match guests only by scoped source ID. Event, notification and registration request identifiers each identify their own business effect and survive retries. Neither timestamps nor event IDs alone identify all effects.

## Full snapshot

| Field | Salou rule |
| --- | --- |
| srcBookingId | Required immutable source booking identifier, up to 128 characters. |
| createdAt, updatedAt | Source UTC instants with explicit Z. Creation time is immutable. Update time is at least creation time and must advance on every business change. |
| arrivalDate, departureDate | Required hotel-local dates, YYYY-MM-DD. Departure must follow arrival. |
| reservationStatus, cancelledAt | Status is confirmed or cancelled. A cancelled snapshot requires its source cancellation instant in UTC. |
| guests | The complete current guest list, 1–100 entries, including on a cancelled reservation. |
| guests[].srcGuestId | Required source guest identifier, up to 128 characters. Reject duplicates within a snapshot. |
| guests[].firstName, guests[].lastName | Each required, up to 100 characters. |
| guests[].dateOfBirth | Required date. Reject a future date using the hotel's local calendar. |
| guests[].passport | Required member. Explicit null is valid. An object must contain number (up to 50 characters), issuingCountry (two-letter ISO country code) and expiresOn (YYYY-MM-DD). An expired passport can be stored. |

Limit each request body to 1 MiB. Reject incomplete or invalid snapshots as a unit. Report stable structured error codes and field paths without echoing rejected values. A Salou payload cannot select a hotel or alter attendance, operation outcomes or destination. Hotel master data requires a validated IANA `timeZoneId`, for example `Europe/Madrid`. Configuration offers supported zones in a dropdown. Fixed offsets and abbreviations cannot identify a hotel zone.

Compare normalized typed source business fields, including membership and restricted guest fields, with guests matched by `srcGuestId`. Compare equivalent date/time representations by value. Ignore JSON property order, guest array order, eventId and diagnostic receivedAt. Preserve meaningful text differences. The source must document its actual timestamp precision and update guarantees. The acceptance comparison must preserve supported precision and flag collisions. No numeric source revision, artificial truncation or tie-break by arrival order is part of this contract.

## Import, update and durability

| Request | Case | Response and durable effect |
| --- | --- | --- |
| POST /api/v1/bookings | Initial import of an existing PMS booking | 201 with Location for the booking after commit. Return only bookingId, srcBookingId and guests containing guestId and srcGuestId. |
| Same POST | Existing identical normalized snapshot including its update time | 200 with existing identifier mappings, including after a lost response. No second booking or outgoing event. |
| Same POST | Existing different snapshot, older or newer | 409 directing the hotel to PUT the existing booking. Preserve the booking. |
| Same POST | Existing different snapshot with equal updatedAt | 409 directing the hotel to PUT. Also durably record the conflict and initiate authoritative reconciliation. |
| PUT /api/v1/bookings/{bookingId} | No such authorized booking exists | An unknown booking returns 404. No upsert. Apply ownership checks before disclosure. |
| Same PUT | Source booking ID disagrees with stored mapping or creation time changes | Reject the invalid snapshot. Source identifiers and creation time are immutable. |
| Same PUT | Newer source snapshot | 200. Atomically apply all source-controlled data, membership and resulting booking outbox row. |
| Same PUT | Equal time and identical business data, or older time | 200. Duplicate or stale respectively. No booking or outbox change. |
| Same PUT | Equal time, different business data | 409. Keep accepted data, durably record and surface the unresolved conflict, reconcile with source. Restrict any identity needed for reconciliation. |

The import contract has no `Idempotency-Key` header. The transaction guards the scoped source identity, including simultaneous first imports. Booking, restricted guest data and outbox commit together in PostgreSQL. An unsuccessful transaction produces no successful acknowledgment. Once committed, the HTTP request can succeed independently of Kafka. The outbox publisher sends opaque references to Kafka. The immutable event ID links publication to the accepted change. The scoped booking key groups events for ordering. Guest references, when needed, are opaque IDs. Workers load required restricted data within hotel scope. An offset advances only after the consumer's database commit, or after durable dead-letter handoff on terminal processing failure. Do not skip earlier incomplete offsets. Replay and redelivery must preserve business effects. Kafka-specific keys, partitions, offsets, retries, dead letters and replay remain visible in the Kafka project. Shared rules and mappers have no generic queue abstraction or broker dependency.

## Membership, attendance and corrections

Omission cancels current participation and preserves history. Reappearance restores the same guest ID. A different source guest gets its own ID. Cancelling the booking cancels all current participation and initiates required downstream cancellation work. There is no booking DELETE. A newer confirmed snapshot can reactivate the booking and restore listed guests. Evaluate their registration anew. Restoration cannot reinstate an old check-in or infer no-show from omission.

POST /api/v1/bookings/{bookingId}/guests/{guestId}/attendance-events accepts a status and UTC `occurredAt`. The authenticated principal supplies the actor, and CHECKIN records `receivedAt` separately. Attendance begins at expected. An active guest can follow these ordinary transitions.

| From | Permitted next statuses |
| --- | --- |
| expected | arrived, checkedIn, noShow |
| arrived | checkedIn |
| checkedIn | checkedOut |
| noShow | arrived, checkedIn |
| checkedOut | None without an authorized correction |

The hotel explicitly reports checkedIn after its own checks. Cancelled participation must be restored before new attendance can be reported. A noShow guest can be cancelled while retaining the noShow in history. To recognize an exact retry, compare booking, guest, authenticated actor, status and occurrence instant against the stored report before evaluating chronology. Ignore receipt time. A repeat has no new effect. An older unseen event, same-time contradictory event or invalid ordinary reversal returns 409 for reconciliation. Corrections require separate permission, a reference to the original event and a reason. Append an attributed and timestamped correction and retain the original. For an exact correction retry, include its target and reason in the duplicate comparison. Planning defines the wire format for corrections and the representation of current attendance after restoration.

## Registration operations

Each guest operation pins its destination and stable request ID before submission. Each hotel selects default GPDS, local LPDS override, or disabled. Disabled configuration prevents submission. Keep destination details internal. Retry the original destination even if configuration changes. Send cancellations for confirmed registrations to the original destination. An unsupported action requires intervention. A worker sends only the required synthetic registration data.

| Internal state | Trigger | Outcome |
| --- | --- | --- |
| pending | Missing passport, passport expired against today's hotel-local date, or correctable data issue | Remain pending with a reason. Await valid hotel data. |
| pending | Ready to submit, processing, or a known temporary failure | Remain pending during automatic work. Allow five submission attempts total, with delays of 5 seconds, 30 seconds, 2 minutes and 10 minutes between attempts. |
| pending | Uncertain submission outcome | Remain pending with uncertainty reason. Reconcile against the pinned destination and operation before any resend. |
| pending | Definitive confirmation | Mark confirmed. Preserve the outcome and unchanged hotel attendance. |
| pending | Permanent failure or exhausted automatic attempts requiring intervention | Mark failed with a reason. Authorized manual retry uses the same operation identity and destination and returns the operation to pending. |
| confirmed | Duplicate, obsolete or delayed result | Preserve confirmed history. A later business change can create a distinct operation. |

Apply only registration-relevant changes. For an unsent pending request, incorporate the latest accepted data before submission. Once submission may have occurred, correlate and reconcile its outcome before sending changed data. A definitive rejection requiring hotel correction must surface a safe actionable reason. An unchanged request retry reuses its identity. Keep any correction to the submitted data distinguishable from a retry of the old content. After confirmation, create a distinct amendment or cancellation operation when required. Before submission, cancellation stops pending work. After uncertain submission, reconcile, then cancel if necessary. Keep duplicate and late results confined to their own operation. Precise destination-specific amendment support, changed-payload submission protocol and simulator responses belong to feature 004.

## Public status and callbacks

GET /api/v1/bookings/{bookingId}/status exposes safe booking and guest ID mappings, reservation status, guest participation and attendance, and the current guest operationId, registrationStatus and safe reasonCode where applicable. Include the active-guest summary and outstanding cancellation work. Omit names, dates of birth, passport details, internal pdsStatus, destination identity and raw provider errors from public views and notifications. Broker records, logs, screenshots and monitoring must also exclude guest identity.

| Public registrationStatus | Hotel interpretation |
| --- | --- |
| processing | Automatic processing, retry or reconciliation is in progress, including a temporary downstream outage. |
| actionRequired | The hotel must supply or correct data. Safe reasons include missing_passport and passport_expired. |
| completed | CHECKIN received definitive confirmation for the current operation. |
| failed | Automatic work cannot finish. Intervention is required. |

Across active guests, select the first status present in priority order actionRequired, failed, processing, completed. Completed therefore means all active guests completed. With no active guest the summary is null. Outstanding cancellation work has its own representation even when the summary is null. A new operationId can show processing after a previous operation completed. A status lookup gives the current snapshot even after a delayed notification.

For CHECKIN registration changes only, atomically persist the status transition and its notification outbox row. Send signed webhooks with stable notificationId, bookingId, guestId, operationId, registration status, safe reason and UTC occurredAt. Repeated deliveries keep the same notificationId. Subscribers tolerate duplicates and delays. A hotel or authorized provider can manage endpoint, enabled flag, event-state filter, signing secret rotation, delivery history and manual retries within its authorized hotels. Subscription permissions cannot confirm a registration. Delivery failure never changes registration status. Proposed delivery states are pending, delivered and failed. Detailed delivery transitions, signature algorithm and rotation protocol are reserved for planning.

## Identity retention

Keep names, birth dates and passport data only in restricted registration storage, including any retained conflict detail. Delete identity 30 days after the hotel's local checkout date, or 30 days from source cancelledAt for a cancelled booking. Removing one guest while the reservation remains confirmed does not supply a booking cancellation timestamp. Apply the checkout-based deadline in that case. Pending and failed operations do not extend the deadline. Retain only minimal identity-free history. Planning specifies execution clock and boundary behavior, including reactivation, and how late retries respect erasure.

## Decision precedence and planning boundaries

The constitution v1.0.0 permits a database-first path. The Owner selected that path for Salou, replacing the broker-first intake order illustrated in earlier shared guidance. The older Kafka key's booking component denotes the source ID. The separate internal UUID addresses CHECKIN bookings. The catalog's provisional PDS state names describe outcomes now expressed through internal operation state, reason and public CHECKIN status. Shared diagrams and historical guidance await their appropriate review gate.

Feature 001 defines contracts. Provider-specific H10 and Cassis payloads, reconciliation cadence, polling mechanics, runtime services, local ports, packages, backend framework and test-result mapping remain outside this stage. Planning verifies source precision, database representation, .NET TimeZoneInfo and ICU portability, correction encoding, and public representations for disabled or stopped work where no submission exists. The local stand, Kafka path, PDS integration (including LPDS) and coverage visualization belong to 002–005 respectively. The optional monitoring UI consumes safe contracts in a later delivery.

## G2 contract changes

The Owner approved C01 and C02 on 2026-11-24. These additions qualify the G1 tables above.

**C01.** Evaluate the accepted and incoming retention deadlines independently. At or after expiry reject a first POST or a newer expired PUT with 410 identity_erased. An existing POST with a different update time returns 409 use_put with the owned updatePath. Every existing-booking POST 409 carries that path. After erasure an equal-time POST or PUT returns 410 only after durable opening or joining of an active, scoped comparisonUnavailable reconciliation intent for that instant. A closed case cannot absorb a later request. Keep only identity-free metadata. Stale PUT returns 200 with current mappings. A newer eligible snapshot may reactivate without restoring erased versions. Rejection preserves any live accepted booking under its own clock. The [retention table](data-model.md#retention-clock) specifies effects and exact instants.

**C02.** When cancellation stops previously visible unsent work, GET returns a null current operationId, registrationStatus and reasonCode. Atomically enqueue a callback identifying the historical operation with null registrationStatus and reasonCode registration_stopped. A null eventStates entry subscribes to that transition. Initially disabled work has no operation or notification. Later disabling preserves pinned or confirmed work. Completed cancellations leave the outstanding list. Active guests with no applicable operation prevent a completed summary, and an all-disabled summary with no operations is null.
