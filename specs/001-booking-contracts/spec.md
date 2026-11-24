# Feature specification · Booking contracts

**Feature** 001-booking-contracts · **Branch** `feature/001-booking-contracts` · **Created** 2026-11-24 · **Status** G1 approved 2026-11-24

Salou VIP imports reservations that already exist in its property management system (PMS). CHECKIN returns identifiers the hotel can use to change the booking, report attendance and follow guest registration. This feature defines the Salou interface and the booking rules other integrations will share. All records in this local test stand are synthetic.

## User Scenarios & Testing

### User story 1 — Import and update a booking (P1)

A hotel needs to know which booking CHECKIN accepted even when the connection drops. Independently exercise this story with one valid full snapshot, its retry, and subsequent updates.

| Case | Given / when | Expected result |
| --- | --- | --- |
| INT-01, SAL-01 | The first import commits, but the PMS times out and repeats it | One booking, one guest mapping per source guest and one outgoing booking event. Both successful responses identify the same records. |
| INT-02, INT-03 | An update repeats the current snapshot with guests reordered, or carries an older source time | Keep the accepted data and mappings. Produce no booking event. |
| INT-04 | Two snapshots share a source update time but disagree on business data | Preserve the accepted snapshot, durably record the conflict and surface it for authoritative reconciliation. |
| INT-05 | One guest in a full snapshot fails validation | Reject the entire snapshot with a safe field-level error. |
| INT-06 | Concurrent imports or updates target the same source booking | At most one effect per accepted snapshot. The latest non-conflicting update wins regardless of arrival order. Each distinct increasing update may apply. |
| SAL-02 | A credential targets a booking, guest or subscription outside its hotel scope | Deny access without revealing another hotel's data or making changes. |
| A01 | The PMS imports different data for an existing source booking, or updates an unknown internal booking | Direct the first caller to update the existing booking. Report the unknown booking to the second. Neither request creates or replaces a booking. |

### User story 2 — Maintain the guest list and report attendance (P1)

A hotel can remove a guest, restore a returning guest and explicitly report a late arrival. Exercise the story independently using a booking with two guests and an authorized attendance reporter.

| Case | Given / when | Expected result |
| --- | --- | --- |
| A02 | A full update omits a guest, then a newer update restores the same source guest | Cancel then restore participation under the same guest ID. Preserve history. A different source guest gets a new ID. |
| A03 | The hotel cancels a reservation and later confirms it again | Cancel all current participation, assess cancellation work, then restore listed guests. Reactivation never reinstates an old check-in. |
| A04 | A guest marked no-show arrives late, or a hotel repeats the same attendance report | Accept the permitted late arrival. An exact repeat adds no event. Reject conflicting or older new reports for reconciliation. |
| A05 | An authorized supervisor corrects an erroneous attendance event | Preserve both the original and the attributed correction, including time and reason. Deny a reporter without correction permission. |

### User story 3 — Follow registration and resolve problems (P2)

A hotel can tell whether to supply data, wait, or seek help. Exercise independently with a synthetic booking and controlled registration outcomes.

| Case | Given / when | Expected result |
| --- | --- | --- |
| PDS-01, PDS-06 | The destination confirms registration, then repeats an earlier result | The current registration completes once. Attendance stays as reported by the hotel. |
| PDS-02, PDS-03 | Required information is missing, expired or rejected as correctable | Tell the hotel what to correct through a safe reason. Resume on valid corrected data. |
| PDS-04, PDS-05 | A temporary failure or an uncertain timeout interrupts submission | Show processing during automatic work. Reconcile uncertain outcomes before any resend. Stop automatic submission at the attempt limit. |
| PDS-07, A06 | Registration-relevant data changes during or after submission, or the hotel cancels | Resolve outstanding work without duplicate registration. Preserve confirmed history and identify subsequent amendment or cancellation work separately. |
| A07 | Guest states differ, all guests cancel, or a callback arrives twice or late | Show the priority summary for active guests and expose outstanding cancellation work separately. The hotel can retrieve the current snapshot. Callback delivery never changes registration. |
| A08 | A booking reaches its identity retention deadline with registration still outstanding | Remove restricted identity on schedule. Keep only minimal identity-free history. |
| A09 | Two hotels select different destinations, then an operator changes configuration during retries | Keep existing operations pinned. Route new operations according to the applicable policy. Never silently switch destinations. |
| A10 | A hotel configures, disables or retries a notification subscription | Enforce hotel scope and permissions, keep delivery history and prevent subscription actions from confirming registration. |

### Edge cases

A cancelled reservation still needs a complete guest list. Removing a no-show preserves the reported attendance in history. Missing and explicitly null documents have different meanings. A timestamp collision remains visible even when it arrives through initial import. An obsolete result for a prior operation cannot complete the current operation. The [contract record](approved-decisions.md) gives the decision tables and validation limits.

## Requirements

### Functional requirements

| ID | Requirement | Acceptance |
| --- | --- | --- |
| FR-01 | Resolve ownership from the authenticated integration. Keep each hotel's bookings, guests, operations, subscriptions and status access within its authorized scope. | SAL-02, A09, A10 |
| FR-02 | Give each scoped source booking one stable internal ID and each source guest within it one stable guest ID. Return both mappings. | INT-01, SAL-01, A02 |
| FR-03 | Validate the complete snapshot and atomically accept its booking, restricted guest changes and publication record before confirming intake. Compare source update times per booking at acceptance. | INT-01 through INT-06, A01 |
| FR-04 | Treat the snapshot as the authority for membership and reservation status. Preserve participation history across removal, cancellation and restoration. | A02, A03 |
| FR-05 | Accept attendance only through an authorized attendance action. Preserve original events when correcting reports. | A03 through A05, PDS-01 |
| FR-06 | Track registration operations separately, pin each destination, bound retries and reconcile uncertain results. Preserve old operation outcomes during changes. | PDS-01 through PDS-07, A06, A09 |
| FR-07 | Provide guest-level CHECKIN status, active-guest summary and outstanding cancellation work. Publish signed notifications when registration changes and retain their delivery history. | A07, A10 |
| FR-08 | Restrict guest identity to authorized registration storage and necessary submission. Enforce the approved retention deadlines, including when a destination is unavailable. | A08, SAL-02, PDS-04 |
| FR-09 | Keep booking rules independent of transport. Publish opaque references after acceptance and protect consumers against duplicate business effects. | INT-01, SAL-01, future BRK/KFK execution |

### Key entities

A **source booking** belongs to one provider and hotel. An internal **booking** maps it to CHECKIN. A **guest participation** links a stable source guest to its internal guest and preserves its membership history. An **attendance event** records what the hotel observed. A **registration operation** tracks one registration, amendment or cancellation. A **notification** reports a CHECKIN change, and a **delivery** records what happened when a subscription received it.

## Success Criteria

### Measurable outcomes

- For every retry of an identical accepted import, the PMS receives the original mappings. The number of bookings and first-import events stays at one.
- In every duplicate, stale, conflicting or invalid update case, the accepted booking and its outgoing booking-event count stay unchanged. Every equal-time conflict is available for reconciliation.
- Each permitted attendance change has an attributable history entry. A completed registration alone produces zero attendance changes.
- Each automatic registration sequence sends at most five attempts. Every uncertain resend has a preceding reconciliation. Every confirmed operation keeps its historical outcome.
- For every status lookup and callback, the hotel sees only authorized identifiers, states and safe reasons. Guest identity occurs zero times in these responses, broker records, logs and monitoring.
- Every cancellation with outstanding work remains discoverable even with zero active guests. Restricted identity is removed at the applicable 30-day deadline.

## Assumptions

The hotel provisions source identifiers and sends complete snapshots with authoritative UTC change times. The integration has a configured hotel, authorized permissions and a validated hotel time zone. The destination is fictional. The [decision record](approved-decisions.md) defines the interface, precedence over older guidance and decisions reserved for planning.

**Coverage status.** All catalog and feature-local cases above are pending executable tests. A01–A10 are local acceptance references. The existing [scenario catalog](../../e2e/fixtures/scenarios.md) owns the shared IDs. INT-01 through INT-06 apply to all intake paths, with Salou first. H10-01 through H10-04 and CAS-01 through CAS-04 await their respective adapter contracts. BRK-01 through BRK-04 and KFK-01 through KFK-03 await feature 003. PDS-01 through PDS-07 describe shared outcomes here and await feature 004 for runtime verification. Feature 002 covers the local stand and 005 covers coverage visualization. The plan selects the test framework and scenario-to-result mapping.

**Review materials.** Compare the existing and proposed intake and acknowledgment order in the [flow diagram](../../diagrams/001-booking-contracts/flow.drawio). Review the independent reservation, participation, attendance, operation and public status rules in the [states diagram](../../diagrams/001-booking-contracts/states.drawio). The Owner approved the written specification and diagrams at G1 on 2026-11-24.
