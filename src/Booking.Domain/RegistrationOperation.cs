using System.Collections.Immutable;
using Booking.Contracts;

namespace Booking.Domain;

public enum OperationKind { Register, Amend, Cancel }
public enum OperationState { Pending, Confirmed, Failed }
public enum OperationReason { Ready, MissingPassport, PassportExpired, CorrectData, TemporaryFailure, Uncertain, Intervention, Unsupported, IdentityExpired, Stopped }
public enum DestinationResult { Confirmed, Rejected, TemporaryFailure, PermanentFailure, Unknown, DefinitelyAbsent }
public sealed record Submission(int Series, int Number, Guid SnapshotId, DateTimeOffset StartedAt, DestinationResult? Result);
public sealed record RegistrationRetry(int Series, string Actor, DateTimeOffset At)
{
    public override string ToString() => nameof(RegistrationRetry);
}

public sealed record RegistrationOperation
{
    public required Guid OperationId { get; init; }
    public required Guid RegistrationRequestId { get; init; }
    public required HotelScope Scope { get; init; }
    public required Guid BookingId { get; init; }
    public required Guid GuestId { get; init; }
    public required Guid SnapshotId { get; init; }
    public required string DestinationId { get; init; }
    public required RegistrationPolicy Policy { get; init; }
    public required OperationKind Kind { get; init; }
    public Guid? PriorOperationId { get; init; }
    public OperationState State { get; private init; }
    public OperationReason Reason { get; private init; }
    public DateTimeOffset? NextAttemptAt { get; private init; }
    public ImmutableArray<Submission> Attempts { get; private init; } = [];
    public ImmutableArray<RegistrationRetry> Retries { get; private init; } = [];
    public int Series => Retries.Length + 1;
    public bool IsStopped => Reason == OperationReason.Stopped;
    public override string ToString() => nameof(RegistrationOperation);

    internal static RegistrationOperation Create(AcceptedBooking booking, Guid guestId, RegistrationPolicy policy,
        string destination, OperationKind kind, RegistrationOperation? previous, OperationReason reason, Func<Guid> allocate) =>
        new()
        {
            Scope = booking.Key.Scope, BookingId = booking.BookingId, GuestId = guestId, OperationId = allocate(),
            RegistrationRequestId = allocate(), SnapshotId = booking.SnapshotId, Policy = policy, DestinationId = destination,
            Kind = kind, PriorOperationId = previous?.OperationId, Reason = reason,
            State = reason is OperationReason.Unsupported or OperationReason.IdentityExpired ? OperationState.Failed : OperationState.Pending
        };

    internal RegistrationOperation Stop() => this with { Reason = OperationReason.Stopped, NextAttemptAt = null };
    internal RegistrationOperation Prepare(Guid snapshotId, OperationReason reason) => this with
    {
        SnapshotId = snapshotId, Reason = reason, State = reason == OperationReason.IdentityExpired ? OperationState.Failed : OperationState.Pending
    };

    internal static OperationReason Eligibility(AcceptedBooking booking, Guid guestId, OperationKind kind, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (booking.Restricted is null || now >= booking.Deadline) return OperationReason.IdentityExpired;
        if (kind == OperationKind.Cancel) return OperationReason.Ready;
        var guest = booking.Restricted.Guests.FirstOrDefault(g => booking.GuestIds[g.SrcGuestId] == guestId);
        if (guest?.Passport is not { } passport) return OperationReason.MissingPassport;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        return passport.ExpiresOn < today ? OperationReason.PassportExpired : OperationReason.Ready;
    }

    public RegistrationOperation BeginSubmission(AcceptedBooking accepted, TimeZoneInfo zone, DateTimeOffset now)
    {
        if (accepted.BookingId != BookingId || accepted.Key.Scope != Scope || accepted.SnapshotId != SnapshotId ||
            State != OperationState.Pending || Reason is not (OperationReason.Ready or OperationReason.TemporaryFailure) ||
            now < NextAttemptAt) return this;
        var eligible = Eligibility(accepted, GuestId, Kind, zone, now);
        if (eligible != OperationReason.Ready) return Prepare(SnapshotId, eligible);
        int number = Attempts.Count(a => a.Series == Series) + 1;
        if (number > 5) return this;
        var contentSnapshot = Attempts.IsEmpty ? SnapshotId : Attempts[0].SnapshotId;
        return this with { Attempts = Attempts.Add(new(Series, number, contentSnapshot, now, null)), Reason = OperationReason.Uncertain, NextAttemptAt = null };
    }

    public RegistrationOperation Resolve(Guid operationId, Guid requestId, int series, int number, DestinationResult result, DateTimeOffset at)
    {
        if (operationId != OperationId || requestId != RegistrationRequestId || Reason != OperationReason.Uncertain ||
            Attempts.IsEmpty || Attempts[^1].Series != series || Attempts[^1].Number != number || at < Attempts[^1].StartedAt) return this;
        var attempts = Attempts.SetItem(Attempts.Length - 1, Attempts[^1] with { Result = result });
        var resolved = this with { Attempts = attempts };
        return result switch
        {
            DestinationResult.Confirmed => resolved with { State = OperationState.Confirmed, Reason = OperationReason.Ready },
            DestinationResult.Rejected => resolved with { Reason = OperationReason.CorrectData },
            DestinationResult.Unknown => resolved,
            DestinationResult.TemporaryFailure or DestinationResult.DefinitelyAbsent when number < 5 =>
                resolved with { Reason = OperationReason.TemporaryFailure, NextAttemptAt = at + RetryDelay(number) },
            _ => resolved with { State = OperationState.Failed, Reason = OperationReason.Intervention }
        };
    }

    public static TimeSpan RetryDelay(int number) => TimeSpan.FromSeconds(number switch
    {
        1 => 5, 2 => 30, 3 => 120, 4 => 600, _ => throw new ArgumentOutOfRangeException(nameof(number))
    });

    public RegistrationOperation ManualRetry(HotelIntegration? verified, string actor, AcceptedBooking booking,
        DateTimeOffset now, bool supportsChange = false)
    {
        if (Reason == OperationReason.IdentityExpired || (Reason == OperationReason.Unsupported && !supportsChange) || string.IsNullOrWhiteSpace(actor) || State != OperationState.Failed || IsStopped ||
            HotelIntegration.Authorize(verified, HotelAction.ManageRegistration, Scope, booking.Key.Scope == Scope &&
                booking.BookingId == BookingId && booking.SnapshotId == SnapshotId) != Access.Allowed) return this;
        var reason = Eligibility(booking, GuestId, Kind, verified!.Zone, now);
        if (reason == OperationReason.IdentityExpired) return this with { Reason = reason };
        return Prepare(SnapshotId, reason) with { Retries = Retries.Add(new(Series + 1, actor, now)), NextAttemptAt = null };
    }
}

public sealed record RegistrationHistory(ImmutableArray<RegistrationOperation> Operations)
{
    private bool CurrentRetired { get; init; }
    public Guid? ComparisonNeededForSnapshotId { get; private init; }
    public RegistrationOperation? Current => !CurrentRetired && Operations.LastOrDefault() is { IsStopped: false } last ? last : null;

    public RegistrationHistory Replace(RegistrationOperation expected, RegistrationOperation operation) =>
        ReferenceEquals(Current, expected) && expected.OperationId == operation.OperationId && expected.RegistrationRequestId == operation.RegistrationRequestId
            ? new(Operations.SetItem(Operations.Length - 1, operation)) : this;

    public RegistrationHistory Evaluate(HotelIntegration hotel, AcceptedBooking booking, Guid guestId, DateTimeOffset now,
        AcceptedBooking? previous = null, bool supportsChange = true, Func<Guid>? allocate = null)
    {
        if (hotel.ProviderId != booking.Key.Scope.ProviderId || !hotel.PropertyIds.Contains(booking.Key.Scope.PropertyId) ||
            !booking.Membership.Participants.TryGetValue(guestId, out var member) ||
            Operations.Any(o => o.BookingId != booking.BookingId || o.GuestId != guestId || o.Scope != booking.Key.Scope)) return this;
        allocate ??= Guid.CreateVersion7;
        bool active = member.ActiveEpisode.HasValue;
        var current = Current;
        if (current?.Reason == OperationReason.Uncertain) return this;
        var lastConfirmed = Operations.LastOrDefault(o => o.State == OperationState.Confirmed);
        bool registered = lastConfirmed != null && lastConfirmed.Kind != OperationKind.Cancel;
        bool needsCancellation = !active && registered;
        bool identityAvailable = booking.Restricted != null && !(now >= booking.Deadline);
        var history = this;
        if (current is { Reason: OperationReason.IdentityExpired })
        {
            if (!identityAvailable || booking.SnapshotId == current.SnapshotId) return this;
            history = this with { CurrentRetired = true, ComparisonNeededForSnapshotId = null };
            current = null;
        }
        if (active && current is { Kind: OperationKind.Cancel, State: OperationState.Confirmed })
        {
            history = this with { CurrentRetired = true, ComparisonNeededForSnapshotId = null };
            current = null;
        }
        if (!active && current is { State: not OperationState.Confirmed, Kind: not OperationKind.Cancel })
        {
            history = Replace(current, current.Stop());
            current = null;
        }
        if (!active && !needsCancellation) return history;
        if (!active && current?.Kind == OperationKind.Cancel)
        {
            var cancellation = identityAvailable ? current with { SnapshotId = booking.SnapshotId } :
                current.Prepare(current.SnapshotId, OperationReason.IdentityExpired);
            return Replace(current, cancellation);
        }

        bool changed = false;
        if (active && current != null && booking.SnapshotId != current.SnapshotId)
        {
            if (identityAvailable && previous?.SnapshotId == current.SnapshotId && previous.BookingId == booking.BookingId &&
                previous.Key == booking.Key && previous.Restricted != null && !(now >= previous.Deadline))
                changed = !SameInput(previous, booking, guestId);
            else if (current.State == OperationState.Confirmed || identityAvailable && !current.Attempts.IsEmpty)
                return this with { ComparisonNeededForSnapshotId = booking.SnapshotId };
        }
        if (active && current is { State: OperationState.Confirmed, Kind: not OperationKind.Cancel } && !changed)
            return Replace(current, current with { SnapshotId = booking.SnapshotId });

        var kind = needsCancellation ? OperationKind.Cancel : registered ? OperationKind.Amend : OperationKind.Register;
        bool newOperation = current == null || current.State == OperationState.Confirmed || kind != current.Kind ||
            changed && !current.Attempts.IsEmpty;
        if (newOperation)
        {
            var pinned = registered ? lastConfirmed : current;
            var policy = pinned?.Policy ?? hotel.Policy;
            if (policy == RegistrationPolicy.Disabled) return history;
            var eligible = kind != OperationKind.Register && !supportsChange ? OperationReason.Unsupported :
                RegistrationOperation.Eligibility(booking, guestId, kind, hotel.Zone, now);
            var next = RegistrationOperation.Create(booking, guestId, policy, pinned?.DestinationId ?? hotel.DestinationId!,
                kind, history.Operations.LastOrDefault(), eligible, allocate);
            return new(history.Operations.Add(next));
        }

        if (!identityAvailable) return Replace(current!, current!.Prepare(current.SnapshotId, OperationReason.IdentityExpired));
        if (current!.State == OperationState.Failed || current.Reason is OperationReason.CorrectData or OperationReason.TemporaryFailure)
            return Replace(current, current with { SnapshotId = booking.SnapshotId });
        return Replace(current, current.Prepare(booking.SnapshotId, RegistrationOperation.Eligibility(booking, guestId, kind, hotel.Zone, now)));
    }

    private static bool SameInput(AcceptedBooking? before, AcceptedBooking after, Guid guestId)
    {
        if (before?.BookingId != after.BookingId || before.Restricted is not { } old || after.Restricted is not { } latest) return false;
        var source = after.GuestIds.First(p => p.Value == guestId).Key;
        return old.ArrivalDate == latest.ArrivalDate && old.DepartureDate == latest.DepartureDate &&
            old.Guests.FirstOrDefault(g => g.SrcGuestId == source) == latest.Guests.FirstOrDefault(g => g.SrcGuestId == source);
    }
}
