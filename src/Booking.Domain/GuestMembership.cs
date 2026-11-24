using System.Collections.Immutable;
using Booking.Contracts;

namespace Booking.Domain;

public sealed record MembershipEpisode(DateTimeOffset StartedAt, DateTimeOffset? EndedAt = null);

public sealed record GuestParticipation(ImmutableArray<MembershipEpisode> Episodes)
{
    public int? ActiveEpisode => !Episodes.IsEmpty && Episodes[^1].EndedAt is null ? Episodes.Length : null;
}

public sealed record GuestMembershipState(ReservationStatus ReservationStatus, ImmutableDictionary<string, Guid> GuestIds,
    ImmutableDictionary<Guid, GuestParticipation> Participants);

public enum ParticipationWork { ReassessRegistration, AssessCancellation }

public sealed record ParticipationChange(Guid GuestId, ParticipationWork Work);

internal sealed record MembershipDecision(GuestMembershipState State, ImmutableArray<GuestMapping> Mappings,
    ImmutableArray<ParticipationChange> Changes);

internal static class GuestMembership
{
    public static MembershipDecision Apply(BookingDto snapshot, GuestMembershipState? prior, Func<Guid> allocate)
    {
        var ids = (prior?.GuestIds ?? ImmutableDictionary.Create<string, Guid>(StringComparer.Ordinal)).ToBuilder();
        var participants = (prior?.Participants ?? ImmutableDictionary<Guid, GuestParticipation>.Empty).ToBuilder();
        var mappings = ImmutableArray.CreateBuilder<GuestMapping>(snapshot.Guests.Length);
        var changes = ImmutableArray.CreateBuilder<ParticipationChange>();
        var active = new HashSet<Guid>();

        foreach (var guest in snapshot.Guests)
        {
            if (!ids.TryGetValue(guest.SrcGuestId, out var guestId))
            {
                guestId = allocate();
                ids.Add(guest.SrcGuestId, guestId);
                participants.Add(guestId, new([]));
            }

            mappings.Add(new(guestId, guest.SrcGuestId));
            if (snapshot.ReservationStatus == ReservationStatus.Cancelled) continue;

            active.Add(guestId);
            var participation = participants[guestId];
            if (participation.ActiveEpisode.HasValue) continue;

            participants[guestId] = new(participation.Episodes.Add(new(snapshot.UpdatedAt)));
            changes.Add(new(guestId, ParticipationWork.ReassessRegistration));
        }

        if (prior is not null)
        {
            foreach (var (guestId, participation) in prior.Participants)
            {
                if (!participation.ActiveEpisode.HasValue || active.Contains(guestId)) continue;

                var episodes = participation.Episodes;
                participants[guestId] = new(episodes.SetItem(episodes.Length - 1, episodes[^1] with { EndedAt = snapshot.UpdatedAt }));
                changes.Add(new(guestId, ParticipationWork.AssessCancellation));
            }
        }

        return new(new(snapshot.ReservationStatus, ids.ToImmutable(), participants.ToImmutable()),
            mappings.MoveToImmutable(), changes.ToImmutable());
    }
}
