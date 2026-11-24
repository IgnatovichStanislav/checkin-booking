using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Booking.Contracts;
using static Booking.Application.Salou.SalouJson;

namespace Booking.Application.Salou;

public sealed record DecodeFailure(int Status, SafeError Body);
public sealed record SnapshotResult(BookingDto? Snapshot, DecodeFailure? Error)
{
    public override string ToString() => nameof(SnapshotResult);
}

public static class SalouSnapshotMapper
{
    public const int MaximumBytes = SalouJson.MaximumBytes;
    private static readonly FrozenSet<string> Countries = (
        "AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ " +
        "CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET " +
        "FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU " +
        "ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY " +
        "MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ " +
        "OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ " +
        "TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW")
        .Split(' ').ToFrozenSet(StringComparer.Ordinal);

    public static SnapshotResult Decode(ReadOnlyMemory<byte> utf8, HotelScope scope, DateOnly today)
    {
        if (utf8.Length > MaximumBytes)
            return Failure(413, "body_too_large", new("$", "size_limit"));
        try
        {
            using var document = JsonDocument.Parse(utf8);
            var root = document.RootElement;
            Shape(root, "$", "srcBookingId", "createdAt", "updatedAt", "arrivalDate", "departureDate",
                "reservationStatus", "cancelledAt", "guests");
            string sourceId = Text(root, "$", "srcBookingId", 128);
            var created = Instant(root, "createdAt");
            var updated = Instant(root, "updatedAt");
            if (updated < created) throw new InvalidField("$.updatedAt", "before_creation");
            var arrival = Date(root, "$", "arrivalDate");
            var departure = Date(root, "$", "departureDate");
            if (departure <= arrival) throw new InvalidField("$.departureDate", "invalid_stay");
            var status = Text(root, "$", "reservationStatus", 9) switch
            {
                "confirmed" => ReservationStatus.Confirmed,
                "cancelled" => ReservationStatus.Cancelled,
                _ => throw new InvalidField("$.reservationStatus", "unsupported_status")
            };
            DateTimeOffset? cancelled = status == ReservationStatus.Cancelled ? Instant(root, "cancelledAt") : null;
            if (cancelled == null && root.TryGetProperty("cancelledAt", out var cancellation) && cancellation.ValueKind != JsonValueKind.Null)
                throw new InvalidField("$.cancelledAt", "unexpected_instant");

            var guests = Required(root, "$", "guests");
            if (guests.ValueKind != JsonValueKind.Array || guests.GetArrayLength() is < 1 or > 100)
                throw new InvalidField("$.guests", "invalid_guest_list");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var parsed = ImmutableArray.CreateBuilder<Guest>(guests.GetArrayLength());
            foreach (var item in guests.EnumerateArray())
            {
                string path = $"$.guests[{parsed.Count}]";
                Shape(item, path, "srcGuestId", "firstName", "lastName", "dateOfBirth", "passport");
                string id = Text(item, path, "srcGuestId", 128);
                if (!seen.Add(id)) throw new InvalidField(path + ".srcGuestId", "duplicate_guest");
                var dob = Date(item, path, "dateOfBirth");
                if (dob > today) throw new InvalidField(path + ".dateOfBirth", "future_date");
                parsed.Add(new Guest(id, Text(item, path, "firstName", 100), Text(item, path, "lastName", 100), dob,
                    ParsePassport(Required(item, path, "passport"), path + ".passport")));
            }
            return new(new BookingDto(new(scope, sourceId), created, updated, arrival, departure, status, cancelled,
                parsed.MoveToImmutable()), null);
        }
        catch (InvalidField invalid) { return Failure(400, "invalid_snapshot", invalid.Issue); }
        catch (JsonException) { return Failure(400, "invalid_snapshot", new("$", "invalid_json")); }
        catch (InvalidOperationException) { return Failure(400, "invalid_snapshot", new("$", "invalid_encoding")); }
    }

    private static SnapshotResult Failure(int status, string code, FieldIssue issue) =>
        new(null, new(status, new(code, [issue])));

    private static Passport? ParsePassport(JsonElement value, string path)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        Shape(value, path, "number", "issuingCountry", "expiresOn");
        var country = Text(value, path, "issuingCountry", 2);
        if (!Countries.Contains(country)) throw new InvalidField(path + ".issuingCountry", "invalid_country");
        return new(Text(value, path, "number", 50), country, Date(value, path, "expiresOn"));
    }

    private static DateOnly Date(JsonElement parent, string path, string key)
    {
        if (DateOnly.TryParseExact(Text(parent, path, key, 10), "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)) return date;
        throw new InvalidField(path + "." + key, "invalid_date");
    }
}
