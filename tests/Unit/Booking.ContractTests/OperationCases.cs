using System.Text.Json.Nodes;
using Booking.Application.Salou;
using Booking.Contracts;
using Booking.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Booking.ContractTests;

public sealed partial class ContractCases
{
    private static JsonNode Fixture(string name) => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", name + ".json")))!;
    private static readonly JsonNode OpExpected = Fixture("expectations");
    private static BookingDto EligibleSnapshot()
    {
        var source = Snapshot();
        var guest = source.Guests[0];
        return source with { Guests = source.Guests.SetItem(0, guest with { Passport = guest.Passport! with { ExpiresOn = new DateOnly(2028, 1, 1) } }) };
    }

    private static AcceptedBooking RegistrationBooking() => Decide(EligibleSnapshot()).Accepted!;
    private static RegistrationHistory EmptyHistory => new([]);
    private static HotelIntegration OperationHotel(RegistrationPolicy policy = RegistrationPolicy.DefaultGpds)
    {
        Assert.IsTrue(HotelIntegration.TryConfigure("salou", ["salou-vip"], Enum.GetValues<HotelAction>(),
            "Europe/Madrid", policy, policy == RegistrationPolicy.LocalLpds ? "local" : "gpds", out var hotel));
        return hotel!;
    }

    private static RegistrationHistory Start(AcceptedBooking booking, int guest = 2) =>
        EmptyHistory.Evaluate(OperationHotel(), booking, Id(guest), Now, allocate: Ids(20));

    private static RegistrationOperation Resolve(RegistrationOperation op, DestinationResult result, DateTimeOffset now) =>
        op.Resolve(op.OperationId, op.RegistrationRequestId, op.Series, op.Attempts[^1].Number, result, now);

    private static AcceptedBooking Changed(AcceptedBooking previous, BookingDto input, int start = 60) =>
        BookingIntake.Decide(input with { UpdatedAt = previous.UpdatedAt.AddSeconds(1) }, previous.BookingId, previous,
            Hotel.Zone, Now, Ids(start)).Accepted!;

    [TestMethod, TestCategory("PDS-01"), TestCategory("PDS-02"), TestCategory("PDS-03"), TestCategory("PDS-06"), TestCategory("Evidence:contract")]
    public void OperationOutcomes()
    {
        var booking = RegistrationBooking();
        var history = Start(booking);
        var op = history.Current!;
        Assert.AreEqual((string?)OpExpected["ready"], RegistrationStatus.PublicState(op).Status);
        var missing = Start(booking, 3).Current!;
        Assert.AreEqual((string?)OpExpected["missing"], RegistrationStatus.PublicState(missing).Reason);
        Assert.IsTrue(missing.BeginSubmission(booking, Hotel.Zone, Now).Attempts.IsEmpty);
        var input = booking.Restricted!;
        var guest = input.Guests[0];
        var localDay = Hotel.LocalDate(Now);
        var expiresToday = guest with { Passport = guest.Passport! with { ExpiresOn = localDay } };
        var changed = Changed(booking, input with { Guests = input.Guests.SetItem(0, expiresToday) });
        var prepared = history.Evaluate(Hotel, changed, Id(2), Now, booking).Current!;
        Assert.AreEqual(OperationReason.Ready, prepared.Reason);
        var tomorrow = Instant("2026-10-25T23:00:00Z");
        Assert.AreEqual((string?)OpExpected["expired"], RegistrationStatus.PublicState(prepared.BeginSubmission(changed, Hotel.Zone, tomorrow)).Reason);
        var sent = op.BeginSubmission(booking, Hotel.Zone, Now);
        Assert.AreEqual(op.OperationId, sent.OperationId);
        Assert.AreSame(sent, sent.Resolve(Id(99), sent.RegistrationRequestId, 1, 1, DestinationResult.Confirmed, Now));
        Assert.AreSame(sent, sent.Resolve(sent.OperationId, Id(99), 1, 1, DestinationResult.Confirmed, Now));
        var confirmed = Resolve(sent, DestinationResult.Confirmed, Now);
        Assert.AreEqual((string?)OpExpected["confirmed"], RegistrationStatus.PublicState(confirmed).Status);
        Assert.AreSame(confirmed, Resolve(confirmed, DestinationResult.Rejected, Now));
        Assert.AreEqual(AttendanceStatus.Expected, Attendance.Status([], booking.BookingId, op.GuestId, 1));
        var rejected = Resolve(sent, DestinationResult.Rejected, Now);
        Assert.AreEqual((string?)OpExpected["rejected"], RegistrationStatus.PublicState(rejected).Reason);
        var prior = history.Replace(op, rejected);
        Assert.AreEqual(rejected.OperationId, prior.Evaluate(Hotel, booking, Id(2), Now).Current!.OperationId);
        var correction = prior.Evaluate(Hotel, changed, Id(2), Now, booking, allocate: Ids(80)).Current!;
        Assert.AreEqual(rejected.OperationId, correction.PriorOperationId);
        Assert.AreNotEqual(rejected.RegistrationRequestId, correction.RegistrationRequestId);
        Assert.AreEqual(changed.SnapshotId, correction.SnapshotId);
    }

    [TestMethod, TestCategory("PDS-04"), TestCategory("PDS-05"), TestCategory("Evidence:contract")]
    public void RetryAndReconcile()
    {
        var booking = RegistrationBooking();
        var original = Start(booking).Current!;
        var op = original;
        var clock = Now;
        var delays = OpExpected["delays"]!.AsArray();
        for (int n = 1; n <= 5; n++)
        {
            op = op.BeginSubmission(booking, Hotel.Zone, clock);
            Assert.AreEqual(n, op.Attempts.Length);
            Assert.AreEqual(booking.SnapshotId, op.Attempts[^1].SnapshotId);
            Assert.AreEqual(OperationReason.Uncertain, op.Reason);
            op = Resolve(op, DestinationResult.Unknown, clock);
            Assert.AreSame(op, op.BeginSubmission(booking, Hotel.Zone, clock.AddHours(1)));
            Assert.AreEqual((string?)OpExpected["uncertain"], RegistrationStatus.PublicState(op).Status);
            clock = clock.AddSeconds(2);
            op = Resolve(op, n == 1 ? DestinationResult.DefinitelyAbsent : DestinationResult.TemporaryFailure, clock);
            Assert.AreEqual(n, op.Attempts.Length);
            if (n == 5) break;
            clock = clock.AddSeconds((int)delays[n - 1]!);
            Assert.AreEqual(clock, op.NextAttemptAt);
            Assert.AreSame(op, op.BeginSubmission(booking, Hotel.Zone, clock.AddTicks(-1)));
        }
        Assert.AreEqual((string?)OpExpected["permanent"], RegistrationStatus.PublicState(op).Reason);
        Assert.AreSame(op, op.ManualRetry(Hotel, "operator", booking, clock));
        var retry = op.ManualRetry(OperationHotel(RegistrationPolicy.LocalLpds), "operator", booking, clock);
        Assert.AreEqual(original.DestinationId, retry.DestinationId);
        Assert.AreEqual(original.RegistrationRequestId, retry.RegistrationRequestId);
        Assert.AreEqual(5, retry.Attempts.Length);
        Assert.AreEqual(2, retry.Retries.Single().Series);
        var next = retry.BeginSubmission(booking, Hotel.Zone, clock);
        Assert.AreEqual(1, next.Attempts[^1].Number);
        Assert.AreEqual(2, next.Attempts[^1].Series);
        Assert.AreSame(next, next.Resolve(next.OperationId, next.RegistrationRequestId, 1, 1, DestinationResult.Confirmed, clock));
    }

    [TestMethod, TestCategory("PDS-07"), TestCategory("A06"), TestCategory("A09"), TestCategory("Evidence:contract")]
    public void RegistrationChange()
    {
        var booking = RegistrationBooking();
        var input = booking.Restricted!;
        var edited = Changed(booking, input with { DepartureDate = input.DepartureDate.AddDays(2) });
        var cancelled = Changed(booking, input with { ReservationStatus = ReservationStatus.Cancelled, CancelledAt = Now });
        var disabled = OperationHotel(RegistrationPolicy.Disabled);
        var local = OperationHotel(RegistrationPolicy.LocalLpds);
        Assert.IsNull(EmptyHistory.Evaluate(disabled, booking, Id(2), Now).Current);
        var history = EmptyHistory.Evaluate(local, booking, Id(2), Now, allocate: Ids(20));
        var op = history.Current!;
        var updated = history.Evaluate(disabled, edited, Id(2), Now, booking);
        Assert.AreEqual(op.OperationId, updated.Current!.OperationId);
        Assert.AreEqual(edited.SnapshotId, updated.Current.SnapshotId);
        Assert.AreEqual("local", updated.Current.DestinationId);
        var stopped = history.Evaluate(disabled, cancelled, Id(2), Now, booking);
        Assert.IsNull(stopped.Current);
        Assert.IsTrue(stopped.Operations.Single().IsStopped);
        var sent = op.BeginSubmission(booking, Hotel.Zone, Now);
        var uncertain = history.Replace(op, sent);
        Assert.AreSame(uncertain, uncertain.Evaluate(disabled, edited, Id(2), Now, booking));
        Assert.AreSame(uncertain, uncertain.Evaluate(disabled, cancelled, Id(2), Now, booking));
        var done = uncertain.Replace(sent, Resolve(sent, DestinationResult.Confirmed, Now));
        var otherGuest = input.Guests[1] with { Passport = input.Guests[0].Passport };
        var unrelated = Changed(booking, input with { Guests = input.Guests.SetItem(1, otherGuest) });
        var missingEvidence = done.Evaluate(disabled, unrelated, Id(2), Now);
        Assert.AreEqual(op.OperationId, missingEvidence.Current!.OperationId, "missing-comparison");
        Assert.AreEqual(unrelated.SnapshotId, missingEvidence.ComparisonNeededForSnapshotId);
        Assert.IsNull(RegistrationStatus.Change(done, missingEvidence, Id(75), Now).Outbox);
        var compared = missingEvidence.Evaluate(disabled, unrelated, Id(2), Now, booking);
        Assert.AreEqual(op.OperationId, compared.Current!.OperationId, "unrelated-guest");
        Assert.IsNull(compared.ComparisonNeededForSnapshotId);
        Assert.IsNull(RegistrationStatus.Change(done, compared, Id(75), Now).Outbox);
        var amend = done.Evaluate(disabled, edited, Id(2), Now, booking, allocate: Ids(80));
        var cancel = done.Evaluate(disabled, cancelled, Id(2), Now, booking, allocate: Ids(90));
        Assert.AreEqual(OperationKind.Amend, amend.Current!.Kind);
        Assert.AreEqual(OperationKind.Cancel, cancel.Current!.Kind);
        Assert.AreNotEqual(op.OperationId, cancel.Current.OperationId);
        Assert.AreEqual("local", cancel.Current.DestinationId);
        Assert.AreEqual(OperationState.Confirmed, cancel.Operations[0].State);
        var refreshed = Changed(cancelled, cancelled.Restricted! with { Guests = [input.Guests[0]] }, 110);
        var actionable = cancel.Evaluate(disabled, refreshed, Id(2), Now, cancelled);
        var attempt = actionable.Current!.BeginSubmission(refreshed, Hotel.Zone, Now);
        Assert.HasCount(1, attempt.Attempts, "cancel-later-snapshot");
        Assert.AreEqual(cancel.Current.RegistrationRequestId, attempt.RegistrationRequestId);
        var pending = cancel.Current.BeginSubmission(cancelled, Hotel.Zone, Now);
        var waiting = cancel.Replace(cancel.Current, pending);
        Assert.AreSame(waiting, waiting.Evaluate(disabled, refreshed, Id(2), Now, cancelled));
        foreach (var result in new[] { DestinationResult.TemporaryFailure, DestinationResult.PermanentFailure })
        {
            var known = waiting.Replace(pending, Resolve(pending, result, Now)).Evaluate(disabled, refreshed, Id(2), Now, cancelled);
            var retry = known.Current!;
            if (result == DestinationResult.PermanentFailure) retry = retry.ManualRetry(disabled, "operator", refreshed, Now);
            retry = retry.BeginSubmission(refreshed, Hotel.Zone, Now.AddSeconds(5));
            Assert.HasCount(2, retry.Attempts, "cancel-still-actionable");
            Assert.AreEqual(pending.RegistrationRequestId, retry.RegistrationRequestId);
            Assert.AreEqual("local", retry.DestinationId);
            Assert.IsTrue(retry.Attempts.All(a => a.SnapshotId == cancelled.SnapshotId), "cancel-content-lineage");
        }
        var completedCancel = waiting.Replace(pending, Resolve(pending, DestinationResult.Confirmed, Now));
        var restored = Changed(cancelled, input, 120);
        var restoredDisabled = completedCancel.Evaluate(disabled, restored, Id(2), Now, cancelled);
        Assert.IsNull(restoredDisabled.Current, "disabled-after-cancellation");
        Assert.AreEqual(OperationState.Confirmed, restoredDisabled.Operations[^1].State);
        var restoredDefault = completedCancel.Evaluate(Hotel, restored, Id(2), Now, cancelled, allocate: Ids(130));
        Assert.AreEqual("gpds", restoredDefault.Current!.DestinationId);
        Assert.AreNotEqual(pending.RegistrationRequestId, restoredDefault.Current.RegistrationRequestId);
        Assert.AreSame(cancel, cancel.Replace(sent, Resolve(sent, DestinationResult.Rejected, Now)));
        Assert.AreEqual(OperationState.Failed, done.Evaluate(disabled, cancelled, Id(2), Now, booking, false).Current!.State);
    }

    [TestMethod, TestCategory("A08"), TestCategory("Evidence:contract")]
    public void IdentityDeadline()
    {
        var prior = RegistrationBooking();
        var input = prior.Restricted!;
        var cancelled = input with { ReservationStatus = ReservationStatus.Cancelled, CancelledAt = Now };
        Assert.AreEqual(Instant((string)OpExpected["cancelled"]!), BookingIntake.IdentityDeadline(cancelled, Hotel.Zone));
        Assert.AreEqual(Instant((string)OpExpected["checkout"]!), prior.Deadline);
        var removed = Changed(prior, input with { Guests = [input.Guests[1]] });
        Assert.AreEqual(prior.Deadline, removed.Deadline);
        foreach (var ticks in new[] { 0, -1, 1 })
        {
            var now = Deadline.AddTicks(ticks);
            bool expired = ticks >= 0;
            var first = Decide(input, now: now);
            Assert.AreEqual(expired ? IntakeKind.Expired : IntakeKind.Create, first.Kind);
            foreach (bool put in new[] { false, true })
            {
                var equal = Decide(input, prior, put, now);
                Assert.AreEqual(expired ? IntakeKind.Open : IntakeKind.Duplicate, equal.Kind);
                Assert.AreEqual(expired, equal.ErasePriorIdentity);
                Assert.AreEqual(expired, equal.Accepted!.Restricted == null);
                Assert.IsNull(equal.RestrictedConflict);
                Assert.IsNull(equal.Event);
                if (expired) Assert.AreEqual(IntakeKind.Join, Decide(input, prior, put, now, equal.Intent).Kind);
            }
            var newer = input with { UpdatedAt = input.UpdatedAt.AddTicks(1) };
            var older = input with { UpdatedAt = input.UpdatedAt.AddTicks(-1) };
            Assert.AreEqual(IntakeKind.UsePut, Decide(newer, prior, false, now).Kind);
            Assert.AreEqual(IntakeKind.UsePut, Decide(older, prior, false, now).Kind);
            Assert.AreEqual(IntakeKind.Stale, Decide(older, prior, true, now).Kind);
            Assert.AreEqual(expired ? IntakeKind.Expired : IntakeKind.Apply, Decide(newer, prior, true, now).Kind);
            var future = Decide(newer with { DepartureDate = input.DepartureDate.AddDays(4) }, prior, true, now);
            Assert.AreEqual(IntakeKind.Apply, future.Kind);
            Assert.AreEqual(expired, future.ErasePriorIdentity);
            var unsent = Start(prior);
            var original = unsent.Current!;
            var nextBooking = future.Accepted!;
            var local = OperationHotel(RegistrationPolicy.LocalLpds);
            var evidenceAtDecision = expired ? new[] { BookingIntake.EraseIdentityIfDue(prior, now)!, prior } : [prior];
            foreach (var evidence in evidenceAtDecision)
            {
                var evaluated = unsent.Evaluate(local, nextBooking, Id(2), now, evidence, allocate: Ids(180));
                Assert.IsNull(evaluated.ComparisonNeededForSnapshotId);
                Assert.HasCount(expired ? 2 : 1, evaluated.Operations, "purge-before-worker");
                var active = evaluated.Current!;
                Assert.AreEqual(expired, original.OperationId != active.OperationId);
                Assert.AreEqual(expired, original.RegistrationRequestId != active.RegistrationRequestId);
                Assert.AreEqual(expired ? "local" : "gpds", active.DestinationId);
                if (expired)
                {
                    var retired = evaluated.Operations[0];
                    Assert.AreEqual(OperationReason.IdentityExpired, retired.Reason);
                    Assert.AreEqual(OperationState.Failed, retired.State);
                    Assert.AreEqual(original.SnapshotId, retired.SnapshotId);
                    Assert.AreEqual(original.RegistrationRequestId, retired.RegistrationRequestId);
                    Assert.AreEqual(original.OperationId, active.PriorOperationId);
                    Assert.IsTrue(retired.Attempts.IsEmpty);
                    Assert.AreSame(retired, retired.ManualRetry(local, "operator", nextBooking, now));
                }
                var sent = active.BeginSubmission(nextBooking, Hotel.Zone, now);
                Assert.HasCount(1, sent.Attempts);
                Assert.AreEqual(nextBooking.SnapshotId, sent.Attempts[0].SnapshotId);
            }
            foreach (var evidence in new AcceptedBooking?[] { null, prior with { SnapshotId = Id(999) },
                prior with { BookingId = Id(998) } })
            {
                var unknown = unsent.Evaluate(local, nextBooking, Id(2), now, evidence);
                Assert.AreEqual(nextBooking.SnapshotId, unknown.ComparisonNeededForSnapshotId, "missing-prior-retention-evidence");
                Assert.AreEqual(original.SnapshotId, unknown.Current!.SnapshotId);
                Assert.IsTrue(unknown.Current.BeginSubmission(nextBooking, Hotel.Zone, now).Attempts.IsEmpty);
            }
            var op = Start(prior).Current!.BeginSubmission(prior, Hotel.Zone, now);
            Assert.AreEqual(expired, op.Reason == OperationReason.IdentityExpired);
            if (expired)
            {
                var erased = BookingIntake.EraseIdentityIfDue(prior, now)!;
                Assert.IsNull(erased.Restricted);
                var retried = op.ManualRetry(OperationHotel(), "operator", erased, now);
                Assert.AreEqual((string?)OpExpected["erased"], RegistrationStatus.PublicState(retried).Reason);
                Assert.IsTrue(retried.Attempts.IsEmpty);
                Assert.IsTrue(retried.BeginSubmission(future.Accepted!, Hotel.Zone, now).Attempts.IsEmpty);
                var expiredHistory = new RegistrationHistory([op]);
                var reactivated = expiredHistory.Evaluate(OperationHotel(RegistrationPolicy.LocalLpds), future.Accepted!,
                    Id(2), now, erased, allocate: Ids(160));
                Assert.HasCount(2, reactivated.Operations, "expired-operation-is-historical");
                Assert.AreEqual(prior.SnapshotId, reactivated.Operations[0].SnapshotId);
                Assert.AreSame(op, reactivated.Operations[0].ManualRetry(OperationHotel(), "operator", future.Accepted!, now));
                Assert.AreNotEqual(op.OperationId, reactivated.Current!.OperationId);
                Assert.AreNotEqual(op.RegistrationRequestId, reactivated.Current.RegistrationRequestId);
                Assert.AreEqual("local", reactivated.Current.DestinationId);
                Assert.HasCount(1, reactivated.Current.BeginSubmission(future.Accepted!, Hotel.Zone, now).Attempts);
            }
        }
        var cancelDeadline = BookingIntake.IdentityDeadline(cancelled, Hotel.Zone)!.Value;
        var old = Changed(prior, cancelled);
        Assert.IsNull(BookingIntake.EraseIdentityIfDue(old, cancelDeadline.AddTicks(-1)));
        Assert.IsNull(BookingIntake.EraseIdentityIfDue(old, cancelDeadline)!.Restricted);
        Assert.IsNull(BookingIntake.EraseIdentityIfDue(old, cancelDeadline.AddTicks(1))!.Restricted);
        var preserved = Decide(cancelled with { UpdatedAt = input.UpdatedAt.AddSeconds(2) }, prior, true, cancelDeadline);
        Assert.AreEqual(IntakeKind.Expired, preserved.Kind);
        Assert.AreSame(prior, preserved.Accepted);
    }
}
