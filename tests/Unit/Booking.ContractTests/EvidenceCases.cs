using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;

namespace Booking.ContractTests;

[TestClass]
public sealed class EvidenceCases
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private static readonly DateTimeOffset Started = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly (string Assembly, string Class, string Method) FirstIdentity = ("Suite", "Suite.Cases", "First");
    private static readonly (string Assembly, string Class, string Method) SecondIdentity = ("Suite", "Suite.Cases", "Second");

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsEmptyOrDuplicateRequiredIdentities()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities([], [FirstIdentity]));
        var duplicate = ("suite", FirstIdentity.Class, FirstIdentity.Method);
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities(
            [FirstIdentity, duplicate], [FirstIdentity]));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsEmptyOrDuplicateDiscoveredIdentities()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities([FirstIdentity], []));
        var duplicate = ("suite", FirstIdentity.Class, FirstIdentity.Method);
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities(
            [FirstIdentity], [FirstIdentity, duplicate]));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsMissingOrUnexpectedDiscoveredIdentities()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities(
            [FirstIdentity, SecondIdentity], [FirstIdentity]));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities(
            [FirstIdentity], [FirstIdentity, SecondIdentity]));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities(
            [FirstIdentity], [SecondIdentity]));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void MatchesNormalizedAssemblyAndExactClassAndMethod()
    {
        var normalizedFirst = ("suite", FirstIdentity.Class, FirstIdentity.Method);
        ScenarioEvidence.ValidateRequiredIdentities([FirstIdentity, SecondIdentity], [SecondIdentity, normalizedFirst]);

        var differentClass = (FirstIdentity.Assembly, "Suite.cases", FirstIdentity.Method);
        var differentMethod = (FirstIdentity.Assembly, FirstIdentity.Class, "first");
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities([FirstIdentity], [differentClass]));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ValidateRequiredIdentities([FirstIdentity], [differentMethod]));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task RejectsInvalidInventoryBeforeReadingMissingFixtures()
    {
        var temp = Directory.CreateTempSubdirectory("booking-inventory-");
        try
        {
            string assemblyName = typeof(EvidenceCases).Assembly.GetName().Name!;
            foreach (string path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
                File.Copy(path, Path.Combine(temp.FullName, Path.GetFileName(path)));
            foreach (string name in new[] { assemblyName + ".deps.json", assemblyName + ".runtimeconfig.json", "scenario-catalog.md" })
                File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(temp.FullName, name));
            File.WriteAllText(Path.Combine(temp.FullName, "required-tests.json"), "[]");

            string runDirectory = Path.Combine(temp.FullName, "run");
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            start.ArgumentList.Add(Path.Combine(temp.FullName, assemblyName + ".dll"));
            start.ArgumentList.Add("--scenario-evidence");
            start.ArgumentList.Add(runDirectory);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var child = Process.Start(start)!;
            try
            {
                var error = child.StandardError.ReadToEndAsync(timeout.Token);
                var output = child.StandardOutput.ReadToEndAsync(timeout.Token);
                await child.WaitForExitAsync(timeout.Token);

                Assert.AreEqual(1, child.ExitCode);
                Assert.IsTrue((await error).Trim() == "Required test inventory is empty.", "Expected the inventory error first.");
                Assert.IsTrue(string.IsNullOrWhiteSpace(await output), "The test platform must not start.");
                Assert.IsFalse(File.Exists(Path.Combine(runDirectory, ScenarioEvidence.MapFileName)));
                Assert.IsFalse(File.Exists(Path.Combine(runDirectory, ScenarioEvidence.TrxFileName)));
                Assert.IsFalse(File.Exists(Path.Combine(runDirectory, ScenarioEvidence.ReportFileName)));
            }
            finally
            {
                if (!child.HasExited)
                {
                    child.Kill(entireProcessTree: true);
                    child.WaitForExit();
                }
            }
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void FirstImportJoinsCompiledMetadata()
    {
        var map = ScenarioEvidence.CompiledMap();
        var first = Find(map, nameof(ContractCases.FirstImport));
        XDocument trx = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        var outcome = ScenarioEvidence.Join(map, trx, Started).BusinessEvidence.Single(row => row.Status == "passed");

        Assert.AreEqual(first, outcome.Method);
        Assert.AreEqual("Passed", outcome.TrxOutcome);
        CollectionAssert.AreEqual(new[] { "INT-01", "SAL-01" }, outcome.Method.ScenarioIds);
        Assert.HasCount(0, outcome.Method.FeatureCaseIds);
        Assert.AreEqual("SAL", outcome.Method.IntakePath);
        Assert.AreEqual("contract", outcome.Method.EvidenceLevel);
        var fixture = outcome.Method.Fixtures.Single(fixture => fixture.Path.EndsWith("/intake/FirstImport.json", StringComparison.Ordinal));
        CollectionAssert.AreEqual(new[] { "first", "first-before", "first-at", "first-after", "first-failed" }, fixture.CaseIds);

        var wire = ScenarioEvidence.ReadFixture("wire/expected.json#Status");
        Assert.AreEqual("e2e/fixtures/001-booking-contracts/wire/expected.json", wire.Path);
        CollectionAssert.AreEqual(new[] { "Status" }, wire.CaseIds);
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ReadFixture("registration/missing.json"));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.ReadFixture("registration/expected.json#Status"));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void KeepsFailuresAndPreservesSkippedAndMissingAsPending()
    {
        var map = ScenarioEvidence.CompiledMap();
        var failed = Find(map, nameof(ContractCases.FirstImport));
        var skipped = Find(map, nameof(ContractCases.DuplicateSnapshot));
        var passed = Find(map, nameof(ContractCases.InvalidSnapshot));
        var missing = Find(map, nameof(ContractCases.StaleUpdate));
        var trx = Report(Started, [Definition(failed, FirstId), Definition(skipped, SecondId), Definition(passed, ThirdId)],
            [Result(FirstId, "Failed"), Result(SecondId, "NotExecuted"), Result(ThirdId, "Passed")]);
        Counters(trx).Add(new XAttribute("executed", 2), new XAttribute("passed", 1),
            new XAttribute("failed", 1), new XAttribute("timeout", 0), new XAttribute("notExecuted", 1));
        var outcomes = ScenarioEvidence.Join(map, trx, Started).BusinessEvidence.ToDictionary(row => row.Method.Method);

        Assert.AreEqual(new MethodOutcome(failed, "Failed", "failed"), outcomes[failed.Method]);
        Assert.AreEqual(new MethodOutcome(skipped, "NotExecuted", "pending"), outcomes[skipped.Method]);
        Assert.AreEqual(new MethodOutcome(passed, "Passed", "passed"), outcomes[passed.Method]);
        Assert.AreEqual(new MethodOutcome(missing, null, "pending"), outcomes[missing.Method]);

        Counters(trx).SetAttributeValue("executed", 1);
        Counters(trx).SetAttributeValue("failed", 0);
        Counters(trx).SetAttributeValue("timeout", 1);
        var timedOut = ScenarioEvidence.Join(map, trx, Started).BusinessEvidence.Single(row => row.Method == failed);
        Assert.AreEqual(new MethodOutcome(failed, "Failed", "failed"), timedOut);
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsDuplicateTestAndMapIdentities()
    {
        var map = ScenarioEvidence.CompiledMap();
        var first = Find(map, nameof(ContractCases.FirstImport));
        var second = Find(map, nameof(ContractCases.DuplicateSnapshot));

        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join([.. map, first with { Assembly = first.Assembly.ToUpperInvariant() }],
            Report(Started, [], []), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join([first with { ScenarioIds = ["INT-01", "INT-01"] }],
            Report(Started, [], []), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(first, FirstId), Definition(second, FirstId)], []), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(first, FirstId), Definition(first, SecondId)], []), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed"), Result(FirstId, "Failed")]), Started));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsUnmappedAndMalformedTrx()
    {
        var map = ScenarioEvidence.CompiledMap();
        var first = Find(map, nameof(ContractCases.FirstImport));
        var unknown = first with { Method = "UnknownMethod" };
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join([first with { ScenarioIds = ["INT-99"] }],
            Report(Started, [], []), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(unknown, FirstId)], [Result(FirstId, "Passed")]), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(first, FirstId)], [Result(SecondId, "Passed")]), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map,
            Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Unexpected")]), Started));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, new XDocument(new XElement("TestRun")), Started));

        XDocument partial = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        partial.Root!.Element(Trx + "ResultSummary")!.Remove();
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, partial, Started));

        XDocument inconsistent = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        inconsistent.Root!.Element(Trx + "Results")!.RemoveNodes();
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, inconsistent, Started));

        XDocument wrongPassFail = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        Counters(wrongPassFail).Add(new XAttribute("passed", 0), new XAttribute("failed", 1));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, wrongPassFail, Started));

        XDocument wrongSkipped = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "NotExecuted")]);
        Counters(wrongSkipped).Add(new XAttribute("notExecuted", 0));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, wrongSkipped, Started));

        XDocument wrongTimeout = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        Counters(wrongTimeout).Add(new XAttribute("timeout", 1));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, wrongTimeout, Started));

        XDocument wrongExecuted = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        Counters(wrongExecuted).Add(new XAttribute("executed", 0));
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, wrongExecuted, Started));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void KeepsHarnessChecksSeparateAndInt06RuntimePending()
    {
        var map = ScenarioEvidence.CompiledMap();
        var harness = Find(map, nameof(KeepsHarnessChecksSeparateAndInt06RuntimePending));
        var report = ScenarioEvidence.Join(map, Report(Started, [Definition(harness, FirstId)], [Result(FirstId, "Passed")]), Started);

        Assert.AreEqual("passed", report.HarnessChecks.Single(row => row.Method == harness).Status);
        Assert.IsTrue(report.HarnessChecks.All(row => row.Method.ScenarioIds.Length == 0 && row.Method.FeatureCaseIds.Length == 0));
        Assert.IsTrue(report.BusinessEvidence.All(row => row.Method.EvidenceLevel == "contract" && row.Status == "pending"));
        Assert.IsFalse(report.BusinessEvidence.Any(row => row.Method.ScenarioIds.Contains("INT-06")));
        var fakeControl = map.Select(method => method == harness ? method : method with { ScenarioIds = ["INT-06"] }).ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(fakeControl, Report(Started, [], []), Started));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task UsesOnlyFreshRunFiles()
    {
        var map = ScenarioEvidence.CompiledMap();
        var first = Find(map, nameof(ContractCases.FirstImport));
        var stale = Report(Started, [Definition(first, FirstId)], [Result(FirstId, "Passed")]);
        Assert.ThrowsExactly<InvalidDataException>(() => ScenarioEvidence.Join(map, stale, Started.AddTicks(1)));

        var temp = Directory.CreateTempSubdirectory("booking-evidence-");
        try
        {
            string old = Path.Combine(temp.FullName, ScenarioEvidence.TrxFileName);
            stale.Save(old);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ScenarioEvidence.RunAsync(new(temp.FullName, null),
                (_, _) => throw new AssertFailedException("Evidence must reject an existing directory before running tests.")));

            string current = Path.Combine(temp.FullName, "current");
            int exit = await ScenarioEvidence.RunAsync(new(current, null), async (collector, args) =>
            {
                CollectionAssert.AreEqual(new[] { "--results-directory", current }, args);
                await ObserveAsync(collector, new FailedTestNodeStateProperty(), Identifier(first));
                return 2;
            });

            Assert.AreEqual(2, exit);
            Assert.IsTrue(File.Exists(old));
            string report = File.ReadAllText(Path.Combine(current, ScenarioEvidence.ReportFileName));
            using var json = JsonDocument.Parse(report);
            var rows = json.RootElement.GetProperty("businessEvidence").EnumerateArray().ToArray();
            Assert.AreEqual("failed", rows.Single(row => row.GetProperty("method").GetProperty("method").GetString() == first.Method)
                .GetProperty("status").GetString());
            Assert.IsTrue(rows.Any(row => row.GetProperty("status").GetString() == "pending"));
            Assert.AreEqual(new ExecutionReceipt(1, 2, "failed", "incomplete", "missing-test-results", null, null,
                json.RootElement.GetProperty("testRunId").GetGuid(), false, false, false), ReadReceipt(current));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task IncompleteRunCannotReturnSuccess()
    {
        var first = Find(ScenarioEvidence.CompiledMap(), nameof(ContractCases.FirstImport));
        var temp = Directory.CreateTempSubdirectory("booking-evidence-");
        try
        {
            string current = Path.Combine(temp.FullName, "current");
            int exit = await ScenarioEvidence.RunAsync(new(current, null), async (collector, _) =>
            {
                await ObserveAsync(collector, PassedTestNodeStateProperty.CachedInstance, Identifier(first));
                return 0;
            });

            Assert.AreEqual(1, exit);
            Assert.IsTrue(File.Exists(Path.Combine(current, ScenarioEvidence.ReportFileName)));
            var receipt = ReadReceipt(current);
            Assert.AreEqual(0, receipt.PlatformExitCode);
            Assert.AreEqual("passed", receipt.TestOutcome);
            Assert.AreEqual("incomplete", receipt.EvidenceOutcome);
            Assert.AreEqual("missing-test-results", receipt.FailureCode);
            Assert.IsNotNull(receipt.JoinedRunId);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task CapturesOnlyKnownTerminalFields()
    {
        string sentinel = "synthetic-evidence-privacy-sentinel";
        var map = ScenarioEvidence.CompiledMap()[..7];
        var collector = new SafeTestEvidence(map);
        IProperty[] states =
        [
            PassedTestNodeStateProperty.CachedInstance, new FailedTestNodeStateProperty(new Exception(sentinel), sentinel),
            new SkippedTestNodeStateProperty(sentinel), new ErrorTestNodeStateProperty(sentinel),
            new TimeoutTestNodeStateProperty(sentinel)
        ];
        await ObserveAsync(collector, new DiscoveredTestNodeStateProperty(sentinel), Identifier(map[0]));
        await ObserveAsync(collector, new InProgressTestNodeStateProperty(sentinel));
        for (int index = 0; index < states.Length; index++)
        {
            await ObserveAsync(collector, states[index], Identifier(map[index]),
                new StandardOutputProperty(sentinel), new StandardErrorProperty(sentinel),
                new FileArtifactProperty(new FileInfo(sentinel), sentinel, sentinel));
        }

        XDocument trx = collector.CreateReport(Started, Started.AddSeconds(1));
        var report = ScenarioEvidence.Join(map, trx, Started);
        var rows = report.BusinessEvidence.Concat(report.HarnessChecks).ToDictionary(row => row.Method.Identity);
        CollectionAssert.AreEqual(new[] { "Passed", "Failed", "Skipped", "Error", "Timeout" },
            map.Take(states.Length).Select(method => rows[method.Identity].TrxOutcome).ToArray());
        Assert.IsNull(rows[map[5].Identity].TrxOutcome);
        Assert.IsNull(rows[map[6].Identity].TrxOutcome);
        Assert.AreEqual(5, (int)Counters(trx).Attribute("total")!);
        Assert.IsFalse(trx.ToString().Contains(sentinel, StringComparison.Ordinal), "Platform diagnostics entered the safe XML.");
        Assert.IsTrue(trx.Descendants().All(node => node.Name.LocalName is "TestRun" or "Times" or "TestDefinitions"
            or "UnitTest" or "TestMethod" or "Results" or "UnitTestResult" or "ResultSummary" or "Counters"));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task RejectsMissingAmbiguousAndUnknownTerminalIdentities()
    {
        var method = ScenarioEvidence.CompiledMap()[0];
        var passed = PassedTestNodeStateProperty.CachedInstance;
        var id = Identifier(method);
        IProperty[][] invalidUpdates =
        [
            [passed],
            [passed, id, Identifier(method with { Method = "AnotherMethod" })],
            [passed, Identifier(method with { Method = "UnknownMethod" })],
            [passed, Identifier(method, "AnotherAssembly")],
            [passed, Identifier(method, arity: 1)]
        ];
        foreach (var update in invalidUpdates)
        {
            var collector = new SafeTestEvidence([method]);
            await ObserveAsync(collector, update);
            Assert.ThrowsExactly<InvalidDataException>(() => collector.CreateReport(Started, Started));
        }

        var ambiguous = new SafeTestEvidence([method, method with { Assembly = "AnotherAssembly" }]);
        await ObserveAsync(ambiguous, passed, id);
        Assert.ThrowsExactly<InvalidDataException>(() => ambiguous.CreateReport(Started, Started));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task RejectsDuplicateTerminalEventsAndInvalidStates()
    {
        var method = ScenarioEvidence.CompiledMap()[0];
        var passed = PassedTestNodeStateProperty.CachedInstance;
        var id = Identifier(method);
        var collector = new SafeTestEvidence([method]);
        await ObserveAsync(collector, passed, id);
        await ObserveAsync(collector, new FailedTestNodeStateProperty(), id);
        Assert.ThrowsExactly<InvalidDataException>(() => collector.CreateReport(Started, Started));

        IProperty[][] invalidUpdates =
        [
            [id],
            [id, new UnsupportedTestState()]
        ];
        foreach (var update in invalidUpdates)
        {
            collector = new SafeTestEvidence([method]);
            await ObserveAsync(collector, update);
            Assert.ThrowsExactly<InvalidDataException>(() => collector.CreateReport(Started, Started));
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void RejectsInvalidEvidenceArguments()
    {
        Assert.IsNull(EvidenceEntryPoint.ParseArguments([]));
        Assert.IsNull(EvidenceEntryPoint.ParseArguments(["--list-tests"]));
        Assert.AreEqual(new EvidenceInvocation("suite", null),
            EvidenceEntryPoint.ParseArguments(["--scenario-evidence", "suite"]));
        Assert.AreEqual(new EvidenceInvocation("suite", "run.json"),
            EvidenceEntryPoint.ParseArguments(["--scenario-evidence", "suite", "--run-context", "run.json"]));
        string[][] invalid =
        [
            ["--scenario-evidence"], ["--scenario-evidence", ""], ["--scenario-evidence", "--list-tests"],
            ["--scenario-evidence", "suite", "--run-context"], ["--scenario-evidence", "suite", "--run-context", "  "],
            ["--scenario-evidence", "suite", "--run-context", "--report-trx"], ["--run-context", "run.json"],
            ["--list-tests", "--scenario-evidence", "suite"],
            ["--scenario-evidence", "suite", "--scenario-evidence", "other"],
            ["--scenario-evidence", "suite", "--run-context", "run.json", "--run-context", "other"],
            ["--scenario-evidence", "suite", "--report-trx"]
        ];
        foreach (var args in invalid)
            Assert.ThrowsExactly<InvalidDataException>(() => EvidenceEntryPoint.ParseArguments(args));
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task RejectsMissingMalformedAndMismatchedRunContext()
    {
        var temp = Directory.CreateTempSubdirectory("booking-context-");
        try
        {
            string digest = new('a', 64);
            string valid = ContextJson(digest);
            string?[] invalid = [null, "{", "{}", valid.Replace(digest, "bad"), valid.Replace("suite\"}", "../outside\"}"),
                valid.Replace("\"suite\"}", "\"different\"}"), valid.Replace("\"runId\"", "\"other\""),
                valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")];
            for (int index = 0; index < invalid.Length; index++)
            {
                string parent = Directory.CreateDirectory(Path.Combine(temp.FullName, $"case-{index}")).FullName;
                string context = Path.Combine(parent, "run.json");
                if (invalid[index] is { } json)
                    File.WriteAllText(context, json);
                string suite = Path.Combine(parent, "suite");
                int exit = await ScenarioEvidence.RunAsync(new(suite, context),
                    (_, _) => throw new AssertFailedException("Invalid context must prevent platform execution."));
                Assert.AreEqual(1, exit);
                Assert.AreEqual(new ExecutionReceipt(1, null, "pending", "failed", "invalid-run-context", null, null,
                    null, false, false, false), ReadReceipt(suite));
                Assert.IsFalse(File.Exists(Path.Combine(suite, ScenarioEvidence.MapFileName)));
            }

            string wrongParent = Path.Combine(temp.FullName, "run.json");
            File.WriteAllText(wrongParent, valid);
            string mismatched = Path.Combine(temp.FullName, "another-run", "suite");
            Assert.AreEqual(1, await ScenarioEvidence.RunAsync(new(mismatched, wrongParent),
                (_, _) => throw new AssertFailedException("A different invocation context must prevent platform execution.")));
            Assert.AreEqual("invalid-run-context", ReadReceipt(mismatched).FailureCode);
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task BindsReceiptToContextAndJoinedRunWhilePreservingPlatformFailure()
    {
        var temp = Directory.CreateTempSubdirectory("booking-receipt-");
        try
        {
            string context = Path.Combine(temp.FullName, "run.json");
            string suite = Path.Combine(temp.FullName, "suite");
            string digest = new('b', 64);
            File.WriteAllText(context, ContextJson(digest));
            var map = ScenarioEvidence.CompiledMap();
            int exit = await ScenarioEvidence.RunAsync(new(suite, context), async (collector, _) =>
            {
                await ObserveAsync(collector, new FailedTestNodeStateProperty(), Identifier(map[0]));
                foreach (var method in map.Skip(1))
                    await ObserveAsync(collector, PassedTestNodeStateProperty.CachedInstance, Identifier(method));
                return 2;
            });
            Assert.AreEqual(2, exit);
            using var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(suite, ScenarioEvidence.ReportFileName)));
            Assert.AreEqual(new ExecutionReceipt(1, 2, "failed", "passed", "platform-tests-failed", FirstId, digest,
                result.RootElement.GetProperty("testRunId").GetGuid(), false, false, false), ReadReceipt(suite));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public async Task ReceiptsDistinguishStandalonePassMismatchedExitAndInterruptedRun()
    {
        var temp = Directory.CreateTempSubdirectory("booking-receipt-");
        try
        {
            var map = ScenarioEvidence.CompiledMap();
            async Task<int> AllPassed(SafeTestEvidence collector, int exit)
            {
                foreach (var method in map)
                    await ObserveAsync(collector, PassedTestNodeStateProperty.CachedInstance, Identifier(method));
                return exit;
            }

            string standalone = Path.Combine(temp.FullName, "standalone");
            Assert.AreEqual(0, await ScenarioEvidence.RunAsync(new(standalone, null), (collector, _) => AllPassed(collector, 0)));
            var receipt = ReadReceipt(standalone);
            Assert.AreEqual(new ExecutionReceipt(1, 0, "passed", "passed", null, null, null,
                receipt.JoinedRunId, false, false, false), receipt);
            Assert.IsNotNull(receipt.JoinedRunId);

            string mismatch = Path.Combine(temp.FullName, "mismatch");
            Assert.AreEqual(2, await ScenarioEvidence.RunAsync(new(mismatch, null), (collector, _) => AllPassed(collector, 2)));
            receipt = ReadReceipt(mismatch);
            Assert.AreEqual(2, receipt.PlatformExitCode);
            Assert.AreEqual("failed", receipt.TestOutcome);
            Assert.AreEqual("failed", receipt.EvidenceOutcome);
            Assert.AreEqual("platform-result-mismatch", receipt.FailureCode);
            Assert.IsNull(receipt.JoinedRunId);
            Assert.IsFalse(File.Exists(Path.Combine(mismatch, ScenarioEvidence.ReportFileName)));

            string falseSuccess = Path.Combine(temp.FullName, "false-success");
            Assert.AreEqual(1, await ScenarioEvidence.RunAsync(new(falseSuccess, null), async (collector, _) =>
            {
                await ObserveAsync(collector, new FailedTestNodeStateProperty(), Identifier(map[0]));
                return 0;
            }));
            receipt = ReadReceipt(falseSuccess);
            Assert.AreEqual(0, receipt.PlatformExitCode);
            Assert.AreEqual("passed", receipt.TestOutcome);
            Assert.AreEqual("failed", receipt.EvidenceOutcome);
            Assert.AreEqual("platform-result-mismatch", receipt.FailureCode);

            string empty = Path.Combine(temp.FullName, "empty");
            Assert.AreEqual(1, await ScenarioEvidence.RunAsync(new(empty, null), (_, _) => Task.FromResult(0)));
            receipt = ReadReceipt(empty);
            Assert.AreEqual("incomplete", receipt.EvidenceOutcome);
            Assert.AreEqual("missing-test-results", receipt.FailureCode);
            Assert.IsNull(receipt.JoinedRunId);
            Assert.IsFalse(File.Exists(Path.Combine(empty, ScenarioEvidence.ReportFileName)));

            string interrupted = Path.Combine(temp.FullName, "interrupted");
            Assert.AreEqual(1, await ScenarioEvidence.RunAsync(new(interrupted, null),
                (_, _) => throw new OperationCanceledException("synthetic-evidence-privacy-sentinel")));
            Assert.AreEqual(new ExecutionReceipt(1, null, "incomplete", "incomplete", "platform-execution-incomplete",
                null, null, null, false, false, false), ReadReceipt(interrupted));
            Assert.IsFalse(File.Exists(Path.Combine(interrupted, ScenarioEvidence.TrxFileName)));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private sealed class UnsupportedTestState() : TestNodeStateProperty(null);

    private static TestMethodIdentifierProperty Identifier(MethodEvidence method, string assembly = "", int arity = 0)
    {
        int split = method.Class.LastIndexOf('.');
        return new(assembly, method.Class[..split], method.Class[(split + 1)..], method.Method, arity, [], "System.Void");
    }

    private static Task ObserveAsync(SafeTestEvidence collector, params IProperty[] properties) =>
        collector.ConsumeAsync(null!, new TestNodeUpdateMessage(new SessionUid("session"), new TestNode
        {
            Uid = new TestNodeUid("synthetic-evidence-privacy-sentinel"),
            DisplayName = "synthetic-evidence-privacy-sentinel",
            Properties = new PropertyBag(properties)
        }), CancellationToken.None);

    private static ExecutionReceipt ReadReceipt(string directory)
    {
        string text = File.ReadAllText(Path.Combine(directory, ScenarioEvidence.ReceiptFileName));
        using var json = JsonDocument.Parse(text);
        Assert.AreEqual(JsonValueKind.False, json.RootElement.GetProperty("policyVerified").ValueKind);
        return JsonSerializer.Deserialize<ExecutionReceipt>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static string ContextJson(string digest) =>
        $$$"""{"schemaVersion":1,"runId":"{{{FirstId}}}","source":{"digest":"{{{digest}}}"},"evidence":{"suite":"suite"}}""";

    private static MethodEvidence Find(MethodEvidence[] methods, string name) => methods.Single(method => method.Method == name);

    private static XElement Definition(MethodEvidence method, Guid id) => new(Trx + "UnitTest", new XAttribute("id", id),
        new XElement(Trx + "TestMethod", new XAttribute("codeBase", $@"C:\tests\{method.Assembly.ToUpperInvariant()}.dll"),
            new XAttribute("className", $"{method.Class}, {method.Assembly}"),
            new XAttribute("name", $"{method.Class}.{method.Method}")));

    private static XElement Result(Guid id, string outcome) => new(Trx + "UnitTestResult",
        new XAttribute("testId", id), new XAttribute("outcome", outcome));

    private static XElement Counters(XDocument document) => document.Root!.Element(Trx + "ResultSummary")!.Element(Trx + "Counters")!;

    private static XDocument Report(DateTimeOffset started, XElement[] definitions, XElement[] results) => new(
        new XElement(Trx + "TestRun", new XAttribute("id", Guid.NewGuid()),
            new XElement(Trx + "Times", new XAttribute("start", started), new XAttribute("finish", started.AddSeconds(1))),
            new XElement(Trx + "TestDefinitions", definitions),
            new XElement(Trx + "Results", results),
            new XElement(Trx + "ResultSummary", new XElement(Trx + "Counters", new XAttribute("total", results.Length)))));
}
