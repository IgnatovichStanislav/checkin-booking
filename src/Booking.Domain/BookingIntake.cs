using System.Collections.Immutable;
using Booking.Contracts;

namespace Booking.Domain;

public sealed record AcceptedBooking(Guid BookingId, Guid SnapshotId, SourceBookingKey Key, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? Deadline, ImmutableDictionary<string, Guid> GuestIds,
    Mappings Mappings, BookingDto? Restricted)
{
    public override string ToString() => nameof(AcceptedBooking);
}

public enum IntakeKind { Create, Apply, Duplicate, Stale, Conflict, UsePut, Expired, Open, Join, Missing, Invalid, Unavailable }
public enum ReconciliationReason { SnapshotConflict, ComparisonUnavailable }
public sealed record ReconciliationIntent(HotelScope Scope, Guid BookingId, DateTimeOffset SourceInstant, bool Open,
    ReconciliationReason Reason);

public sealed record IntakeDecision(IntakeKind Kind, bool IsPost, AcceptedBooking? Accepted = null,
    bool ErasePriorIdentity = false, BookingEvent? Event = null, ReconciliationIntent? Intent = null,
    BookingDto? RestrictedConflict = null, string? InvalidField = null)
{
    public override string ToString() => nameof(IntakeDecision);
}

public static class BookingIntake
{
    public static IntakeDecision Decide(BookingDto incoming, Guid? updateBookingId, AcceptedBooking? prior,
        TimeZoneInfo zone, DateTimeOffset now, Func<Guid>? allocate = null, ReconciliationIntent? existingIntent = null)
    {
        bool post = !updateBookingId.HasValue;
        if (prior != null && (prior.Key.Scope != incoming.Key.Scope ||
            (post ? prior.Key != incoming.Key : updateBookingId != prior.BookingId)))
            return new(post ? IntakeKind.Unavailable : IntakeKind.Missing, post);
        if (prior == null && !post) return new(IntakeKind.Missing, false);

        bool erase = prior is { Restricted: not null } && now >= prior.Deadline;
        if (erase) prior = prior! with { Restricted = null };
        IntakeDecision Result(IntakeKind kind, string? field = null) => new(kind, post, prior, erase, InvalidField: field);

        if (!post && prior != null)
        {
            if (prior.Key != incoming.Key) return Result(IntakeKind.Invalid, "$.srcBookingId");
            if (prior.CreatedAt != incoming.CreatedAt) return Result(IntakeKind.Invalid, "$.createdAt");
        }
        var incomingDeadline = IdentityDeadline(incoming, zone);
        if (prior != null)
        {
            int order = incoming.UpdatedAt.CompareTo(prior.UpdatedAt);
            if (order != 0 && post) return Result(IntakeKind.UsePut);
            if (order < 0) return Result(IntakeKind.Stale);
            if (order == 0)
            {
                if (prior.Restricted == null)
                {
                    var intent = new ReconciliationIntent(prior.Key.Scope, prior.BookingId, prior.UpdatedAt, true,
                        ReconciliationReason.ComparisonUnavailable);
                    return Result(intent == existingIntent ? IntakeKind.Join : IntakeKind.Open) with { Intent = intent };
                }
                if (SameBusinessData(prior.Restricted, incoming)) return Result(IntakeKind.Duplicate);
                return Result(IntakeKind.Conflict) with
                {
                    Intent = new(prior.Key.Scope, prior.BookingId, prior.UpdatedAt, true, ReconciliationReason.SnapshotConflict),
                    RestrictedConflict = now >= incomingDeadline ? null : incoming
                };
            }
        }
        if (now >= incomingDeadline) return Result(IntakeKind.Expired);
        allocate ??= Guid.CreateVersion7;
        Guid bookingId = prior?.BookingId ?? allocate();
        var ids = (prior?.GuestIds ?? ImmutableDictionary.Create<string, Guid>(StringComparer.Ordinal)).ToBuilder();
        var mappings = ImmutableArray.CreateBuilder<GuestMapping>(incoming.Guests.Length);
        foreach (var guest in incoming.Guests)
        {
            if (!ids.TryGetValue(guest.SrcGuestId, out var guestId))
                ids.Add(guest.SrcGuestId, guestId = allocate());
            mappings.Add(new(guestId, guest.SrcGuestId));
        }
        var accepted = new AcceptedBooking(bookingId, allocate(), incoming.Key, incoming.CreatedAt, incoming.UpdatedAt,
            incomingDeadline, ids.ToImmutable(), new(bookingId, incoming.Key.SrcBookingId, mappings.MoveToImmutable()), incoming);
        return new(prior == null ? IntakeKind.Create : IntakeKind.Apply, post, accepted, erase,
            new(allocate(), bookingId, accepted.SnapshotId));
    }

    public static DateTimeOffset? IdentityDeadline(BookingDto snapshot, TimeZoneInfo zone)
    {
        if (snapshot.CancelledAt is { } cancelled)
            return cancelled > DateTimeOffset.MaxValue.AddDays(-30) ? null : cancelled.AddDays(30);
        if (snapshot.DepartureDate > DateOnly.MaxValue.AddDays(-30)) return null;
        var local = snapshot.DepartureDate.AddDays(30).ToDateTime(TimeOnly.MinValue);
        if (zone.IsInvalidTime(local))
        {
            var valid = local.AddHours(1);
            while (zone.IsInvalidTime(valid)) valid = valid.AddHours(1);
            long low = local.Ticks / TimeSpan.TicksPerSecond, high = valid.Ticks / TimeSpan.TicksPerSecond;
            while (high - low > 1)
            {
                long middle = low + (high - low) / 2;
                if (zone.IsInvalidTime(new DateTime(middle * TimeSpan.TicksPerSecond))) low = middle;
                else high = middle;
            }
            local = new DateTime(high * TimeSpan.TicksPerSecond);
        }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        if (local.Ticks - offset.Ticks > DateTime.MaxValue.Ticks) return null;
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    public static bool SameBusinessData(BookingDto left, BookingDto right)
    {
        if (left.Key != right.Key || left.CreatedAt != right.CreatedAt || left.UpdatedAt != right.UpdatedAt ||
            left.ArrivalDate != right.ArrivalDate || left.DepartureDate != right.DepartureDate ||
            left.ReservationStatus != right.ReservationStatus || left.CancelledAt != right.CancelledAt ||
            left.Guests.Length != right.Guests.Length) return false;
        var byId = left.Guests.ToDictionary(g => g.SrcGuestId, StringComparer.Ordinal);
        return right.Guests.All(g => byId.Remove(g.SrcGuestId, out var accepted) && accepted == g);
    }
}
