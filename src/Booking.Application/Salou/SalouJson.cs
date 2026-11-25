using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Booking.Application.Salou;

internal static class SalouJson
{
    internal const int MaximumBytes = 1024 * 1024;

    internal sealed class InvalidField(string path, string reason) : Exception
    {
        public FieldIssue Issue { get; } = new(path, reason);
    }

    internal static void Shape(JsonElement element, string path, params ReadOnlySpan<string> allowed)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidField(path, "object_required");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in element.EnumerateObject())
            if (!allowed.Contains(member.Name) || !seen.Add(member.Name))
                throw new InvalidField(path, "unexpected_member");
    }

    internal static JsonElement Required(JsonElement parent, string path, string key) =>
        parent.TryGetProperty(key, out var value) ? value : throw new InvalidField(path + "." + key, "required");

    internal static string Text(JsonElement parent, string path, string key, int limit)
    {
        var value = Required(parent, path, key);
        if (value.ValueKind != JsonValueKind.String) throw new InvalidField(path + "." + key, "string_required");
        string text = value.GetString()!;
        if (text.Length == 0 || text.EnumerateRunes().Count() > limit)
            throw new InvalidField(path + "." + key, "invalid_length");
        return text;
    }

    internal static DateTimeOffset Instant(JsonElement parent, string key)
    {
        string text = Text(parent, "$", key, 28);
        int digits = text.Length - 21;
        string? format = text.Length == 20 ? "yyyy-MM-dd'T'HH:mm:ss'Z'" :
            digits is >= 1 and <= 7 ? "yyyy-MM-dd'T'HH:mm:ss." + new string('f', digits) + "'Z'" : null;
        if (format != null && DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var instant)) return instant;
        throw new InvalidField("$." + key, "invalid_utc_instant");
    }
}
