using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Booking.Contracts;
using Booking.Domain;

namespace Booking.Application.Salou;

public sealed record GuestStatus(
    [property: JsonPropertyName("guestId"), JsonRequired] Guid GuestId,
    [property: JsonPropertyName("srcGuestId"), JsonRequired] string SrcGuestId,
    [property: JsonPropertyName("participation"), JsonRequired] string Participation,
    [property: JsonPropertyName("attendance"), JsonRequired] string Attendance,
    [property: JsonPropertyName("operationId"), JsonRequired] Guid? OperationId,
    [property: JsonPropertyName("registrationStatus"), JsonRequired] string? RegistrationStatus,
    [property: JsonPropertyName("reasonCode"), JsonRequired] string? ReasonCode);

public sealed record OutstandingCancellation(
    [property: JsonPropertyName("guestId"), JsonRequired] Guid GuestId,
    [property: JsonPropertyName("operationId"), JsonRequired] Guid OperationId,
    [property: JsonPropertyName("registrationStatus"), JsonRequired] string RegistrationStatus,
    [property: JsonPropertyName("reasonCode"), JsonRequired] string? ReasonCode);

public sealed record BookingStatus(
    [property: JsonPropertyName("bookingId"), JsonRequired] Guid BookingId,
    [property: JsonPropertyName("srcBookingId"), JsonRequired] string SrcBookingId,
    [property: JsonPropertyName("reservationStatus"), JsonRequired] string ReservationStatus,
    [property: JsonPropertyName("activeGuestSummary"), JsonRequired] string? ActiveGuestSummary,
    [property: JsonPropertyName("guests"), JsonRequired] ImmutableArray<GuestStatus> Guests,
    [property: JsonPropertyName("outstandingCancellations"), JsonRequired] ImmutableArray<OutstandingCancellation> OutstandingCancellations);

public sealed record RegistrationNotification(
    [property: JsonPropertyName("notificationId"), JsonRequired] Guid NotificationId,
    [property: JsonPropertyName("bookingId"), JsonRequired] Guid BookingId,
    [property: JsonPropertyName("guestId"), JsonRequired] Guid GuestId,
    [property: JsonPropertyName("operationId"), JsonRequired] Guid OperationId,
    [property: JsonPropertyName("registrationStatus"), JsonRequired] string? RegistrationStatus,
    [property: JsonPropertyName("reasonCode"), JsonRequired] string? ReasonCode,
    [property: JsonPropertyName("occurredAt"), JsonRequired] DateTime OccurredAt);

public sealed record RegistrationChangeEffect(RegistrationHistory History, RegistrationNotification? Outbox);

public static class RegistrationStatus
{
    public static (string Status, string? Reason) PublicState(RegistrationOperation op) => (op.State, op.Reason) switch
    {
        (OperationState.Confirmed, _) => ("completed", null),
        (_, OperationReason.IdentityExpired) => ("failed", "identity_expired"),
        (OperationState.Failed, _) => ("failed", "intervention_required"),
        (_, OperationReason.CorrectData) => ("actionRequired", "correct_data"),
        (_, OperationReason.MissingPassport) => ("actionRequired", "missing_passport"),
        (_, OperationReason.PassportExpired) => ("actionRequired", "passport_expired"),
        _ => ("processing", null)
    };

    public static IntakeResponse Read(HotelIntegration? verified, Guid bookingId, AcceptedBooking? booking,
        IReadOnlyDictionary<Guid, RegistrationHistory> histories, ImmutableArray<AttendanceEvent> attendance)
    {
        var access = HotelIntegration.Authorize(verified, HotelAction.ReadStatus, booking?.Key.Scope, booking?.BookingId == bookingId);
        if (access != Access.Allowed) return access switch
        {
            Access.Unauthenticated => IntakeResponse.Error(401, "unauthenticated"),
            Access.Forbidden => IntakeResponse.Error(403, "forbidden"),
            _ => IntakeResponse.Error(404, "not_found")
        };
        var guests = ImmutableArray.CreateBuilder<GuestStatus>();
        var cancellations = ImmutableArray.CreateBuilder<OutstandingCancellation>();
        foreach (var (sourceId, guestId) in booking!.GuestIds.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var member = booking.Membership.Participants[guestId];
            histories.TryGetValue(guestId, out var history);
            var op = history?.Current;
            if (op?.BookingId != bookingId || op.GuestId != guestId || op.Scope != booking.Key.Scope) op = null;
            var (status, reason) = op == null ? (null, null) : PublicState(op);
            var attended = Attendance.Status(attendance, bookingId, guestId, member.Episodes.Length);
            guests.Add(new(guestId, sourceId, member.ActiveEpisode.HasValue ? "active" : "cancelled",
                JsonNamingPolicy.CamelCase.ConvertName(attended.ToString()), op?.OperationId, status, reason));
            if (op is { Kind: OperationKind.Cancel, State: not OperationState.Confirmed })
                cancellations.Add(new(guestId, op.OperationId, status!, reason));
        }
        var active = guests.Where(g => g.Participation == "active").Select(g => g.RegistrationStatus).ToArray();
        string? summary = new[] { "actionRequired", "failed", "processing" }.FirstOrDefault(active.Contains);
        if (summary == null && active.Length != 0 && active.All(s => s == "completed")) summary = "completed";
        return new(200, new BookingStatus(bookingId, booking.Key.SrcBookingId,
            booking.Membership.ReservationStatus == ReservationStatus.Confirmed ? "confirmed" : "cancelled",
            summary, guests.ToImmutable(), cancellations.ToImmutable()));
    }

    public static RegistrationChangeEffect Change(RegistrationHistory before, RegistrationHistory after, Guid changeId, DateTimeOffset at)
    {
        var prior = before.Current;
        var next = after.Current;
        if (next == null)
        {
            var stopped = after.Operations.LastOrDefault();
            if (prior == null || stopped is not { IsStopped: true } || stopped.OperationId != prior.OperationId) return new(after, null);
            return new(after, new(changeId, prior.BookingId, prior.GuestId, prior.OperationId, null, "registration_stopped", at.UtcDateTime));
        }
        var (status, reason) = PublicState(next);
        if (prior != null && (prior.Scope != next.Scope || prior.BookingId != next.BookingId || prior.GuestId != next.GuestId ||
            prior.OperationId == next.OperationId && PublicState(prior) == (status, reason))) return new(after, null);
        return new(after, new(changeId, next.BookingId, next.GuestId, next.OperationId, status, reason, at.UtcDateTime));
    }
}
