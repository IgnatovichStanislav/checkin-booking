using System.Text.Json;
using System.Xml.Linq;

namespace Booking.ContractTests;

[TestClass]
public sealed class EvidenceCases
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private static readonly DateTimeOffset Started = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid FirstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondId = Guid.Parse("00000000-0000-0000-0000-000000000002");

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
    }

    [TestMethod, TestCategory("Evidence:harness")]
    public void KeepsFailuresAndPreservesSkippedAndMissingAsPending()
    {
        var map = ScenarioEvidence.CompiledMap();
        var failed = Find(map, nameof(ContractCases.FirstImport));
        var skipped = Find(map, nameof(ContractCases.DuplicateSnapshot));
        var missing = Find(map, nameof(ContractCases.StaleUpdate));
        var trx = Report(Started, [Definition(failed, FirstId), Definition(skipped, SecondId)],
            [Result(FirstId, "Failed"), Result(SecondId, "NotExecuted")]);
        var outcomes = ScenarioEvidence.Join(map, trx, Started).BusinessEvidence.ToDictionary(row => row.Method.Method);

        Assert.AreEqual(new MethodOutcome(failed, "Failed", "failed"), outcomes[failed.Method]);
        Assert.AreEqual(new MethodOutcome(skipped, "NotExecuted", "pending"), outcomes[skipped.Method]);
        Assert.AreEqual(new MethodOutcome(missing, null, "pending"), outcomes[missing.Method]);
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
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ScenarioEvidence.RunAsync(temp.FullName,
                _ => throw new AssertFailedException("Evidence must reject an existing directory before running tests.")));

            string current = Path.Combine(temp.FullName, "current");
            int exit = await ScenarioEvidence.RunAsync(current, args =>
            {
                CollectionAssert.AreEqual(new[] { "--report-trx", "--report-trx-filename", ScenarioEvidence.TrxFileName,
                    "--results-directory", current }, args);
                Report(DateTimeOffset.UtcNow, [Definition(first, FirstId)], [Result(FirstId, "Failed")])
                    .Save(Path.Combine(current, ScenarioEvidence.TrxFileName));
                return Task.FromResult(2);
            });

            Assert.AreEqual(2, exit);
            Assert.IsTrue(File.Exists(old));
            string report = File.ReadAllText(Path.Combine(current, ScenarioEvidence.ReportFileName));
            using var json = JsonDocument.Parse(report);
            var rows = json.RootElement.GetProperty("businessEvidence").EnumerateArray().ToArray();
            Assert.AreEqual("failed", rows.Single(row => row.GetProperty("method").GetProperty("method").GetString() == first.Method)
                .GetProperty("status").GetString());
            Assert.IsTrue(rows.Any(row => row.GetProperty("status").GetString() == "pending"));
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
            int exit = await ScenarioEvidence.RunAsync(current, _ =>
            {
                Report(DateTimeOffset.UtcNow, [Definition(first, FirstId)], [Result(FirstId, "Passed")])
                    .Save(Path.Combine(current, ScenarioEvidence.TrxFileName));
                return Task.FromResult(0);
            });

            Assert.AreEqual(1, exit);
            Assert.IsTrue(File.Exists(Path.Combine(current, ScenarioEvidence.ReportFileName)));
        }
        finally
        {
            temp.Delete(recursive: true);
        }
    }

    private static MethodEvidence Find(MethodEvidence[] methods, string name) => methods.Single(method => method.Method == name);

    private static XElement Definition(MethodEvidence method, Guid id) => new(Trx + "UnitTest", new XAttribute("id", id),
        new XElement(Trx + "TestMethod", new XAttribute("codeBase", $@"C:\tests\{method.Assembly.ToUpperInvariant()}.dll"),
            new XAttribute("className", $"{method.Class}, {method.Assembly}"),
            new XAttribute("name", $"{method.Class}.{method.Method}")));

    private static XElement Result(Guid id, string outcome) => new(Trx + "UnitTestResult",
        new XAttribute("testId", id), new XAttribute("outcome", outcome));

    private static XDocument Report(DateTimeOffset started, XElement[] definitions, XElement[] results) => new(
        new XElement(Trx + "TestRun", new XAttribute("id", Guid.NewGuid()),
            new XElement(Trx + "Times", new XAttribute("start", started), new XAttribute("finish", started.AddSeconds(1))),
            new XElement(Trx + "TestDefinitions", definitions),
            new XElement(Trx + "Results", results),
            new XElement(Trx + "ResultSummary", new XElement(Trx + "Counters", new XAttribute("total", results.Length)))));
}
