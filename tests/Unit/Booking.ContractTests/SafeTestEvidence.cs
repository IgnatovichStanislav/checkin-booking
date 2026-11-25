using System.Xml.Linq;
using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Booking.ContractTests;

internal sealed class SafeTestEvidence(MethodEvidence[] methods) : IDataConsumer
{
    private readonly object gate = new();
    private readonly ILookup<(string Class, string Method), MethodEvidence> compiledMethods =
        methods.ToLookup(method => (method.Class, method.Method));
    private readonly Dictionary<MethodEvidence, string> outcomes = [];
    private bool invalidEvent;

    public string Uid => "Booking.ContractTests.SafeTestEvidence";
    public string Version => "1.0.0";
    public string DisplayName => "Booking contract evidence";
    public string Description => "Collects known test method outcomes.";
    public Type[] DataTypesConsumed => [typeof(TestNodeUpdateMessage)];
    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (value is TestNodeUpdateMessage update)
        {
            lock (gate)
                Observe(update.TestNode.Properties);
        }
        return Task.CompletedTask;
    }

    private void Observe(PropertyBag properties)
    {
        var states = properties.OfType<TestNodeStateProperty>().Take(2).ToArray();
        if (states is [DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty])
            return;

        string? outcome = states switch
        {
            [PassedTestNodeStateProperty] => "Passed",
            [FailedTestNodeStateProperty] => "Failed",
            [SkippedTestNodeStateProperty] => "Skipped",
            [ErrorTestNodeStateProperty] => "Error",
            [TimeoutTestNodeStateProperty] => "Timeout",
            _ => null
        };
        var identifiers = properties.OfType<TestMethodIdentifierProperty>().Take(2).ToArray();
        if (outcome is null || identifiers is not [{ MethodArity: 0, ParameterTypeFullNames.Length: 0 } identifier])
        {
            invalidEvent = true;
            return;
        }

        string className = string.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : identifier.Namespace + "." + identifier.TypeName;
        var candidates = compiledMethods[(className, identifier.MethodName)]
            .Where(method => string.IsNullOrEmpty(identifier.AssemblyFullName)
                || string.Equals(method.Assembly, identifier.AssemblyFullName.Split(',', 2)[0], StringComparison.OrdinalIgnoreCase))
            .Take(2).ToArray();
        if (candidates.Length != 1 || !outcomes.TryAdd(candidates[0], outcome))
            invalidEvent = true;
    }

    internal XDocument CreateReport(DateTimeOffset started, DateTimeOffset finished)
    {
        lock (gate)
        {
            if (invalidEvent)
                throw new InvalidDataException("The test platform emitted an invalid terminal test event.");

            XNamespace trx = ScenarioEvidence.Trx;
            var definitions = new XElement(trx + "TestDefinitions");
            var results = new XElement(trx + "Results");
            foreach (var (method, outcome) in outcomes.OrderBy(row => row.Key.Class, StringComparer.Ordinal)
                         .ThenBy(row => row.Key.Method, StringComparer.Ordinal))
            {
                var id = Guid.NewGuid();
                definitions.Add(new XElement(trx + "UnitTest", new XAttribute("id", id),
                    new XElement(trx + "TestMethod", new XAttribute("codeBase", method.Assembly + ".dll"),
                        new XAttribute("className", method.Class), new XAttribute("name", method.Method))));
                results.Add(new XElement(trx + "UnitTestResult", new XAttribute("testId", id), new XAttribute("outcome", outcome)));
            }

            int Count(string status) => outcomes.Values.Count(outcome => outcome == status);
            var counters = new XElement(trx + "Counters", new XAttribute("total", outcomes.Count),
                new XAttribute("passed", Count("Passed")), new XAttribute("failed", Count("Failed")),
                new XAttribute("notExecuted", Count("Skipped")), new XAttribute("error", Count("Error")),
                new XAttribute("timeout", Count("Timeout")));
            return new XDocument(new XElement(trx + "TestRun", new XAttribute("id", Guid.NewGuid()),
                new XElement(trx + "Times", new XAttribute("start", started), new XAttribute("finish", finished)),
                definitions, results, new XElement(trx + "ResultSummary", counters)));
        }
    }
}

internal static class SafeEvidenceBuilderHook
{
    private static readonly AsyncLocal<SafeTestEvidence?> Current = new();

    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
    {
        if (Current.Value is { } collector)
            builder.TestHost.AddDataConsumer(_ => collector);
    }

    internal static async Task<int> RunAsync(SafeTestEvidence collector, string[] args)
    {
        var previous = Current.Value;
        Current.Value = collector;
        try
        {
            return await MicrosoftTestingPlatformApplication.RunAsync(
                [.. args, "--ansi", "off", "--progress", "off", "--show-test-results", "none",
                    "--show-stdout", "None", "--show-stderr", "None", "--show-flaky-tests", "off"]);
        }
        finally
        {
            Current.Value = previous;
        }
    }
}
