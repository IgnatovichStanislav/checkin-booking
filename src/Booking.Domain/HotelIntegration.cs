using System.Collections.Immutable;
using System.Globalization;
using Booking.Contracts;

namespace Booking.Domain;

public enum HotelAction { Import, Update, ReadStatus, Attendance, CorrectAttendance, ManageRegistration, ManageSubscriptions }
public enum RegistrationPolicy { DefaultGpds, LocalLpds, Disabled }
public enum Access { Allowed, Unauthenticated, Forbidden, NotFound }

public sealed class HotelIntegration
{
    private HotelIntegration(string providerId, ImmutableHashSet<string> propertyIds, ImmutableHashSet<HotelAction> permissions,
        TimeZoneInfo zone, RegistrationPolicy policy, string? destinationId)
    {
        ProviderId = providerId;
        PropertyIds = propertyIds;
        Permissions = permissions;
        Zone = zone;
        Policy = policy;
        DestinationId = destinationId;
    }

    public string ProviderId { get; }
    public ImmutableHashSet<string> PropertyIds { get; }
    public ImmutableHashSet<HotelAction> Permissions { get; }
    public TimeZoneInfo Zone { get; }
    public RegistrationPolicy Policy { get; }
    public string? DestinationId { get; }

    public static bool TryConfigure(string providerId, IEnumerable<string> propertyIds, IEnumerable<HotelAction> permissions,
        string timeZoneId, RegistrationPolicy policy, string? destinationId, out HotelIntegration? integration)
    {
        integration = null;
        var properties = propertyIds.ToImmutableHashSet(StringComparer.Ordinal);
        var actions = permissions.ToImmutableHashSet();
        if (string.IsNullOrWhiteSpace(providerId) || properties.Count == 0 || properties.Any(string.IsNullOrWhiteSpace) ||
            !Enum.IsDefined(policy) || actions.Any(a => !Enum.IsDefined(a)) || !TryZone(timeZoneId, out var zone) ||
            (policy != RegistrationPolicy.Disabled && string.IsNullOrWhiteSpace(destinationId))) return false;
        integration = new(providerId, properties, actions, zone!, policy, policy == RegistrationPolicy.Disabled ? null : destinationId);
        return true;
    }

    public static bool TryZone(string? configuredId, out TimeZoneInfo? zone)
    {
        zone = null;
        if (configuredId is not ("Europe/Madrid" or "Europe/Paris") || !HasIcu())
            return false;
        return TimeZoneInfo.TryFindSystemTimeZoneById(configuredId, out zone);
    }

    private static bool HasIcu()
    {
        var version = CultureInfo.InvariantCulture.CompareInfo.Version;
        return version.FullVersion != 0 && BitConverter.ToInt32(version.SortId.ToByteArray()) == version.FullVersion;
    }

    public DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    public static Access Authorize(HotelIntegration? integration, HotelAction action, HotelScope? scope,
        bool parentMatches = true)
    {
        if (integration is null) return Access.Unauthenticated;
        if (!integration.Permissions.Contains(action)) return Access.Forbidden;
        return parentMatches && scope is not null && integration.ProviderId == scope.ProviderId && integration.PropertyIds.Contains(scope.PropertyId)
            ? Access.Allowed : Access.NotFound;
    }

    public HotelScope? SinglePropertyScope() =>
        PropertyIds.Count == 1 ? new HotelScope(ProviderId, PropertyIds.Single()) : null;
}
