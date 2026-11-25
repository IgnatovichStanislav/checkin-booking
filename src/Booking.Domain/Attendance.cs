using System.Collections.Immutable;

namespace Booking.Domain;

public enum AttendanceStatus { Expected, Arrived, CheckedIn, CheckedOut, NoShow }
public enum CorrectionReason { IncorrectReport, IncorrectStatus, IncorrectTime }
public enum AttendanceKind { Append, Duplicate, NotFound, Conflict }

public sealed record AttendanceObservation(AttendanceStatus Status, DateTimeOffset OccurredAt);

public sealed record AttendanceEvent(Guid AttendanceEventId, Guid BookingId, Guid GuestId, int Episode, string Actor,
    AttendanceObservation Observation, DateTimeOffset ReceivedAt, Guid? OriginalEventId = null, CorrectionReason? ReasonCode = null)
{
    public override string ToString() => nameof(AttendanceEvent);
}

public sealed record AttendanceDecision(AttendanceKind Kind, Guid? AttendanceEventId = null, AttendanceEvent? Append = null);

public static class Attendance
{
    public static AttendanceEvent? FindOriginal(ImmutableArray<AttendanceEvent> history, Guid bookingId, Guid guestId, Guid eventId) =>
        history.FirstOrDefault(entry => entry.BookingId == bookingId && entry.GuestId == guestId &&
            entry.AttendanceEventId == eventId && entry.OriginalEventId is null);

    public static AttendanceDecision Report(AcceptedBooking booking, Guid guestId, ImmutableArray<AttendanceEvent> history,
        string actor, AttendanceObservation observation, DateTimeOffset receivedAt, Func<Guid>? allocate = null)
    {
        if (!booking.Membership.Participants.TryGetValue(guestId, out var participant))
            return new(AttendanceKind.NotFound);

        var guestHistory = ForGuest(history, booking.BookingId, guestId);
        var duplicate = guestHistory.FirstOrDefault(entry => IsRetry(entry, actor, observation, null, null));
        if (duplicate is not null) return new(AttendanceKind.Duplicate, duplicate.AttendanceEventId);

        if (participant.ActiveEpisode is not { } episode) return new(AttendanceKind.Conflict);

        var currentStatus = AttendanceStatus.Expected;
        foreach (var episodeHistory in guestHistory.GroupBy(entry => entry.Episode))
        {
            if (!TryReplay(episodeHistory, out var latest) ||
                (latest is not null && observation.OccurredAt <= latest.OccurredAt))
                return new(AttendanceKind.Conflict);

            if (episodeHistory.Key == episode && latest is not null) currentStatus = latest.Status;
        }
        if (!CanTransition(currentStatus, observation.Status)) return new(AttendanceKind.Conflict);

        return Append(booking.BookingId, guestId, episode, actor, observation, receivedAt, allocate);
    }

    public static AttendanceDecision Correct(AcceptedBooking booking, Guid guestId, Guid originalEventId,
        ImmutableArray<AttendanceEvent> history, string actor, AttendanceObservation replacement, CorrectionReason reason,
        DateTimeOffset receivedAt, Func<Guid>? allocate = null)
    {
        if (!booking.Membership.Participants.ContainsKey(guestId) ||
            FindOriginal(history, booking.BookingId, guestId, originalEventId) is not { } original)
            return new(AttendanceKind.NotFound);

        var guestHistory = ForGuest(history, booking.BookingId, guestId);
        var duplicate = guestHistory.FirstOrDefault(entry => IsRetry(entry, actor, replacement, originalEventId, reason));
        if (duplicate is not null) return new(AttendanceKind.Duplicate, duplicate.AttendanceEventId);

        if (!TryReplay(guestHistory.Where(entry => entry.Episode == original.Episode), out _, originalEventId, replacement))
            return new(AttendanceKind.Conflict);

        return Append(booking.BookingId, guestId, original.Episode, actor, replacement, receivedAt, allocate, originalEventId, reason);
    }

    public static AttendanceStatus Status(ImmutableArray<AttendanceEvent> history, Guid bookingId, Guid guestId, int episode)
    {
        if (!TryReplay(ForGuest(history, bookingId, guestId).Where(entry => entry.Episode == episode), out var latest))
            throw new InvalidOperationException("Invalid persisted attendance history.");

        return latest?.Status ?? AttendanceStatus.Expected;
    }

    private static IEnumerable<AttendanceEvent> ForGuest(ImmutableArray<AttendanceEvent> history, Guid bookingId, Guid guestId) =>
        history.Where(entry => entry.BookingId == bookingId && entry.GuestId == guestId);

    private static bool IsRetry(AttendanceEvent entry, string actor, AttendanceObservation observation, Guid? originalEventId,
        CorrectionReason? reason) =>
        entry.Actor == actor && entry.Observation == observation && entry.OriginalEventId == originalEventId && entry.ReasonCode == reason;

    private static AttendanceDecision Append(Guid bookingId, Guid guestId, int episode, string actor, AttendanceObservation observation,
        DateTimeOffset receivedAt, Func<Guid>? allocate, Guid? originalEventId = null, CorrectionReason? reason = null)
    {
        var eventId = (allocate ?? Guid.CreateVersion7)();
        return new(AttendanceKind.Append, eventId,
            new(eventId, bookingId, guestId, episode, actor, observation, receivedAt, originalEventId, reason));
    }

    private static bool TryReplay(IEnumerable<AttendanceEvent> episodeHistory, out AttendanceObservation? latest,
        Guid? correctedEventId = null, AttendanceObservation? replacement = null)
    {
        // Replay in append order so a corrected instant cannot move an observation to another position.
        var originals = new List<AttendanceEvent>();
        var corrections = new Dictionary<Guid, AttendanceObservation>();
        foreach (var entry in episodeHistory)
        {
            if (entry.OriginalEventId is { } target) corrections[target] = entry.Observation;
            else originals.Add(entry);
        }
        if (correctedEventId is { } proposedTarget) corrections[proposedTarget] = replacement!;

        latest = null;
        foreach (var original in originals)
        {
            var next = corrections.GetValueOrDefault(original.AttendanceEventId, original.Observation);
            var priorStatus = latest?.Status ?? AttendanceStatus.Expected;
            if ((latest is not null && next.OccurredAt <= latest.OccurredAt) ||
                !(next.Status == AttendanceStatus.Expected ? priorStatus == AttendanceStatus.Expected : CanTransition(priorStatus, next.Status)))
                return false;

            latest = next;
        }
        return true;
    }

    private static bool CanTransition(AttendanceStatus from, AttendanceStatus to) => from switch
    {
        AttendanceStatus.Expected => to is AttendanceStatus.Arrived or AttendanceStatus.CheckedIn or AttendanceStatus.NoShow,
        AttendanceStatus.Arrived => to == AttendanceStatus.CheckedIn,
        AttendanceStatus.CheckedIn => to == AttendanceStatus.CheckedOut,
        AttendanceStatus.NoShow => to is AttendanceStatus.Arrived or AttendanceStatus.CheckedIn,
        _ => false
    };
}
