using System.Collections.Immutable;

namespace Booking.Contracts;

public enum ReservationStatus { Confirmed, Cancelled }

public sealed record Passport(string Number, string IssuingCountry, DateOnly ExpiresOn)
{
    public override string ToString() => nameof(Passport);
}

public sealed record Guest(string SrcGuestId, string FirstName, string LastName, DateOnly DateOfBirth, Passport? Passport)
{
    public override string ToString() => nameof(Guest);
}

public sealed record BookingDto(SourceBookingKey Key, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateOnly ArrivalDate, DateOnly DepartureDate, ReservationStatus ReservationStatus,
    DateTimeOffset? CancelledAt, ImmutableArray<Guest> Guests)
{
    public override string ToString() => nameof(BookingDto);
}
