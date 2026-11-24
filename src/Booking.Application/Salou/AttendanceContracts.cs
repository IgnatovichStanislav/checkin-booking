using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Booking.Domain;
using static Booking.Application.Salou.SalouJson;

namespace Booking.Application.Salou;

public sealed record AttendanceReceipt([property: JsonPropertyName("attendanceEventId")] Guid AttendanceEventId);

public sealed record AttendanceResult(int Status, object Body, AttendanceEvent? Append = null);

public static class AttendanceContracts
{
    public static AttendanceResult Evaluate(HotelIntegration? verified, string actor, Guid bookingId, Guid guestId,
        Guid? originalEventId, AcceptedBooking? booking, ImmutableArray<AttendanceEvent> history,
        ReadOnlyMemory<byte> body, DateTimeOffset receivedAt, Func<Guid>? allocate = null)
    {
        bool correcting = originalEventId.HasValue;
        var action = correcting ? HotelAction.CorrectAttendance : HotelAction.Attendance;
        var access = HotelIntegration.Authorize(string.IsNullOrWhiteSpace(actor) ? null : verified, action,
            booking?.Key.Scope, booking?.BookingId == bookingId);
        if (access == Access.Unauthenticated) return Error(401, "unauthenticated");
        if (access == Access.Forbidden) return Error(403, "forbidden");
        if (access == Access.NotFound || !booking!.Membership.Participants.ContainsKey(guestId) ||
            (originalEventId is { } target && Attendance.FindOriginal(history, bookingId, guestId, target) is null))
            return Error(404, "not_found");

        if (body.Length > MaximumBytes) return Error(413, "body_too_large", new("$", "size_limit"));
        AttendanceObservation observation;
        CorrectionReason reason = default;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (correcting) Shape(root, "$", "status", "occurredAt", "reasonCode");
            else Shape(root, "$", "status", "occurredAt");

            observation = new(ReadStatus(root, correcting), Instant(root, "occurredAt"));
            if (correcting) reason = ReadReason(root);
        }
        catch (InvalidField invalid) { return Error(400, "invalid_request", invalid.Issue); }
        catch (JsonException) { return Error(400, "invalid_request", new("$", "invalid_json")); }
        catch (InvalidOperationException) { return Error(400, "invalid_request", new("$", "invalid_encoding")); }

        var decision = originalEventId is { } eventId
            ? Attendance.Correct(booking, guestId, eventId, history, actor, observation, reason, receivedAt, allocate)
            : Attendance.Report(booking, guestId, history, actor, observation, receivedAt, allocate);
        return decision.Kind switch
        {
            AttendanceKind.Append => new(201, new AttendanceReceipt(decision.AttendanceEventId!.Value), decision.Append),
            AttendanceKind.Duplicate => new(200, new AttendanceReceipt(decision.AttendanceEventId!.Value)),
            AttendanceKind.NotFound => Error(404, "not_found"),
            _ => Error(409, "attendance_conflict")
        };
    }

    private static AttendanceStatus ReadStatus(JsonElement root, bool correcting) => Text(root, "$", "status", 10) switch
    {
        "expected" when correcting => AttendanceStatus.Expected,
        "arrived" => AttendanceStatus.Arrived,
        "checkedIn" => AttendanceStatus.CheckedIn,
        "checkedOut" => AttendanceStatus.CheckedOut,
        "noShow" => AttendanceStatus.NoShow,
        _ => throw new InvalidField("$.status", "unsupported_status")
    };

    private static CorrectionReason ReadReason(JsonElement root) => Text(root, "$", "reasonCode", 16) switch
    {
        "incorrect_report" => CorrectionReason.IncorrectReport,
        "incorrect_status" => CorrectionReason.IncorrectStatus,
        "incorrect_time" => CorrectionReason.IncorrectTime,
        _ => throw new InvalidField("$.reasonCode", "unsupported_reason")
    };

    private static AttendanceResult Error(int status, string code, FieldIssue? field = null) =>
        new(status, new SafeError(code, field is null ? [] : [field]));
}
