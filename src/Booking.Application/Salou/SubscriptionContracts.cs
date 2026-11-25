using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Booking.Contracts;
using Booking.Domain;
using static Booking.Application.Salou.SalouJson;

namespace Booking.Application.Salou;

public sealed record SubscriptionInput(
    [property: JsonPropertyName("endpoint"), JsonRequired] string Endpoint,
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("eventStates"), JsonRequired] ImmutableArray<string?> EventStates);

public sealed record Subscription(
    [property: JsonPropertyName("subscriptionId"), JsonRequired] Guid SubscriptionId,
    [property: JsonPropertyName("endpoint"), JsonRequired] string Endpoint,
    [property: JsonPropertyName("enabled"), JsonRequired] bool Enabled,
    [property: JsonPropertyName("eventStates"), JsonRequired] ImmutableArray<string?> EventStates,
    [property: JsonPropertyName("activeKeyId"), JsonRequired] string ActiveKeyId);

public sealed record Rotation(
    [property: JsonPropertyName("keyId"), JsonRequired] string KeyId);

public sealed record DeliveryAttempt(
    [property: JsonPropertyName("attempt"), JsonRequired] int Attempt,
    [property: JsonPropertyName("occurredAt"), JsonRequired] DateTime OccurredAt,
    [property: JsonPropertyName("outcome"), JsonRequired] string Outcome);

public sealed record Delivery(
    [property: JsonPropertyName("deliveryId"), JsonRequired] Guid DeliveryId,
    [property: JsonPropertyName("notificationId"), JsonRequired] Guid NotificationId,
    [property: JsonPropertyName("state"), JsonRequired] string State,
    [property: JsonPropertyName("attempts"), JsonRequired] ImmutableArray<DeliveryAttempt> Attempts);

public sealed record CallbackProvisioning(HotelScope Scope, ImmutableHashSet<string> Endpoints, ImmutableHashSet<string> KeyIds);
public sealed record ManagedSubscription(HotelScope Scope, Subscription View,
    ImmutableDictionary<string, DateTimeOffset> VerificationUntil);
public sealed record DeliveryWork(HotelScope Scope, Guid SubscriptionId, Delivery View, int SeriesStart,
    DateTimeOffset? NextAttemptAt = null, DateTimeOffset? InFlight = null);
public sealed record SubscriptionDecision(IntakeResponse Response, ManagedSubscription? Write = null);

public static class SubscriptionContracts
{
    private static bool KnownState(string? value) => value is null or "processing" or "actionRequired" or "completed" or "failed";

    public static IntakeResponse? Denial(HotelIntegration? verified, HotelScope requested, ManagedSubscription? subscription,
        Guid? subscriptionId = null, DeliveryWork? delivery = null, Guid? deliveryId = null)
    {
        bool own = (!subscriptionId.HasValue || subscription?.Scope == requested && subscription.View.SubscriptionId == subscriptionId) &&
            (!deliveryId.HasValue || delivery?.Scope == requested && delivery.SubscriptionId == subscriptionId && delivery.View.DeliveryId == deliveryId);
        return HotelIntegration.Authorize(verified, HotelAction.ManageSubscriptions, requested, own) switch
        {
            Access.Allowed => null,
            Access.Unauthenticated => IntakeResponse.Error(401, "unauthenticated"),
            Access.Forbidden => IntakeResponse.Error(403, "forbidden"),
            _ => IntakeResponse.Error(404, "not_found")
        };
    }

    public static IntakeResponse List(HotelIntegration? verified, HotelScope requested, IEnumerable<ManagedSubscription> subscriptions) =>
        Denial(verified, requested, null) ?? new(200, subscriptions.Where(s => s.Scope == requested).Select(s => s.View).ToImmutableArray());

    public static IntakeResponse Get(HotelIntegration? verified, HotelScope requested, Guid id, ManagedSubscription? subscription) =>
        Denial(verified, requested, subscription, id) ?? new(200, subscription!.View);

    public static IntakeResponse Deliveries(HotelIntegration? verified, HotelScope requested, Guid id, ManagedSubscription? subscription,
        IEnumerable<DeliveryWork> deliveries) => Denial(verified, requested, subscription, id) ??
        new(200, deliveries.Where(d => d.Scope == requested && d.SubscriptionId == id).Select(d => d.View).ToImmutableArray());

    public static SubscriptionDecision Configure(HotelIntegration? verified, CallbackProvisioning provisioned, Guid? subscriptionId,
        ManagedSubscription? existing, ReadOnlyMemory<byte> body, string initialKeyId, Func<Guid>? allocate = null)
    {
        var denied = Denial(verified, provisioned.Scope, existing, subscriptionId);
        if (denied != null) return new(denied);
        if (body.Length > MaximumBytes) return new(IntakeResponse.Error(413, "body_too_large"));
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            Shape(root, "$", "endpoint", "enabled", "eventStates");
            string endpoint = Required(root, "$", "endpoint").GetString() ?? "";
            if (!endpoint.StartsWith("https://", StringComparison.Ordinal) || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
                !provisioned.Endpoints.Contains(endpoint)) throw new InvalidField("$.endpoint", "invalid_endpoint");
            bool enabled = Required(root, "$", "enabled").GetBoolean();
            var states = Required(root, "$", "eventStates").EnumerateArray().Select(s => s.GetString()).ToImmutableArray();
            if (states.IsEmpty || states.Any(s => !KnownState(s)) || states.Distinct().Count() != states.Length)
                throw new InvalidField("$.eventStates", "invalid_filter");
            var prior = subscriptionId.HasValue ? existing : null;
            string key = prior?.View.ActiveKeyId ?? initialKeyId;
            if (!provisioned.KeyIds.Contains(key)) return new(IntakeResponse.Error(400, "unknown_key"));
            var view = new Subscription(prior?.View.SubscriptionId ?? (allocate ?? Guid.CreateVersion7)(), endpoint, enabled, states, key);
            return new(new(prior == null ? 201 : 200, view), new(provisioned.Scope, view, prior?.VerificationUntil ?? ImmutableDictionary<string, DateTimeOffset>.Empty));
        }
        catch (InvalidField error) { return new(new(400, new SafeError("invalid_request", [error.Issue]))); }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { return new(IntakeResponse.Error(400, "invalid_request")); }
    }

    public static SubscriptionDecision Rotate(HotelIntegration? verified, CallbackProvisioning provisioned, Guid subscriptionId,
        ManagedSubscription? existing, ReadOnlyMemory<byte> body, DateTimeOffset? lastActiveSend)
    {
        var denied = Denial(verified, provisioned.Scope, existing, subscriptionId);
        if (denied != null) return new(denied);
        if (body.Length > MaximumBytes) return new(IntakeResponse.Error(413, "body_too_large"));
        try
        {
            using var document = JsonDocument.Parse(body);
            Shape(document.RootElement, "$", "keyId");
            var key = Text(document.RootElement, "$", "keyId", 128);
            if (!provisioned.KeyIds.Contains(key)) throw new InvalidField("$.keyId", "unknown_key");
            var view = existing!.View with { ActiveKeyId = key };
            var overlap = existing.VerificationUntil;
            if (lastActiveSend.HasValue && existing.View.ActiveKeyId != key)
                overlap = overlap.SetItem(existing.View.ActiveKeyId, lastActiveSend.Value.AddMinutes(5));
            return new(new(200, view), existing with { View = view, VerificationUntil = overlap.Remove(key) });
        }
        catch (InvalidField error) { return new(new(400, new SafeError("invalid_request", [error.Issue]))); }
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        { return new(IntakeResponse.Error(400, "invalid_request")); }
    }

    public static DeliveryWork? Queue(ManagedSubscription sub, HotelScope scope, RegistrationNotification notification,
        IReadOnlyDictionary<(Guid Subscription, Guid Notification), DeliveryWork> existing, Func<Guid>? allocate = null)
    {
        if (scope != sub.Scope) return null;
        if (existing.TryGetValue((sub.View.SubscriptionId, notification.NotificationId), out var work)) return work;
        if (!sub.View.Enabled || !sub.View.EventStates.Contains(notification.RegistrationStatus)) return null;
        return new(scope, sub.View.SubscriptionId, new((allocate ?? Guid.CreateVersion7)(), notification.NotificationId, "pending", []), 0);
    }

    public static DeliveryWork Begin(ManagedSubscription sub, DeliveryWork delivery, DateTimeOffset at) =>
        sub.Scope == delivery.Scope && sub.View.SubscriptionId == delivery.SubscriptionId && sub.View.Enabled &&
        delivery.View.State == "pending" && delivery.InFlight == null && !(at < delivery.NextAttemptAt)
            ? delivery with { InFlight = at, NextAttemptAt = null } : delivery;

    public static DeliveryWork Finish(DeliveryWork work, DateTimeOffset started, DateTimeOffset finished, int? status)
    {
        if (work.InFlight != started || finished < started) return work;
        int attempt = work.View.Attempts.Length + 1;
        int number = attempt - work.SeriesStart;
        string outcome = status switch
        {
            >= 200 and <= 299 => "delivered",
            null => "uncertain",
            408 or 429 or (>= 500 and <= 599) => "temporary_failure",
            _ => "permanent_failure"
        };
        bool retry = number < 5 && outcome is "temporary_failure" or "uncertain";
        var view = work.View with
        {
            Attempts = work.View.Attempts.Add(new(attempt, started.UtcDateTime, outcome)),
            State = retry ? "pending" : outcome == "delivered" ? "delivered" : "failed"
        };
        return work with { View = view, InFlight = null, NextAttemptAt = retry ? finished + RegistrationOperation.RetryDelay(number) : null };
    }

    public static (IntakeResponse Response, DeliveryWork? Write) Retry(HotelIntegration? verified, HotelScope requested,
        Guid subscriptionId, ManagedSubscription? sub, Guid deliveryId, DeliveryWork? work)
    {
        var denied = Denial(verified, requested, sub, subscriptionId, work, deliveryId);
        if (denied != null) return (denied, null);
        if (!sub!.View.Enabled || work!.View.State != "failed") return (IntakeResponse.Error(409, "delivery_conflict"), null);
        var next = work with { View = work.View with { State = "pending" }, NextAttemptAt = null, SeriesStart = work.View.Attempts.Length };
        return (new(202, next.View), next);
    }
}

public static class CallbackSignature
{
    public const string TimestampHeader = "X-Checkin-Timestamp";
    public const string KeyHeader = "X-Checkin-Key-Id";
    public const string SignatureHeader = "X-Checkin-Signature";

    public static ImmutableDictionary<string, string> Sign(ManagedSubscription subscription, DateTimeOffset sentAt,
        ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        string timestamp = sentAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        return ImmutableDictionary<string, string>.Empty.Add(TimestampHeader, timestamp).Add(KeyHeader, subscription.View.ActiveKeyId)
            .Add(SignatureHeader, Convert.ToHexStringLower(Mac(timestamp, body, key)));
    }

    public static bool Verify(ManagedSubscription subscription, IReadOnlyDictionary<string, string> headers, DateTimeOffset now,
        ReadOnlySpan<byte> body, Func<string, byte[]?> resolve)
    {
        if (!headers.TryGetValue(KeyHeader, out string? id) ||
            (id != subscription.View.ActiveKeyId && (!subscription.VerificationUntil.TryGetValue(id, out var end) || now > end)) ||
            !headers.TryGetValue(TimestampHeader, out string? stamp) ||
            !long.TryParse(stamp, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long seconds) ||
            seconds.ToString(CultureInfo.InvariantCulture) != stamp ||
            Math.Abs((now - DateTimeOffset.UnixEpoch).Ticks - (decimal)seconds * TimeSpan.TicksPerSecond) > TimeSpan.FromMinutes(5).Ticks ||
            !headers.TryGetValue(SignatureHeader, out string? hex) || hex.Length != 64 ||
            hex.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')) || resolve(id) is not { Length: > 0 } key) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(hex), Mac(stamp, body, key));
    }

    private static byte[] Mac(string timestamp, ReadOnlySpan<byte> body, ReadOnlySpan<byte> key)
    {
        using var mac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, key);
        mac.AppendData(Encoding.ASCII.GetBytes(timestamp + "."));
        mac.AppendData(body);
        return mac.GetHashAndReset();
    }
}
