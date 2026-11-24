using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Booking.Contracts;
using Booking.Domain;

namespace Booking.Application.Salou;

public sealed record FieldIssue([property: JsonPropertyName("path")] string Path, [property: JsonPropertyName("code")] string Code);
public sealed record SafeError([property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("fields")] ImmutableArray<FieldIssue> Fields);
public sealed record ExistingImportConflict([property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("fields")] ImmutableArray<FieldIssue> Fields, [property: JsonPropertyName("updatePath")] string UpdatePath);

public sealed record IntakeResponse(int Status, object Body, string? Location = null)
{
    public static IntakeResponse Error(int status, string code) => new(status, new SafeError(code, []));

    public static IntakeResponse From(IntakeDecision decision, bool durableAcceptanceConfirmed)
    {
        if (!durableAcceptanceConfirmed) return Error(503, "temporarily_unavailable");
        string Path() => $"/api/v1/bookings/{decision.Accepted!.BookingId:D}";
        return decision.Kind switch
        {
            IntakeKind.Create => new(201, decision.Accepted!.Mappings, Path()),
            IntakeKind.Apply or IntakeKind.Stale or IntakeKind.Duplicate => new(200, decision.Accepted!.Mappings),
            IntakeKind.UsePut or IntakeKind.Conflict when decision.IsPost => new(409, new ExistingImportConflict("use_put", [], Path())),
            IntakeKind.Conflict => Error(409, "snapshot_conflict"),
            IntakeKind.Open or IntakeKind.Join or IntakeKind.Expired => Error(410, "identity_erased"),
            IntakeKind.Missing => Error(404, "not_found"),
            IntakeKind.Invalid => new(400, new SafeError("invalid_snapshot", [new(decision.InvalidField!, "immutable")])),
            _ => Error(503, "temporarily_unavailable")
        };
    }
}

public sealed record SalouIntakeResult(IntakeResponse? Rejection, IntakeDecision? Decision)
{
    public override string ToString() => nameof(SalouIntakeResult);
}

public static class SalouIntake
{
    public static SalouIntakeResult Evaluate(HotelIntegration? verified, Guid? updateBookingId, AcceptedBooking? prior,
        ReadOnlyMemory<byte> body, DateTimeOffset now, Func<Guid>? allocate = null, ReconciliationIntent? existingIntent = null)
    {
        if (verified == null) return new(IntakeResponse.Error(401, "unauthenticated"), null);
        if (!verified.Permissions.Contains(updateBookingId.HasValue ? HotelAction.Update : HotelAction.Import))
            return new(IntakeResponse.Error(403, "forbidden"), null);
        if (verified.SinglePropertyScope() is not { } scope) return new(IntakeResponse.Error(503, "temporarily_unavailable"), null);
        if (updateBookingId.HasValue && (prior?.BookingId != updateBookingId || prior?.Key.Scope != scope))
            return new(IntakeResponse.Error(404, "not_found"), null);
        var decoded = SalouSnapshotMapper.Decode(body, scope, verified.LocalDate(now));
        if (decoded.Error is { } invalid) return new(new(invalid.Status, invalid.Body), null);
        return new(null, BookingIntake.Decide(decoded.Snapshot!, updateBookingId, prior, verified.Zone, now, allocate, existingIntent));
    }
}
