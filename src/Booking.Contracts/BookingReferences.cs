using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Booking.Contracts;

public sealed record HotelScope(string ProviderId, string PropertyId);

public sealed record SourceBookingKey(HotelScope Scope, string SrcBookingId)
{
    public string Encode() => string.Join(':', Uri.EscapeDataString(Scope.ProviderId),
        Uri.EscapeDataString(Scope.PropertyId), Uri.EscapeDataString(SrcBookingId));
}

public sealed record BookingEvent([property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("bookingId")] Guid BookingId, [property: JsonPropertyName("snapshotId")] Guid SnapshotId);

public sealed record GuestMapping([property: JsonPropertyName("guestId")] Guid GuestId,
    [property: JsonPropertyName("srcGuestId")] string SrcGuestId);

public sealed record Mappings([property: JsonPropertyName("bookingId")] Guid BookingId,
    [property: JsonPropertyName("srcBookingId")] string SrcBookingId,
    [property: JsonPropertyName("guests")] ImmutableArray<GuestMapping> Guests);
