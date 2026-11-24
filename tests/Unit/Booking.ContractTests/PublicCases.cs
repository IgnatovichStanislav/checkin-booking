using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Booking.Application.Salou;
using Booking.Contracts;
using Booking.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Booking.ContractTests;

public sealed partial class ContractCases
{
    private static readonly JsonNode WireExpected = Fixture("wire/expected");
    private static readonly JsonNode Definitions = ContractFile("types.schema.json")["$defs"]!;
    private static JsonNode ContractFile(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "contracts", file)))!;

    private static void Expected(string name, object value) =>
        Assert.IsTrue(JsonNode.DeepEquals(WireExpected[name], JsonSerializer.SerializeToNode(value)), name);

    private static BookingStatus Status(AcceptedBooking booking, params RegistrationHistory[] histories)
    {
        var byGuest = histories.Where(h => !h.Operations.IsEmpty).ToDictionary(h => h.Operations[0].GuestId);
        var response = RegistrationStatus.Read(OperationHotel(), booking.BookingId, booking, byGuest, []);
        Assert.AreEqual(200, response.Status);
        return (BookingStatus)response.Body;
    }

    private static RegistrationNotification Stopped()
    {
        var booking = RegistrationBooking();
        var history = Start(booking);
        var cancelled = Changed(booking, booking.Restricted! with { ReservationStatus = ReservationStatus.Cancelled, CancelledAt = Now });
        var next = history.Evaluate(Hotel, cancelled, Id(2), Now, booking);
        return RegistrationStatus.Change(history, next, Id(30), Now).Outbox!;
    }

    [TestMethod, TestCategory("A07"), TestCategory("A10"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "wire/expected.json#Status,Notification,SubscriptionInput,Subscription,Delivery,Rotation",
        "registration/expectations.json#delays", "intake/restricted/snapshot.json")]
    public void PublicProjection()
    {
        var booking = RegistrationBooking();
        var ready = Start(booking);
        Expected("Status", Status(booking, ready));
        var missing = Start(booking, 3);
        var op = ready.Current!;
        var send = op.BeginSubmission(booking, Hotel.Zone, Now);
        var failed = ready.Replace(op, Resolve(send, DestinationResult.PermanentFailure, Now));
        var confirmed = ready.Replace(op, Resolve(send, DestinationResult.Confirmed, Now));
        Assert.AreEqual("actionRequired", Status(booking, failed, missing).ActiveGuestSummary);
        Assert.AreEqual("failed", Status(booking, failed).ActiveGuestSummary);
        Assert.IsNull(Status(booking, confirmed).ActiveGuestSummary);
        Assert.IsNull(Status(booking).ActiveGuestSummary);
        var single = Changed(booking, booking.Restricted! with { Guests = [booking.Restricted.Guests[0]] });
        Assert.AreEqual("completed", Status(single, confirmed).ActiveGuestSummary);
        var cancelled = Changed(booking, booking.Restricted! with { ReservationStatus = ReservationStatus.Cancelled, CancelledAt = Now });
        var work = confirmed.Evaluate(OperationHotel(RegistrationPolicy.Disabled), cancelled, Id(2), Now, booking, allocate: Ids(90));
        var outstanding = Status(cancelled, work);
        Assert.IsNull(outstanding.ActiveGuestSummary);
        Assert.AreEqual(work.Current!.OperationId, outstanding.OutstandingCancellations.Single().OperationId);
        var complete = Resolve(work.Current.BeginSubmission(cancelled, Hotel.Zone, Now), DestinationResult.Confirmed, Now);
        var history = work.Replace(work.Current, complete);
        Assert.IsEmpty(Status(cancelled, history).OutstandingCancellations);
        Assert.HasCount(2, history.Operations);
        var stopped = ready.Evaluate(Hotel, cancelled, Id(2), Now, booking);
        var row = Status(cancelled, stopped).Guests[0];
        Assert.IsNull(row.OperationId);
        Assert.IsNull(row.RegistrationStatus);
        Assert.IsNull(row.ReasonCode);
        Expected("Notification", Stopped());
        Assert.AreEqual(Stopped().NotificationId, RegistrationStatus.Change(ready, stopped, Id(30), Now).Outbox!.NotificationId);
        Assert.IsNull(RegistrationStatus.Change(stopped, stopped, Id(31), Now).Outbox);
        Assert.IsNull(RegistrationStatus.Change(EmptyHistory, EmptyHistory, Id(31), Now).Outbox);
        Assert.IsNull(RegistrationStatus.Change(ready, ready.Replace(op, send), Id(31), Now).Outbox);
        Assert.AreEqual("completed", RegistrationStatus.Change(ready, confirmed, Id(32), Now).Outbox!.RegistrationStatus);
        VerifyDelivery();
    }

    private static CallbackProvisioning Provisioned => new(Scope, ImmutableHashSet.Create("https://callbacks.example.test/salou"),
        ImmutableHashSet.Create("primary", "replacement"));

    private static ManagedSubscription Subscribe() => SubscriptionContracts.Configure(OperationHotel(), Provisioned, null, null,
        Bytes(WireExpected["SubscriptionInput"]!), "primary", Ids(40)).Write!;

    private static void VerifyDelivery()
    {
        var sub = Subscribe();
        Expected("Subscription", sub.View);
        var notification = Stopped();
        var existing = new Dictionary<(Guid, Guid), DeliveryWork>();
        var work = SubscriptionContracts.Queue(sub, Scope, notification, existing, Ids(50))!;
        existing.Add((sub.View.SubscriptionId, notification.NotificationId), work);
        Assert.AreSame(work, SubscriptionContracts.Queue(sub, Scope, notification, existing));
        Assert.IsNull(SubscriptionContracts.Queue(sub, Scope, notification with { RegistrationStatus = "failed", ReasonCode = null },
            new Dictionary<(Guid, Guid), DeliveryWork>()));
        var initial = work;
        var clock = Now;
        int?[] responses = [503, null, 408, 429, 500];
        for (int i = 0; i < responses.Length; i++)
        {
            work = SubscriptionContracts.Begin(sub, work, clock);
            Assert.AreEqual(clock, work.InFlight);
            var disabled = sub with { View = sub.View with { Enabled = false } };
            Assert.AreSame(initial, SubscriptionContracts.Begin(disabled, initial, clock));
            work = SubscriptionContracts.Finish(work, clock, clock, responses[i]);
            if (i == 0) Expected("Delivery", work.View);
            if (i + 1 == responses.Length) break;
            clock = clock.AddSeconds((int)OpExpected["delays"]![i]!);
            Assert.AreEqual(clock, work.NextAttemptAt);
            Assert.AreSame(work, SubscriptionContracts.Begin(sub, work, clock.AddTicks(-1)));
        }
        Assert.AreEqual("failed", work.View.State);
        var retried = SubscriptionContracts.Retry(OperationHotel(), Scope, Id(40), sub, Id(50), work);
        Assert.AreEqual(202, retried.Response.Status);
        Assert.AreEqual(notification.NotificationId, retried.Write!.View.NotificationId);
        Assert.AreEqual(5, retried.Write.SeriesStart);
        foreach (var (status, expected) in new[] { (200, "delivered"), (299, "delivered"), (302, "failed"), (400, "failed") })
        {
            var finished = SubscriptionContracts.Finish(SubscriptionContracts.Begin(sub, retried.Write, clock), clock, clock, status);
            Assert.AreEqual(expected, finished.View.State);
            Assert.HasCount(6, finished.View.Attempts);
        }
        VerifySignature(sub);
    }

    private static void VerifySignature(ManagedSubscription sub)
    {
        byte[] body = Bytes(WireExpected["Notification"]!);
        byte[] key = RandomNumberGenerator.GetBytes(32), nextKey = RandomNumberGenerator.GetBytes(32);
        byte[]? Lookup(string id) => id == "primary" ? key : id == "replacement" ? nextKey : null;
        var headers = CallbackSignature.Sign(sub, Now, body, key);
        var stamp = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        byte[] signed = [.. Encoding.ASCII.GetBytes(stamp + "."), .. body];
        Assert.IsTrue(headers["X-Checkin-Signature"] == Convert.ToHexStringLower(HMACSHA256.HashData(key, signed)), "signed-bytes");
        Assert.AreEqual(stamp, headers["X-Checkin-Timestamp"]);
        Assert.AreEqual("primary", headers["X-Checkin-Key-Id"]);
        Assert.IsTrue(CallbackSignature.Verify(sub, headers, Now.AddMinutes(-5), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, headers, Now.AddMinutes(-5).AddTicks(-1), body, Lookup));
        Assert.IsTrue(CallbackSignature.Verify(sub, headers, Now.AddMinutes(-5).AddTicks(1), body, Lookup));
        Assert.IsTrue(CallbackSignature.Verify(sub, headers, Now.AddMinutes(5).AddTicks(-1), body, Lookup));
        Assert.IsTrue(CallbackSignature.Verify(sub, headers, Now.AddMinutes(5), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, headers, Now.AddMinutes(5).AddTicks(1), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, headers, Now.AddSeconds(-301), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, headers, Now, [.. body, 32], Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, headers.SetItem(CallbackSignature.SignatureHeader, new string('F', 64)), Now, body, Lookup));
        var rotated = SubscriptionContracts.Rotate(OperationHotel(), Provisioned, Id(40), sub, Bytes(WireExpected["Rotation"]!), Now).Write!;
        Assert.IsTrue(CallbackSignature.Verify(rotated, headers, Now.AddMinutes(5), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(rotated, headers, Now.AddMinutes(5).AddTicks(1), body, Lookup));
        var fresh = CallbackSignature.Sign(rotated, Now.AddSeconds(5), body, nextKey);
        Assert.AreEqual("replacement", fresh[CallbackSignature.KeyHeader]);
        Assert.AreNotEqual(stamp, fresh[CallbackSignature.TimestampHeader]);
        Assert.IsTrue(CallbackSignature.Verify(rotated, fresh, Now.AddSeconds(5), body, Lookup));
        Assert.IsFalse(CallbackSignature.Verify(sub, fresh, Now.AddSeconds(5), body, Lookup));
    }

    [TestMethod, TestCategory("SAL-02"), TestCategory("A05"), TestCategory("A09"), TestCategory("A10"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "wire/expected.json#Subscription,SubscriptionInput", "intake/restricted/snapshot.json")]
    public void ScopedResources()
    {
        var booking = RegistrationBooking();
        var sub = Subscribe();
        var wrong = AttendanceHotel(Enum.GetValues<HotelAction>(), "other");
        var caller = OperationHotel();
        var subscriptionsOnly = AttendanceHotel([HotelAction.ManageSubscriptions]);
        var histories = new Dictionary<Guid, RegistrationHistory>();
        foreach (var (integration, status) in new[] { ((HotelIntegration?)null, 401), (Hotel, 403), (wrong, 404) })
        {
            Assert.AreEqual(status, RegistrationStatus.Read(integration, booking.BookingId, booking, histories, []).Status);
            Assert.AreEqual(status, SubscriptionContracts.Denial(integration, Scope, sub, Id(40))!.Status);
            Assert.AreEqual(status, AttendanceContracts.Evaluate(integration, "actor", booking.BookingId, Id(2), null, booking, [], ReadOnlyMemory<byte>.Empty, Now).Status);
        }
        Assert.AreEqual(404, RegistrationStatus.Read(caller, Id(99), booking, histories, []).Status);
        Assert.AreEqual(404, AttendanceContracts.Evaluate(caller, "actor", booking.BookingId, Id(99), null, booking, [], ReadOnlyMemory<byte>.Empty, Now).Status);
        Assert.AreEqual(404, SubscriptionContracts.Denial(caller, Scope, sub, Id(99))!.Status);
        Assert.AreEqual(404, SubscriptionContracts.Retry(caller, Scope, Id(40), sub, Id(50), null).Response.Status);
        Assert.AreEqual(Access.Forbidden, HotelIntegration.Authorize(subscriptionsOnly, HotelAction.ManageRegistration, Scope));
        Assert.IsNull(SubscriptionContracts.Denial(subscriptionsOnly, Scope, sub, Id(40)));
        var other = sub with { Scope = new("salou", "other") };
        Assert.AreEqual(404, SubscriptionContracts.Get(caller, Scope, Id(40), other).Status);
        Expected("Subscription", SubscriptionContracts.Get(caller, Scope, Id(40), sub).Body);
        Assert.HasCount(1, (ImmutableArray<Subscription>)SubscriptionContracts.List(caller, Scope, [sub, other]).Body);
        var delivery = new DeliveryWork(Scope, Id(40), new(Id(50), Id(30), "pending", []), 0);
        var deliveries = SubscriptionContracts.Deliveries(caller, Scope, Id(40), sub,
            [delivery, delivery with { Scope = other.Scope }, delivery with { SubscriptionId = Id(99) }]);
        Assert.HasCount(1, (ImmutableArray<Delivery>)deliveries.Body);
        var op = Start(booking).Current!;
        var failed = Resolve(op.BeginSubmission(booking, Hotel.Zone, Now), DestinationResult.PermanentFailure, Now);
        Assert.AreSame(failed, failed.ManualRetry(wrong, "actor", booking, Now));
        Assert.AreSame(failed, failed.ManualRetry(subscriptionsOnly, "actor", booking, Now));
        foreach (var (field, value) in new (string, JsonNode?)[] {
            ("endpoint", "http://callbacks.example.test/salou"), ("endpoint", "https://elsewhere.example.test/x"),
            ("enabled", null), ("eventStates", new JsonArray()), ("eventStates", new JsonArray("processing", "processing")),
            ("eventStates", new JsonArray("internal")), ("unknown", true) })
        {
            var input = WireExpected["SubscriptionInput"]!.DeepClone();
            input[field] = value;
            var decision = SubscriptionContracts.Configure(caller, Provisioned, null, null, Bytes(input), "primary");
            Assert.AreEqual(400, decision.Response.Status, field);
            Assert.IsNull(decision.Write, field);
        }
        var unknown = SubscriptionContracts.Rotate(caller, Provisioned, Id(40), sub, "{\"keyId\":\"other\"}"u8.ToArray(), Now);
        Assert.AreEqual(400, unknown.Response.Status);
    }

    [TestMethod, TestCategory("FR-08"), TestCategory("FR-09"), TestCategory("A08"), TestCategory("SAL-02"), TestCategory("INT-01"), TestCategory("Evidence:contract")]
    [EvidenceFixtures("SAL", "wire/expected.json#BookingEvent,Status,Notification,AttendanceReceipt,Error,ExistingImportConflict", "intake/restricted/snapshot.json")]
    public void SafeReferences()
    {
        var input = EligibleSnapshot();
        var decision = Decide(input);
        var booking = decision.Accepted!;
        Expected("BookingEvent", decision.Event!);
        var newer = Changed(booking, input with { DepartureDate = input.DepartureDate.AddDays(1) });
        Assert.AreNotEqual(newer.SnapshotId, decision.Event!.SnapshotId);
        Assert.AreEqual(booking.SnapshotId, decision.Event.SnapshotId);
        object[] restricted = [input, input.Guests[0], input.Guests[0].Passport!, decision, booking,
            SalouSnapshotMapper.Decode(Bytes(Wire()), Scope, Hotel.LocalDate(Now))];
        foreach (var value in restricted) Assert.IsTrue(value.ToString() == value.GetType().Name, "restricted-formatting");
        Expected("Status", Status(booking, Start(booking)));
        Expected("Notification", Stopped());
        Expected("AttendanceReceipt", new AttendanceReceipt(Id(22)));
        Expected("Error", new SafeError("invalid_request", [new("$.status", "unsupported_status")]));
        Expected("ExistingImportConflict", IntakeResponse.From(Decide(input with { UpdatedAt = input.UpdatedAt.AddTicks(1) }, booking), true).Body);
        var invalid = Wire();
        var secret = input.Guests[0].FirstName;
        invalid[secret] = secret;
        var error = SalouSnapshotMapper.Decode(Bytes(invalid), Scope, Hotel.LocalDate(Now)).Error!;
        Assert.IsFalse(JsonSerializer.Serialize(error).Contains(secret, StringComparison.Ordinal), "redacted-error");
        Assert.IsFalse(error.ToString().Contains(secret, StringComparison.Ordinal), "redacted-diagnostic");
    }

    [TestMethod, TestCategory("INT-05"), TestCategory("A04"), TestCategory("A07"), TestCategory("A10"), TestCategory("Evidence:contract")]
    [TestCategory("FR-01"), TestCategory("FR-02"), TestCategory("FR-03"), TestCategory("FR-04"), TestCategory("FR-05")]
    [TestCategory("FR-06"), TestCategory("FR-07"), TestCategory("FR-08"), TestCategory("FR-09")]
    [EvidenceFixtures("SAL", "wire/expected.json#Status,Notification,SubscriptionInput,Subscription,Rotation,Delivery,"
        + "Attendance,Correction,AttendanceReceipt,BookingEvent,Error,ExistingImportConflict",
        "wire/operations.json", "intake/restricted/snapshot.json")]
    public void WireRoundTrip()
    {
        foreach (var (name, value) in WireExpected.AsObject())
        {
            Assert.IsTrue(Matches(Definitions[name]!, value), name);
            var extra = value!.DeepClone();
            extra["unknown"] = true;
            Assert.IsFalse(Matches(Definitions[name]!, extra), name);
            foreach (var required in Definitions[name]!["required"]!.AsArray())
            {
                var missing = value.DeepClone().AsObject();
                missing.Remove((string)required!);
                Assert.IsFalse(Matches(Definitions[name]!, missing), name);
            }
        }
        RoundTrip<RegistrationNotification>("Notification");
        RoundTrip<BookingStatus>("Status");
        RoundTrip<SubscriptionInput>("SubscriptionInput");
        RoundTrip<Subscription>("Subscription");
        RoundTrip<Rotation>("Rotation");
        RoundTrip<Delivery>("Delivery");
        RoundTrip<AttendanceReceipt>("AttendanceReceipt");
        RoundTrip<BookingEvent>("BookingEvent");
        RoundTrip<SafeError>("Error");
        RoundTrip<ExistingImportConflict>("ExistingImportConflict");
        Assert.IsTrue(Matches(Definitions["Snapshot"]!, Wire()), "snapshot");
        Assert.IsTrue(Matches(Definitions["Mappings"]!, JsonSerializer.SerializeToNode(RegistrationBooking().Mappings)), "mappings");
        var guest = WireExpected["Status"]!["guests"]![0]!;
        var schema = Definitions["Status"]!["properties"]!["guests"]!["items"]!;
        foreach (var operation in new[] { false, true })
        foreach (var status in new[] { false, true })
        foreach (var reason in new[] { false, true })
        {
            var row = guest.DeepClone();
            row["operationId"] = operation ? Id(20).ToString() : null;
            row["registrationStatus"] = status ? "processing" : null;
            row["reasonCode"] = reason ? "correct_data" : null;
            Assert.AreEqual(operation && status || !operation && !status && !reason, Matches(schema, row), "guest-tuple");
        }
        foreach (var status in new string?[] { null, "processing", "actionRequired", "failed", "completed" })
        foreach (var reason in new string?[] { null, "registration_stopped", "correct_data" })
        {
            var body = WireExpected["Notification"]!.DeepClone();
            body["registrationStatus"] = status;
            body["reasonCode"] = reason;
            Assert.AreEqual((status == null) == (reason == "registration_stopped"), Matches(Definitions["Notification"]!, body), "callback-tuple");
            body["operationId"] = null;
            Assert.IsFalse(Matches(Definitions["Notification"]!, body), "callback-operation");
        }
        var cancellation = new JsonObject { ["guestId"] = Id(2).ToString(), ["operationId"] = Id(20).ToString(),
            ["registrationStatus"] = "completed", ["reasonCode"] = null };
        Assert.IsFalse(Matches(Definitions["Status"]!["properties"]!["outstandingCancellations"]!["items"]!, cancellation));
        VerifyOperations();
    }

    private static void RoundTrip<T>(string name) => Expected(name, WireExpected[name]!.Deserialize<T>()!);

    private static bool Matches(JsonNode schema, JsonNode? value)
    {
        if (schema["$ref"] is { } reference) return Matches(Definitions[((string)reference)!.Split('/')[^1]]!, value);
        if (schema["enum"] is JsonArray choices && !choices.Any(c => JsonNode.DeepEquals(c, value))) return false;
        if (schema.AsObject().ContainsKey("const") && !JsonNode.DeepEquals(schema["const"], value)) return false;
        if (schema["not"] is { } no && Matches(no, value)) return false;
        if (schema["oneOf"] is JsonArray one && one.Count(c => Matches(c!, value)) != 1) return false;
        if (schema["anyOf"] is JsonArray any && !any.Any(c => Matches(c!, value))) return false;
        if (schema["allOf"] is JsonArray all && !all.All(c => Matches(c!, value))) return false;
        if (schema["if"] is { } condition && schema[Matches(condition, value) ? "then" : "else"] is { } branch && !Matches(branch, value)) return false;
        var type = value?.GetValueKind() ?? JsonValueKind.Null;
        if ((string?)schema["type"] is { } expected && expected != (type switch
        {
            JsonValueKind.Object => "object", JsonValueKind.Array => "array", JsonValueKind.Null => "null",
            JsonValueKind.String => "string", JsonValueKind.True or JsonValueKind.False => "boolean", JsonValueKind.Number => "integer", _ => ""
        })) return false;
        if (value is JsonObject obj)
        {
            if (schema["required"] is JsonArray required && required.Any(n => !obj.ContainsKey((string)n!))) return false;
            if (schema["properties"] is JsonObject properties)
                foreach (var (name, child) in obj)
                {
                    if (properties[name] is { } property) { if (!Matches(property, child)) return false; }
                    else if ((bool?)schema["additionalProperties"] == false) return false;
                }
        }
        if (value is JsonArray array)
        {
            if (array.Count < (int?)schema["minItems"] || array.Count > (int?)schema["maxItems"]) return false;
            if (schema["items"] is { } items && !array.All(item => Matches(items, item))) return false;
            if ((bool?)schema["uniqueItems"] == true && array.Select(n => n?.ToJsonString()).Distinct().Count() != array.Count) return false;
        }
        if (type == JsonValueKind.Number && (!value!.AsValue().TryGetValue<int>(out var integer) || integer < (int?)schema["minimum"])) return false;
        if (type != JsonValueKind.String) return true;
        string text = (string)value!;
        int length = text.EnumerateRunes().Count();
        if (length < (int?)schema["minLength"] || length > (int?)schema["maxLength"]) return false;
        if ((string?)schema["pattern"] is { } pattern && !Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant)) return false;
        return (string?)schema["format"] switch
        {
            "uuid" => Guid.TryParseExact(text, "D", out _),
            "date" => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "uri" => Uri.TryCreate(text, UriKind.Absolute, out _),
            _ => true
        };
    }

    private static void VerifyOperations()
    {
        var doc = ContractFile("openapi.json");
        var paths = doc["paths"]!.AsObject();
        var rows = Fixture("wire/operations").AsArray();
        Assert.AreEqual(rows.Count, paths.Sum(path => path.Value!.AsObject().Count(p => p.Key != "parameters")));
        const string reference = "./types.schema.json#/$defs/";
        foreach (var row in rows)
        {
            string id = (string)row!["operationId"]!;
            var op = paths[(string)row["path"]!]![(string)row["method"]!]!;
            Assert.AreEqual(id, (string?)op["operationId"], id);
            var body = (string?)row["body"];
            Assert.AreEqual(body == null ? null : reference + body, (string?)op["requestBody"]?["content"]?["application/json"]?["schema"]?["$ref"], id);
            var responses = op["responses"]!.AsObject();
            string[] success = ((string)row["codes"]!).Split(' '), errors = ((string)row["errors"]!).Split(' ');
            CollectionAssert.AreEquivalent(success.Concat(errors).ToArray(), responses.Select(x => x.Key).ToArray(), id);
            foreach (var (code, response) in responses)
            {
                string target = success.Contains(code) ? (string)row["success"]! : id == "importBooking" && code == "409" ? "ExistingImportConflict" : "Error";
                var schema = response!["content"]!["application/json"]!["schema"]!;
                if (target.EndsWith("[]", StringComparison.Ordinal))
                {
                    Assert.AreEqual("array", (string?)schema["type"], id);
                    target = target[..^2];
                    schema = schema["items"]!;
                }
                Assert.AreEqual(reference + target, (string?)schema["$ref"], id);
                Assert.IsNotNull(Definitions[target], id);
            }
        }
        var hook = doc["webhooks"]!["registrationChanged"]!["post"]!;
        Assert.AreEqual(reference + "Notification", (string?)hook["requestBody"]!["content"]!["application/json"]!["schema"]!["$ref"]);
        Assert.IsNotNull(hook["responses"]!["2XX"]);
        CollectionAssert.AreEquivalent(new[] { "X-Checkin-Key-Id", "X-Checkin-Timestamp", "X-Checkin-Signature" },
            hook["parameters"]!.AsArray().Select(p => (string)p!["name"]!).ToArray());
        Assert.IsTrue(hook["parameters"]!.AsArray().All(p => (string?)p!["in"] == "header" && (bool?)p["required"] == true));
    }
}
