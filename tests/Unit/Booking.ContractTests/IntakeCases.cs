using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Booking.Application.Salou;
using Booking.Contracts;
using Booking.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Booking.ContractTests;

[TestClass]
public sealed partial class ContractCases
{
    private static readonly string IntakeFixtures = Path.Combine(AppContext.BaseDirectory, "fixtures", "intake");
    private static readonly DateTimeOffset Now = Instant("2026-10-25T00:30:00Z");
    private static readonly DateTimeOffset Deadline = Instant("2026-11-24T23:00:00Z");
    private static readonly HotelIntegration Hotel = ConfiguredHotel();

    private static HotelIntegration ConfiguredHotel()
    {
        Assert.IsTrue(HotelIntegration.TryConfigure("salou", ["salou-vip"], [HotelAction.Import, HotelAction.Update],
            "Europe/Madrid", RegistrationPolicy.DefaultGpds, "gpds", out var integration), "hotel-configuration");
        return integration!;
    }
    private static HotelScope Scope => Hotel.SinglePropertyScope()!;
    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-7000-8000-{n:D12}");
    private static JsonNode Wire() => JsonNode.Parse(File.ReadAllText(Path.Combine(IntakeFixtures, "restricted", "snapshot.json")))!;
    private static byte[] Bytes(JsonNode wire) => Encoding.UTF8.GetBytes(wire.ToJsonString());
    private static BookingDto Snapshot() => SalouSnapshotMapper.Decode(Bytes(Wire()), Scope, Hotel.LocalDate(Now)).Snapshot!;
    private static Func<Guid> Ids(int start = 1)
    {
        int next = start;
        return () => Id(next++);
    }

    private static JsonArray CaseRows(string method)
    {
        var file = JsonNode.Parse(File.ReadAllText(Path.Combine(IntakeFixtures, method + ".json")))!;
        return file["cases"]!.AsArray();
    }

    private static void Cases(string method, Action<string, string, int> check)
    {
        foreach (var row in CaseRows(method))
            check((string)row!["caseId"]!, (string)row["effect"]!, (int)row["status"]!);
    }

    private static IntakeDecision Decide(BookingDto incoming, AcceptedBooking? previous = null, bool put = false,
        DateTimeOffset? now = null, ReconciliationIntent? intent = null) =>
        BookingIntake.Decide(incoming, put ? Id(1) : null, previous, Hotel.Zone, now ?? Now, Ids(previous == null ? 1 : 10), intent);

    private static AcceptedBooking Prior() => Decide(Snapshot()).Accepted!;
    private static void Verify(string caseId, string effect, int status, IntakeDecision decision, bool committed = true)
    {
        Assert.AreEqual(effect, decision.Kind.ToString(), true, caseId);
        var response = IntakeResponse.From(decision, committed);
        Assert.AreEqual(status, response.Status, caseId);
        Assert.AreEqual(effect is "create" or "apply", decision.Event != null, caseId);
        if (status == 409 && decision.IsPost)
            Assert.AreEqual($"/api/v1/bookings/{Id(1)}", JsonSerializer.SerializeToNode(response.Body)!["updatePath"]!.GetValue<string>(), caseId);
        if (status == 410 || status == 503)
            Assert.AreEqual(status == 410 ? "identity_erased" : "temporarily_unavailable",
                JsonSerializer.SerializeToNode(response.Body)!["code"]!.GetValue<string>(), caseId);
    }

    [TestMethod, TestCategory("INT-01"), TestCategory("SAL-01"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/FirstImport.json", "intake/restricted/snapshot.json", "intake/expected-mappings.json")]
    public void FirstImport()
    {
        Cases(nameof(FirstImport), (id, effect, status) =>
        {
            var now = id switch { "first-at" => Deadline, "first-after" => Deadline.AddTicks(1), "first-before" => Deadline.AddTicks(-1), _ => Now };
            var result = Decide(Snapshot(), now: now);
            Verify(id, effect, status, result, id != "first-failed");
            if (status != 201) return;
            var throughAdapter = SalouIntake.Evaluate(Hotel, null, null, Bytes(Wire()), now, Ids());
            Assert.IsNull(throughAdapter.Rejection, id);
            Assert.AreEqual(IntakeKind.Create, throughAdapter.Decision!.Kind, id);
            var response = IntakeResponse.From(result, true);
            Assert.AreEqual($"/api/v1/bookings/{Id(1)}", response.Location, id);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(IntakeFixtures, "expected-mappings.json"))),
                JsonSerializer.SerializeToNode(response.Body)), id);
            Assert.AreEqual("salou:salou-vip:sal%3A%25%2F%C3%A9", Snapshot().Key.Encode(), id);
            Assert.IsTrue(JsonSerializer.SerializeToNode(result.Event)!.AsObject().Select(p => p.Key).ToHashSet()
                .SetEquals(["eventId", "bookingId", "snapshotId"]), id);
            Assert.AreEqual(result.Accepted!.SnapshotId, result.Event!.SnapshotId, id);
        });
    }

    [TestMethod, TestCategory("INT-02"), TestCategory("SAL-01"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/DuplicateSnapshot.json", "intake/restricted/snapshot.json")]
    public void DuplicateSnapshot()
    {
        var confirmed = Snapshot();
        var cancelled = confirmed with { ReservationStatus = ReservationStatus.Cancelled, CancelledAt = Instant("2026-10-24T22:30:00.1234567Z") };
        Assert.AreEqual(Instant("2026-11-23T22:30:00.1234567Z"), BookingIntake.IdentityDeadline(cancelled, Hotel.Zone), "cancel-duration");
        var clocks = new[]
        {
            ("America/Sao_Paulo", new DateOnly(2018, 10, 5), "2018-11-04T03:00:00Z"),
            ("America/Havana", new DateOnly(2020, 10, 2), "2020-11-01T04:00:00Z")
        };
        foreach (var (zone, checkout, expected) in clocks)
            Assert.AreEqual(Instant(expected), BookingIntake.IdentityDeadline(confirmed with { DepartureDate = checkout },
                TimeZoneInfo.FindSystemTimeZoneById(zone)), "local-midnight-" + zone);

        Cases(nameof(DuplicateSnapshot), (id, effect, status) =>
        {
            var original = Wire().AsObject();
            var wire = new JsonObject(original.Reverse().Select(p => KeyValuePair.Create(p.Key, p.Value?.DeepClone())));
            wire["createdAt"] = "2026-08-01T00:00:00.0000000Z";
            wire["guests"] = new JsonArray(original["guests"]!.AsArray().Reverse().Select(g => g!.DeepClone()).ToArray());
            var reordered = SalouSnapshotMapper.Decode(Bytes(wire), Scope, Hotel.LocalDate(Now)).Snapshot!;
            var result = Decide(reordered, Prior(), id.EndsWith("put", StringComparison.Ordinal),
                id.StartsWith("erased", StringComparison.Ordinal) ? Deadline : Now);
            Verify(id, effect, status, result);
            Assert.AreEqual(effect == "open", result.ErasePriorIdentity, id);
        });
    }

    [TestMethod, TestCategory("INT-03"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/StaleUpdate.json", "intake/restricted/snapshot.json", "intake/expected-mappings.json")]
    public void StaleUpdate()
    {
        Cases(nameof(StaleUpdate), (id, effect, status) =>
        {
            var original = Snapshot();
            var newer = id == "one-tick-newer";
            var result = Decide(original with { UpdatedAt = original.UpdatedAt.AddTicks(newer ? 1 : -1) }, Prior(), true,
                id == "erased-stale" ? Deadline : Now);
            Verify(id, effect, status, result);
            Assert.AreEqual(original.UpdatedAt.AddTicks(newer ? 1 : 0), result.Accepted!.UpdatedAt, id);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(IntakeFixtures, "expected-mappings.json"))),
                JsonSerializer.SerializeToNode(IntakeResponse.From(result, true).Body)), id);
        });
    }

    [TestMethod, TestCategory("INT-04"), TestCategory("A01"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/ConflictingSnapshot.json", "intake/restricted/snapshot.json")]
    public void ConflictingSnapshot()
    {
        Cases(nameof(ConflictingSnapshot), (id, effect, status) =>
        {
            var input = Snapshot();
            var guest = input.Guests[0];
            input = input with { Guests = input.Guests.SetItem(0, guest with { FirstName = guest.FirstName.ToUpperInvariant() }) };
            var live = id.StartsWith("collision", StringComparison.Ordinal);
            var intent = new ReconciliationIntent(id == "other-scope" ? Scope with { PropertyId = "other" } : Scope,
                Id(1), id == "other-instant" ? input.UpdatedAt.AddTicks(-1) : input.UpdatedAt, id != "closed-intent",
                ReconciliationReason.ComparisonUnavailable);
            var result = Decide(input, Prior(), id != "collision-post", live ? Now : Deadline.AddTicks(1), live || id == "failed-handoff" ? null : intent);
            Verify(id, effect, status, result, id != "failed-handoff");
            Assert.AreEqual(live, result.RestrictedConflict != null, id);
            Assert.IsNotNull(result.Intent, id);
            Assert.AreEqual(503, IntakeResponse.From(result, false).Status, id);
        });
    }

    [TestMethod, TestCategory("A01"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/ExistingImportAndUnknownUpdate.json", "intake/restricted/snapshot.json")]
    public void ExistingImportAndUnknownUpdate()
    {
        Cases(nameof(ExistingImportAndUnknownUpdate), (id, effect, status) =>
        {
            var input = Snapshot();
            var prior = Prior();
            bool post = id.EndsWith("post", StringComparison.Ordinal);
            input = id switch
            {
                "immutable-created" => input with { CreatedAt = input.CreatedAt.AddTicks(1) },
                "immutable-source" => input with { Key = input.Key with { SrcBookingId = "different" } },
                "expired-proposal" => input with { UpdatedAt = input.UpdatedAt.AddTicks(1), ArrivalDate = new(2026, 8, 20), DepartureDate = new(2026, 8, 21) },
                "reactivate" => input with { UpdatedAt = input.UpdatedAt.AddTicks(1), DepartureDate = new(2026, 12, 12) },
                _ => input with { UpdatedAt = input.UpdatedAt.AddTicks(id.Contains("older", StringComparison.Ordinal) ? -1 : 1) }
            };
            var at = id is "erased-older-post" or "reactivate" ? Deadline : Now;
            var result = Decide(input, id == "unknown-put" ? null : prior, !post, at);
            Verify(id, effect, status, result);
            if (id == "expired-proposal") Assert.AreSame(prior, result.Accepted, id);
            if (id == "reactivate")
            {
                Assert.IsTrue(result.ErasePriorIdentity, id);
                Assert.AreNotEqual(prior.SnapshotId, result.Accepted!.SnapshotId, id);
            }
        });
    }

    [TestMethod, TestCategory("INT-05"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "intake/InvalidSnapshot.json", "intake/restricted/snapshot.json")]
    public void InvalidSnapshot()
    {
        foreach (var row in CaseRows(nameof(InvalidSnapshot)))
        {
            string id = (string)row!["caseId"]!;
            int status = (int)row["status"]!;
            var body = Wire();
            var guests = body["guests"]!.AsArray();
            var guest = guests[0]!.AsObject();
            switch (id)
            {
                case "missing-passport": guest.Remove("passport"); break;
                case "bad-country": guest["passport"]!["issuingCountry"] = "ZZ"; break;
                case "duplicate-guest": guests[1]!["srcGuestId"] = guest["srcGuestId"]!.DeepClone(); break;
                case "tomorrow": guest["dateOfBirth"] = "2026-10-26"; break;
                case "unknown-member": guest["untrusted-sensitive-field"] = true; break;
                case "invalid-date": body["departureDate"] = "2026-02-30"; break;
                case "missing-status": body.AsObject().Remove("reservationStatus"); break;
                case "null-id": body["srcBookingId"] = null; break;
                case "eight-digits": body["updatedAt"] = "2026-09-01T00:00:00.12345678Z"; break;
                case "offset": body["createdAt"] = "2026-08-01T00:00:00+00:00"; break;
                case "too-many-scalars": guest["firstName"] = string.Concat(Enumerable.Repeat("😀", 101)); break;
                case "too-many-guests": while (guests.Count < 101) guests.Add(guest.DeepClone()); break;
                case "bad-range": body["departureDate"] = body["arrivalDate"]!.DeepClone(); break;
                case "created-later": body["createdAt"] = "2026-10-01T00:00:00Z"; break;
                case "cancel-missing": body["reservationStatus"] = "cancelled"; break;
                case "confirmed-instant": body["cancelledAt"] = body["updatedAt"]!.DeepClone(); break;
            }
            byte[] bytes = id switch { "malformed" => [0x7B], "oversized" => new byte[1048577], _ => Bytes(body) };
            var parsed = SalouSnapshotMapper.Decode(bytes, Scope, Hotel.LocalDate(Now));
            Assert.IsNull(parsed.Snapshot, id);
            Assert.AreEqual(status, parsed.Error!.Status, id);
            Assert.AreEqual((string)row["effect"]! == "large" ? "body_too_large" : "invalid_snapshot", parsed.Error.Body.Code, id);
            Assert.AreEqual(1, parsed.Error.Body.Fields.Length, id);
            var issue = parsed.Error.Body.Fields[0];
            Assert.IsTrue(issue.Path == (string)row["fieldPath"]! && issue.Code == (string)row["fieldCode"]!, id);
            Assert.IsFalse(JsonSerializer.Serialize(parsed.Error).Contains("untrusted-sensitive-field", StringComparison.Ordinal), id);
            if (id is "malformed" or "oversized") VerifyRejectedPut(id, bytes, status);
        }
        Assert.AreEqual(401, SalouIntake.Evaluate(null, null, null, ReadOnlyMemory<byte>.Empty, Now).Rejection!.Status, "credentials-first");
        Assert.AreEqual(404, SalouIntake.Evaluate(Hotel, Id(7), Prior(), ReadOnlyMemory<byte>.Empty, Now).Rejection!.Status, "scope-first");
        Assert.IsFalse(HotelIntegration.TryConfigure("salou", ["salou-vip"], [], "CET", RegistrationPolicy.Disabled,
            null, out _), "invalid-config");
        var valid = Wire();
        valid["guests"]![0]!["firstName"] = string.Concat(Enumerable.Repeat("😀", 100));
        valid["guests"]![0]!["dateOfBirth"] = "2026-10-25";
        var decoded = SalouSnapshotMapper.Decode(Bytes(valid), Scope, Hotel.LocalDate(Instant("2026-10-24T22:30:00Z")));
        Assert.IsNotNull(decoded.Snapshot, "local-today");
        Assert.AreEqual("BookingDto", decoded.Snapshot.ToString());
        Assert.AreEqual("Guest", decoded.Snapshot.Guests[0].ToString());
        Assert.AreEqual("Passport", decoded.Snapshot.Guests[0].Passport!.ToString());
        Assert.IsTrue(HotelIntegration.TryZone("Europe/Madrid", out _), "madrid-icu");
        Assert.IsTrue(HotelIntegration.TryZone("Europe/Paris", out _), "paris-icu");
        Assert.IsFalse(HotelIntegration.TryZone("CET", out _), "alias");
        Assert.AreEqual(Access.Unauthenticated, HotelIntegration.Authorize(null, HotelAction.Import, Scope));
        Assert.AreEqual(Access.Forbidden, HotelIntegration.Authorize(Hotel, HotelAction.ManageSubscriptions, Scope));
        Assert.AreEqual(Access.NotFound, HotelIntegration.Authorize(Hotel, HotelAction.Update, Scope, false));
    }

    private static void VerifyRejectedPut(string id, byte[] body, int expectedStatus)
    {
        var prior = Prior();
        foreach (var now in new[] { Deadline.AddTicks(-1), Deadline })
        {
            var result = SalouIntake.Evaluate(Hotel, prior.BookingId, prior, body, now);
            Assert.AreEqual(expectedStatus, result.Rejection!.Status, id);
            Assert.AreEqual(expectedStatus == 413 ? "body_too_large" : "invalid_snapshot",
                ((SafeError)result.Rejection.Body).Code, id);
            Assert.IsNull(result.Decision, id);
            if (now < Deadline) Assert.IsNull(result.ErasureOnRejection, id);
            else Assert.IsTrue(result.ErasureOnRejection == (prior with { Restricted = null }), id);
        }

        var foreign = prior with { Key = prior.Key with { Scope = Scope with { PropertyId = "other" } } };
        foreach (var candidate in new AcceptedBooking?[] { foreign, prior with { BookingId = Id(7) }, null })
        {
            var result = SalouIntake.Evaluate(Hotel, prior.BookingId, candidate, body, Deadline);
            Assert.AreEqual(404, result.Rejection!.Status, id);
            Assert.IsNull(result.Decision, id);
            Assert.IsNull(result.ErasureOnRejection, id);
        }
    }
}
