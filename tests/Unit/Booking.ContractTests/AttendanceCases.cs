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

public sealed partial class ContractCases
{
    private static readonly string AttendanceFixtures = Path.Combine(AppContext.BaseDirectory, "fixtures", "attendance");

    [TestMethod, TestCategory("A02"), TestCategory("A03"), TestCategory("Evidence:contract")]
    public void GuestMembership()
    {
        var hotel = AttendanceHotel();
        var source = Snapshot();
        var first = source.Guests[0];
        var second = source.Guests[1];
        var third = first with { SrcGuestId = "source-third" };
        var fourth = first with { SrcGuestId = "source-fourth" };
        var sourceIds = new[] { first.SrcGuestId, second.SrcGuestId, third.SrcGuestId, fourth.SrcGuestId };
        var booking = Prior();
        var knownIds = new List<Guid> { booking.GuestIds[first.SrcGuestId], booking.GuestIds[second.SrcGuestId] };
        var firstId = knownIds[0];
        var allocate = Ids(20);
        var noShow = Report(hotel, booking, firstId, [], "noShow", Now.AddMinutes(-1), allocate: allocate);
        VerifyAttendanceResponse(noShow, 201, "initial-no-show");
        ImmutableArray<AttendanceEvent> history = [noShow.Append!];

        foreach (var row in AttendanceFixture(nameof(GuestMembership))["cases"]!.AsArray())
        {
            string id = (string)row!["caseId"]!;
            string snapshot = (string)row["snapshot"]!;
            bool stale = (string?)row["freshness"] == "stale";
            ImmutableArray<Guest> guests = snapshot switch
            {
                "omit-first" => [second],
                "both" => [first, second],
                "add-third" => [first, second, third],
                "cancelled" => [first, second, third, fourth],
                "confirm-first-and-fourth" => [first, fourth],
                _ => throw new InvalidOperationException("Unknown membership case.")
            };
            bool cancelled = snapshot == "cancelled";
            var incoming = source with
            {
                Guests = guests,
                UpdatedAt = booking.UpdatedAt.AddTicks(stale ? -1 : 1),
                ReservationStatus = cancelled ? ReservationStatus.Cancelled : ReservationStatus.Confirmed,
                CancelledAt = cancelled ? Now : null
            };
            if (cancelled)
            {
                var checkIn = Report(hotel, booking, firstId, history, "checkedIn", Now, allocate: allocate);
                VerifyAttendanceResponse(checkIn, 201, id);
                Assert.AreEqual(2, checkIn.Append!.Episode, id);
                history = history.Add(checkIn.Append);
            }

            var decision = BookingIntake.Decide(incoming, booking.BookingId, booking, Hotel.Zone, Now, allocate);
            Assert.AreEqual(stale ? IntakeKind.Stale : IntakeKind.Apply, decision.Kind, id);
            booking = decision.Accepted!;
            Assert.AreEqual(ReservationStatusFor(snapshot), booking.Membership.ReservationStatus, id);

            var active = row["active"]!.AsArray();
            var episodes = row["episodes"]!.AsArray();
            var members = booking.Membership.Participants;
            Assert.AreEqual(active.Count, members.Count, id);
            for (int index = 0; index < active.Count; index++)
            {
                var actualId = booking.GuestIds[sourceIds[index]];
                if (index < knownIds.Count) Assert.AreEqual(knownIds[index], actualId, id);
                else
                {
                    Assert.DoesNotContain(actualId, knownIds, id);
                    knownIds.Add(actualId);
                }
                Assert.AreEqual((bool)active[index]!, members[actualId].ActiveEpisode.HasValue, id);
                Assert.AreEqual((int)episodes[index]!, members[actualId].Episodes.Length, id);
            }
            Assert.IsTrue(booking.Mappings.Guests.Select(mapping => mapping.GuestId).SequenceEqual(
                booking.Restricted!.Guests.Select(guest => booking.GuestIds[guest.SrcGuestId])), id);
            var expectedChanges = row["changes"]!.AsArray().Select(change => (string)change!).ToArray();
            var observedChanges = decision.ParticipationChanges.Select(change =>
                $"{change.Work}:{knownIds.IndexOf(change.GuestId) + 1}").ToArray();
            CollectionAssert.AreEquivalent(expectedChanges, observedChanges, id);

            var firstMembership = members[firstId];
            Assert.AreEqual(ExpectedStatus((string)row["attendance"]!),
                Attendance.Status(history, booking.BookingId, firstId, firstMembership.Episodes.Length), id);
            Assert.AreEqual(AttendanceStatus.NoShow, Attendance.Status(history, booking.BookingId, firstId, 1), id);
            var retry = Report(hotel, booking, firstId, history, "noShow", Now.AddMinutes(-1), receivedAt: Now.AddSeconds(1));
            VerifyAttendanceResponse(retry, 200, id, noShow.Append!.AttendanceEventId);
            if (!firstMembership.ActiveEpisode.HasValue)
                VerifyAttendanceResponse(Report(hotel, booking, firstId, history, "arrived", Now.AddMinutes(1)), 409, id);
        }
        Assert.AreEqual(AttendanceStatus.CheckedIn, Attendance.Status(history, booking.BookingId, firstId, 2), "closed-checked-in-episode");
        Assert.IsTrue(BookingIntake.EraseIdentityIfDue(booking, booking.Deadline!.Value) is { Restricted: null } erased &&
            erased.Membership == booking.Membership, "preserve-membership-after-erasure");
    }

    [TestMethod, TestCategory("A04"), TestCategory("Evidence:contract")]
    public void AttendanceChronology()
    {
        var fixture = AttendanceFixture(nameof(AttendanceChronology));
        var instant = Instant((string)fixture["baseInstant"]!);
        var hotel = AttendanceHotel();
        var original = Prior();
        var otherSource = Snapshot();
        var other = BookingIntake.Decide(otherSource with { Key = otherSource.Key with { SrcBookingId = "other-booking" } },
            null, null, Hotel.Zone, Now, Ids(101)).Accepted!;
        foreach (var row in fixture["cases"]!.AsArray())
        {
            string id = (string)row!["caseId"]!;
            ImmutableArray<AttendanceEvent> history = [];
            var receipts = new List<Guid?>();
            var allocate = Ids(200);
            foreach (var step in row["steps"]!.AsArray())
            {
                var booking = (bool?)step!["otherBooking"] == true ? other : original;
                var guest = booking.Mappings.Guests[((int?)step["guest"] ?? 1) - 1].GuestId;
                var status = (string)step["status"]!;
                var occurredAt = instant.AddTicks((int)step["tick"]!);
                var receivedAt = Now.AddSeconds(receipts.Count);
                string actor = (string?)step["actor"] ?? "attendance-actor";
                var result = Report(hotel, booking, guest, history, status, occurredAt, actor, receivedAt, allocate);
                VerifyAttendanceResponse(result, (int)step["http"]!, id, RetryId(step, receipts));
                if (result.Append is { } entry)
                {
                    Assert.IsTrue(entry.BookingId == booking.BookingId && entry.GuestId == guest && entry.Episode == 1 &&
                        entry.Actor == actor && entry.Observation == new AttendanceObservation(ExpectedStatus(status), occurredAt) &&
                        entry.ReceivedAt == receivedAt && entry.OriginalEventId is null && entry.ReasonCode is null, id);
                    history = history.Add(entry);
                    Assert.AreEqual(ExpectedStatus(status), Attendance.Status(history, booking.BookingId, guest, 1), id);
                }
                receipts.Add((result.Body as AttendanceReceipt)?.AttendanceEventId);
            }
        }

        var guestId = original.Mappings.Guests[0].GuestId;
        foreach (var row in fixture["invalid"]!.AsArray())
        {
            string id = (string)row!["caseId"]!;
            var body = row["body"] is { } text ? Encoding.UTF8.GetBytes((string)text!) : new byte[1048577];
            var result = AttendanceContracts.Evaluate(hotel, "attendance-actor", original.BookingId, guestId, null,
                original, [], body, Now);
            VerifyAttendanceResponse(result, (int)row["http"]!, id);
            var issue = ((SafeError)result.Body).Fields.Single();
            Assert.IsTrue(issue.Path == (string)row["path"]! && issue.Code == (string)row["fieldCode"]!, id);
            Assert.DoesNotContain("untrusted-field", JsonSerializer.Serialize(result.Body), id);
        }
        var wholeSecond = AttendanceContracts.Evaluate(hotel, "attendance-actor", original.BookingId, guestId, null,
            original, [], """{"status":"arrived","occurredAt":"2026-10-25T00:29:00Z"}"""u8.ToArray(), Now, Ids(200));
        VerifyAttendanceResponse(wholeSecond, 201, "whole-second");
        VerifyAttendanceResponse(Report(hotel, original, guestId, [wholeSecond.Append!], "arrived", instant),
            200, "equivalent-occurrence", wholeSecond.Append!.AttendanceEventId);
    }

    [TestMethod, TestCategory("A05"), TestCategory("Evidence:contract")]
    public void CorrectAttendance()
    {
        var fixture = AttendanceFixture(nameof(CorrectAttendance));
        var instant = Instant((string)fixture["baseInstant"]!);
        var hotel = AttendanceHotel();
        foreach (var row in fixture["cases"]!.AsArray())
        {
            string id = (string)row!["caseId"]!;
            var booking = Prior();
            var guestId = booking.Mappings.Guests[0].GuestId;
            ImmutableArray<AttendanceEvent> history = [];
            var allocate = Ids(300);
            foreach (var report in row["reports"]!.AsArray())
            {
                var result = Report(hotel, booking, guestId, history, (string)report!["status"]!,
                    instant.AddTicks((int)report["tick"]!), allocate: allocate);
                VerifyAttendanceResponse(result, 201, id);
                history = history.Add(result.Append!);
            }
            var originals = history;
            if ((bool?)row["restore"] == true) booking = RestoreFirstGuest(booking);

            var receipts = new List<Guid?>();
            foreach (var step in row["steps"]!.AsArray())
            {
                var original = originals[(int)step!["target"]!];
                var occurredAt = instant.AddTicks((int)step["tick"]!);
                var receivedAt = Now.AddSeconds(receipts.Count);
                string status = (string)step["status"]!;
                string reason = (string)step["reason"]!;
                string actor = (string?)step["actor"] ?? "correction-actor";
                var result = AttendanceContracts.Evaluate(hotel, actor, booking.BookingId, guestId,
                    original.AttendanceEventId, booking, history, AttendanceBody(status, occurredAt, reason), receivedAt, allocate);
                VerifyAttendanceResponse(result, (int)step["http"]!, id, RetryId(step, receipts));
                if (result.Append is { } entry)
                {
                    Assert.IsTrue(entry.BookingId == booking.BookingId && entry.GuestId == guestId && entry.Episode == original.Episode &&
                        entry.Actor == actor && entry.OriginalEventId == original.AttendanceEventId && entry.ReceivedAt == receivedAt &&
                        entry.Observation == new AttendanceObservation(ExpectedStatus(status), occurredAt), id);
                    string reasonName = reason.Replace("_", "", StringComparison.Ordinal);
                    Assert.AreEqual(reasonName, entry.ReasonCode.ToString(), true, id);
                    history = history.Add(entry);
                }
                var episode = booking.Membership.Participants[guestId].ActiveEpisode!.Value;
                Assert.AreEqual(ExpectedStatus((string)step["effective"]!), Attendance.Status(history, booking.BookingId, guestId, episode), id);
                if (step["historicalEffective"] is { } previous)
                    Assert.AreEqual(ExpectedStatus((string)previous!), Attendance.Status(history, booking.BookingId, guestId, original.Episode), id);
                Assert.IsTrue(history.Take(originals.Length).SequenceEqual(originals), id);
                receipts.Add((result.Body as AttendanceReceipt)?.AttendanceEventId);
            }
        }
        VerifyCorrectionScope(instant);
    }

    private static void VerifyCorrectionScope(DateTimeOffset instant)
    {
        var booking = Prior();
        var guest = booking.Mappings.Guests[0].GuestId;
        var reporter = AttendanceHotel([HotelAction.Attendance]);
        var supervisor = AttendanceHotel([HotelAction.CorrectAttendance]);
        var foreign = AttendanceHotel([HotelAction.CorrectAttendance], "other-property");
        var original = Report(reporter, booking, guest, [], "arrived", instant, allocate: Ids(400)).Append!;
        ImmutableArray<AttendanceEvent> history = [original];
        var target = original.AttendanceEventId;
        var correction = AttendanceBody("noShow", instant, "incorrect_status");
        AttendanceResult Correct(HotelIntegration? hotel, Guid bookingId, Guid guestId, Guid originalEventId, byte[] body) =>
            AttendanceContracts.Evaluate(hotel, "correction-actor", bookingId, guestId, originalEventId,
                booking, history, body, Now, Ids(401));

        VerifyAttendanceResponse(Correct(null, booking.BookingId, guest, target, []), 401, "correction-credential");
        VerifyAttendanceResponse(Correct(reporter, booking.BookingId, guest, Id(900), []), 403, "correction-permission-first");
        VerifyAttendanceResponse(Correct(foreign, booking.BookingId, guest, target, []), 404, "foreign-hotel");
        VerifyAttendanceResponse(Correct(supervisor, Id(900), guest, target, []), 404, "wrong-booking");
        VerifyAttendanceResponse(Correct(supervisor, booking.BookingId, booking.Mappings.Guests[1].GuestId, target, []),
            404, "target-outside-guest");
        VerifyAttendanceResponse(Correct(supervisor, booking.BookingId, guest, Id(900), []), 404, "unknown-target");
        VerifyAttendanceResponse(Report(supervisor, booking, guest, history, "checkedIn", instant.AddTicks(1)), 403, "report-permission");
        VerifyAttendanceResponse(Report(reporter, booking, Id(900), history, "checkedIn", instant.AddTicks(1)), 404, "wrong-guest");

        var badReason = Correct(supervisor, booking.BookingId, guest, target, AttendanceBody("arrived", instant, "unknown-reason"));
        VerifyAttendanceResponse(badReason, 400, "invalid-correction-reason");
        Assert.AreEqual("$.reasonCode", ((SafeError)badReason.Body).Fields.Single().Path, "reason-path");
        Assert.DoesNotContain("unknown-reason", JsonSerializer.Serialize(badReason.Body), "safe-reason-error");
        var missingReason = Correct(supervisor, booking.BookingId, guest, target, AttendanceBody("arrived", instant));
        VerifyAttendanceResponse(missingReason, 400, "missing-correction-reason");
        Assert.AreEqual("required", ((SafeError)missingReason.Body).Fields.Single().Code, "missing-reason-code");
        VerifyAttendanceResponse(Correct(supervisor, booking.BookingId, guest, target, new byte[1048577]), 413, "correction-size");
        var appended = Correct(supervisor, booking.BookingId, guest, target, correction);
        VerifyAttendanceResponse(appended, 201, "correction-permission-is-separate");
        Assert.AreEqual("AttendanceEvent", appended.Append!.ToString(), "safe-event-format");
        history = history.Add(appended.Append);
        VerifyAttendanceResponse(Correct(supervisor, booking.BookingId, guest, appended.Append.AttendanceEventId, correction),
            404, "target-original-event");
        VerifyAttendanceResponse(Report(reporter, booking, guest, history, "arrived", instant.AddTicks(1)),
            201, "ordinary-report-follows-corrected-status");
        VerifyAttendanceResponse(Report(reporter, booking, guest, history, "arrived", instant),
            200, "original-report-remains-a-retry", original.AttendanceEventId);
    }

    private static AttendanceResult Report(HotelIntegration hotel, AcceptedBooking booking, Guid guestId,
        ImmutableArray<AttendanceEvent> history, string status, DateTimeOffset occurredAt, string actor = "attendance-actor",
        DateTimeOffset? receivedAt = null, Func<Guid>? allocate = null) =>
        AttendanceContracts.Evaluate(hotel, actor, booking.BookingId, guestId, null, booking, history,
            AttendanceBody(status, occurredAt), receivedAt ?? Now, allocate);

    private static AcceptedBooking RestoreFirstGuest(AcceptedBooking booking)
    {
        var source = booking.Restricted!;
        var omit = source with { Guests = source.Guests.RemoveAt(0), UpdatedAt = source.UpdatedAt.AddTicks(1) };
        var cancelled = BookingIntake.Decide(omit, booking.BookingId, booking, Hotel.Zone, Now, Ids(51)).Accepted!;
        return BookingIntake.Decide(source with { UpdatedAt = omit.UpdatedAt.AddTicks(1) }, booking.BookingId, cancelled,
            Hotel.Zone, Now, Ids(61)).Accepted!;
    }

    private static void VerifyAttendanceResponse(AttendanceResult result, int expected, string id, Guid? retryId = null)
    {
        Assert.AreEqual(expected, result.Status, id);
        Assert.AreEqual(expected == 201, result.Append is not null, id);
        if (expected is 200 or 201)
        {
            var eventId = expected == 200 ? retryId!.Value : result.Append!.AttendanceEventId;
            Assert.IsTrue(JsonNode.DeepEquals(new JsonObject { ["attendanceEventId"] = eventId.ToString("D") },
                JsonSerializer.SerializeToNode(result.Body)), id);
            return;
        }
        var code = expected switch
        {
            400 => "invalid_request",
            401 => "unauthenticated",
            403 => "forbidden",
            404 => "not_found",
            409 => "attendance_conflict",
            413 => "body_too_large",
            _ => throw new InvalidOperationException("Unknown expected attendance response.")
        };
        Assert.AreEqual(code, ((SafeError)result.Body).Code, id);
    }

    private static Guid? RetryId(JsonNode step, List<Guid?> receipts) =>
        (int?)step["retryOf"] is { } index ? receipts[index] : null;

    private static AttendanceStatus ExpectedStatus(string status) => Enum.Parse<AttendanceStatus>(status, ignoreCase: true);

    private static ReservationStatus ReservationStatusFor(string snapshot) =>
        snapshot == "cancelled" ? ReservationStatus.Cancelled : ReservationStatus.Confirmed;

    private static JsonNode AttendanceFixture(string method) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AttendanceFixtures, method + ".json")))!;

    private static byte[] AttendanceBody(string status, DateTimeOffset occurredAt, string? reason = null)
    {
        var body = new JsonObject
        {
            ["status"] = status,
            ["occurredAt"] = occurredAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)
        };
        if (reason is not null) body["reasonCode"] = reason;
        return Bytes(body);
    }

    private static HotelIntegration AttendanceHotel(HotelAction[]? actions = null, string property = "salou-vip")
    {
        Assert.IsTrue(HotelIntegration.TryConfigure("salou", [property], actions ?? [HotelAction.Attendance, HotelAction.CorrectAttendance],
            "Europe/Madrid", RegistrationPolicy.Disabled, null, out var integration), "attendance-hotel");
        return integration!;
    }
}
