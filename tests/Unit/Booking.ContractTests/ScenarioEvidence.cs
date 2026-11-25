using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Booking.ContractTests;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class EvidenceFixturesAttribute(string intakePath, params string[] fixtures) : Attribute
{
    public string IntakePath { get; } = intakePath;
    public string[] Fixtures { get; } = fixtures;
}

internal sealed record FixtureReference(string Path, string[] CaseIds);

internal sealed record MethodEvidence(
    string Assembly, string Class, string Method, string? IntakePath, string EvidenceLevel,
    string[] ScenarioIds, string[] FeatureCaseIds, string[] RequirementIds, FixtureReference[] Fixtures)
{
    internal (string Assembly, string Class, string Method) Identity => NormalizeIdentity(Assembly, Class, Method);

    internal static (string Assembly, string Class, string Method) NormalizeIdentity(string assembly, string className, string method) =>
        (assembly.ToLowerInvariant(), className, method);
}

internal sealed record MethodOutcome(MethodEvidence Method, string? TrxOutcome, string Status);
internal sealed record EvidenceReport(Guid TestRunId, MethodOutcome[] BusinessEvidence, MethodOutcome[] HarnessChecks);
internal sealed record ExecutionReceipt(int SchemaVersion, int? PlatformExitCode, string TestOutcome,
    string EvidenceOutcome, string? FailureCode, Guid? RunId, string? SourceDigest, Guid? JoinedRunId,
    bool SourceVerified, bool VerificationEligible);

internal static class ScenarioEvidence
{
    internal const string MapFileName = "scenario-map.json";
    internal const string TrxFileName = "booking-contracts.trx";
    internal const string ReportFileName = "scenario-results.json";
    internal const string ReceiptFileName = "execution.json";
    private const string RequiredTestsFileName = "required-tests.json";
    private const string FixtureRoot = "e2e/fixtures/001-booking-contracts/";
    internal static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly Lazy<HashSet<string>> Catalog = new(ReadCatalog);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed record RequiredTestIdentity(string Assembly, string Class, string Method);

    private sealed record RunContext(Guid RunId, string SourceDigest);

    internal static void ValidateRequiredIdentities(
        IEnumerable<(string Assembly, string Class, string Method)> required,
        IEnumerable<(string Assembly, string Class, string Method)> discovered)
    {
        var requiredSet = UniqueIdentities(required, "Required test inventory is empty.", "Duplicate required test identity.");
        var discoveredSet = UniqueIdentities(discovered, "Discovered test inventory is empty.", "Duplicate discovered test identity.");
        Require(requiredSet.IsSubsetOf(discoveredSet), "A required test identity was not discovered.");
        Require(discoveredSet.IsSubsetOf(requiredSet), "An unexpected test identity was discovered.");
    }

    private static HashSet<(string Assembly, string Class, string Method)> UniqueIdentities(
        IEnumerable<(string Assembly, string Class, string Method)> identities, string empty, string duplicate)
    {
        var unique = new HashSet<(string Assembly, string Class, string Method)>();
        foreach (var (assembly, className, method) in identities)
        {
            Require(!string.IsNullOrWhiteSpace(assembly) && !string.IsNullOrWhiteSpace(className)
                && !string.IsNullOrWhiteSpace(method), "Invalid test identity.");
            Require(unique.Add(MethodEvidence.NormalizeIdentity(assembly, className, method)), duplicate);
        }
        Require(unique.Count > 0, empty);
        return unique;
    }

    private static RequiredTestIdentity[] ReadRequiredIdentities()
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, RequiredTestsFileName));
        try
        {
            return JsonSerializer.Deserialize<RequiredTestIdentity[]>(file, Json)
                ?? throw new InvalidDataException("Invalid required test inventory.");
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Invalid required test inventory.");
        }
    }

    private static HashSet<string> ReadCatalog()
    {
        var catalog = File.ReadLines(Path.Combine(AppContext.BaseDirectory, "scenario-catalog.md"))
            .Select(line => Regex.Match(line, @"^\| ((?:INT|H01|SAL|CAS|BRK|KFK|PDS)-[0-9]{2}) \|"))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToArray();
        Require(catalog.Length > 0 && catalog.Length == catalog.Distinct().Count(), "Invalid scenario catalog.");
        return catalog.ToHashSet(StringComparer.Ordinal);
    }

    internal static MethodEvidence[] CompiledMap()
    {
        var assembly = typeof(ScenarioEvidence).Assembly;
        var testMethods = assembly.GetTypes()
            .Where(type => type.IsDefined(typeof(TestClassAttribute), false))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(TestMethodAttribute), false))
            .ToArray();
        var required = ReadRequiredIdentities();
        Require(required.All(identity => identity is not null), "Invalid required test inventory.");
        ValidateRequiredIdentities(required.Select(identity => (identity.Assembly, identity.Class, identity.Method)),
            testMethods.Select(ReflectedIdentity));

        var methods = testMethods
            .Select(ReadMetadata)
            .OrderBy(method => method.Class, StringComparer.Ordinal).ThenBy(method => method.Method, StringComparer.Ordinal)
            .ToArray();
        Index(methods);
        return methods;
    }

    private static (string Assembly, string Class, string Method) ReflectedIdentity(MethodInfo method) =>
        (method.DeclaringType!.Assembly.GetName().Name!, method.DeclaringType.FullName!, method.Name);

    private static MethodEvidence ReadMetadata(MethodInfo method)
    {
        var categories = method.GetCustomAttributes<TestCategoryAttribute>().SelectMany(category => category.TestCategories).ToArray();
        Require(categories.Length == categories.Distinct().Count(), "Duplicate test category.");
        var evidence = categories.Where(value => value.StartsWith("Evidence:", StringComparison.Ordinal)).ToArray();
        Require(evidence.Length == 1, "Each method needs one evidence level.");
        string level = evidence[0]["Evidence:".Length..];
        var scenarios = categories.Where(Catalog.Value.Contains).ToArray();
        var featureCases = categories.Where(value => Regex.IsMatch(value, @"^A(?:0[1-9]|10)$")).ToArray();
        var requirements = categories.Where(value => Regex.IsMatch(value, @"^FR-0[1-9]$")).ToArray();
        Require(categories.Length == 1 + scenarios.Length + featureCases.Length + requirements.Length, "Unknown evidence category.");

        var metadata = method.GetCustomAttribute<EvidenceFixturesAttribute>();
        bool harness = level == "harness";
        Require(harness
            ? metadata is null && categories.Length == 1
            : (level is "contract" or "runtime") && metadata?.IntakePath is "SAL" or "all"
                && scenarios.Length + featureCases.Length > 0 && metadata.Fixtures.Length > 0,
            "Invalid method evidence metadata.");

        var fixtures = metadata?.Fixtures.Select(ReadFixture).ToArray() ?? [];
        Require(fixtures.Length == fixtures.Select(fixture => fixture.Path).Distinct().Count(), "Duplicate fixture path.");
        var identity = ReflectedIdentity(method);
        return new(identity.Assembly, identity.Class, identity.Method, metadata?.IntakePath, level,
            scenarios, featureCases, requirements, fixtures);
    }

    internal static FixtureReference ReadFixture(string reference)
    {
        string[] parts = reference.Split('#', 2);
        string path = parts[0];
        Require(Regex.IsMatch(path, @"^(?:intake/(?:restricted/)?|attendance/|registration/|wire/)[a-zA-Z0-9-]+\.json$"),
            "Invalid fixture path.");
        string file = Path.Combine(AppContext.BaseDirectory, "fixtures", path);
        Require(File.Exists(file), "Mapped fixture does not exist.");

        string[] ids = [];
        Require(parts.Length == 1 || !path.StartsWith("intake/restricted/", StringComparison.Ordinal),
            "Restricted fixtures can be referenced only by path.");
        if (!path.StartsWith("intake/restricted/", StringComparison.Ordinal))
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(file));
            var root = fixture.RootElement;
            if (parts.Length == 2)
            {
                ids = parts[1].Split(',');
                Require(root.ValueKind == JsonValueKind.Object && ids.All(id => root.TryGetProperty(id, out _)),
                    "Unknown fixture case reference.");
            }
            else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("cases", out var cases))
            {
                ids = cases.EnumerateArray().Select(row => row.GetProperty("caseId").GetString()!).ToArray();
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                ids = root.EnumerateArray().Select(row => row.GetProperty("operationId").GetString()!).ToArray();
            }
        }

        Require(ids.All(id => id is not null && Regex.IsMatch(id, @"^[A-Za-z0-9][A-Za-z0-9-]*$"))
            && ids.Length == ids.Distinct().Count(), "Invalid or duplicate fixture case ID.");
        return new(FixtureRoot + path, ids);
    }

    internal static async Task<int> RunAsync(EvidenceInvocation invocation, Func<SafeTestEvidence, string[], Task<int>> execute)
    {
        string runDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(invocation.Directory));
        Require(!Path.Exists(runDirectory), "Evidence requires a fresh run directory.");
        Directory.CreateDirectory(runDirectory);

        var receipt = new ExecutionReceipt(1, null, "pending", "pending", null, null, null, null, false, false);
        if (invocation.ContextPath is { } contextPath)
        {
            try
            {
                var context = ReadRunContext(contextPath, runDirectory);
                receipt = receipt with { RunId = context.RunId, SourceDigest = context.SourceDigest };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException
                or JsonException or ArgumentException or NotSupportedException)
            {
                return Complete(runDirectory, receipt with { EvidenceOutcome = "failed", FailureCode = "invalid-run-context" });
            }
        }

        var methods = CompiledMap();
        WriteNew(Path.Combine(runDirectory, MapFileName), methods);
        var collector = new SafeTestEvidence(methods);
        var started = DateTimeOffset.UtcNow;
        int exitCode;
        try
        {
            exitCode = await execute(collector, ["--results-directory", runDirectory]);
        }
        catch (Exception)
        {
            return Complete(runDirectory, receipt with
            {
                TestOutcome = "incomplete", EvidenceOutcome = "incomplete", FailureCode = "platform-execution-incomplete"
            });
        }

        receipt = receipt with { PlatformExitCode = exitCode, TestOutcome = exitCode == 0 ? "passed" : "failed" };
        return Complete(runDirectory, JoinObservedResults(runDirectory, methods, collector, started, receipt));
    }

    private static ExecutionReceipt JoinObservedResults(string runDirectory, MethodEvidence[] methods, SafeTestEvidence collector,
        DateTimeOffset started, ExecutionReceipt receipt)
    {
        XDocument trx;
        try
        {
            trx = collector.CreateReport(started, DateTimeOffset.UtcNow);
        }
        catch (InvalidDataException)
        {
            return receipt with { EvidenceOutcome = "failed", FailureCode = "invalid-test-event" };
        }

        try
        {
            var report = Join(methods, trx, started);
            var rows = report.BusinessEvidence.Concat(report.HarnessChecks).ToArray();
            bool incomplete = rows.Any(row => row.Status == "pending");
            bool failed = rows.Any(row => row.Status == "failed");
            if ((receipt.PlatformExitCode == 0 && failed) || (receipt.PlatformExitCode != 0 && !incomplete && !failed))
                return receipt with { EvidenceOutcome = "failed", FailureCode = "platform-result-mismatch" };

            if (rows.All(row => row.TrxOutcome is null))
                return receipt with { EvidenceOutcome = "incomplete", FailureCode = "missing-test-results" };

            using (var file = new FileStream(Path.Combine(runDirectory, TrxFileName), FileMode.CreateNew, FileAccess.Write))
                trx.Save(file);
            WriteNew(Path.Combine(runDirectory, ReportFileName), report);
            return receipt with
            {
                JoinedRunId = report.TestRunId,
                EvidenceOutcome = incomplete ? "incomplete" : "passed",
                FailureCode = incomplete ? "missing-test-results" : failed ? "platform-tests-failed" : null
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or XmlException)
        {
            return receipt with { EvidenceOutcome = "failed", FailureCode = "invalid-scenario-evidence" };
        }
    }

    private static RunContext ReadRunContext(string contextPath, string runDirectory)
    {
        string path = Path.GetFullPath(contextPath);
        Require(Path.GetFileName(path) == "run.json"
            && string.Equals(Path.Combine(Path.GetDirectoryName(path)!, "suite"), runDirectory,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Invalid run context.");
        using var file = File.OpenRead(path);
        using var context = JsonDocument.Parse(file);
        var root = context.RootElement;
        var version = ContextProperty(root, "schemaVersion");
        var run = ContextProperty(root, "runId");
        var source = ContextProperty(root, "source");
        var evidence = ContextProperty(root, "evidence");
        var digest = ContextProperty(source, "digest");
        var suite = ContextProperty(evidence, "suite");
        Require(version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out int schemaVersion) && schemaVersion == 1
            && run.ValueKind == JsonValueKind.String && run.TryGetGuid(out var runId) && runId != Guid.Empty
            && digest.ValueKind == JsonValueKind.String && Regex.IsMatch(digest.GetString()!, @"\A[0-9a-f]{64}\z")
            && suite.ValueKind == JsonValueKind.String && suite.GetString() == "suite", "Invalid run context.");
        return new(run.GetGuid(), digest.GetString()!);
    }

    private static JsonElement ContextProperty(JsonElement element, string name)
    {
        Require(element.ValueKind == JsonValueKind.Object, "Invalid run context.");
        var fields = element.EnumerateObject().Where(property => property.NameEquals(name)).Take(2).ToArray();
        Require(fields.Length == 1, "Invalid run context.");
        return fields[0].Value;
    }

    private static int Complete(string runDirectory, ExecutionReceipt receipt)
    {
        int platformExit = receipt.PlatformExitCode.GetValueOrDefault();
        try
        {
            WriteNew(Path.Combine(runDirectory, ReceiptFileName), receipt);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("invalid-execution-receipt");
            return platformExit != 0 ? platformExit : 1;
        }

        if (receipt.FailureCode is { } code)
            Console.Error.WriteLine(code);
        return platformExit != 0 ? platformExit : receipt.EvidenceOutcome == "passed" ? 0 : 1;
    }

    private static void WriteNew<T>(string path, T value)
    {
        using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(file, value, Json);
    }

    internal static EvidenceReport Join(MethodEvidence[] methods, XDocument document, DateTimeOffset runStarted)
    {
        var index = Index(methods);
        Require(document.Root?.Name == Trx + "TestRun", "Expected a TRX test run.");
        var run = document.Root!;
        var times = Element(run, "Times");
        Require(DateTimeOffset.TryParse((string?)times.Attribute("start"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            && DateTimeOffset.TryParse((string?)times.Attribute("finish"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var finish)
            && start >= runStarted && finish >= start, "TRX is stale or has invalid run timestamps.");
        var summary = Element(run, "ResultSummary");
        var counters = Element(summary, "Counters");
        var definitions = new Dictionary<Guid, MethodEvidence>();
        var definedMethods = new HashSet<MethodEvidence>();
        foreach (var definition in Element(run, "TestDefinitions").Elements())
        {
            Require(definition.Name == Trx + "UnitTest", "Unsupported TRX test definition.");
            var method = Element(definition, "TestMethod");
            string className = RequiredAttribute(method, "className").Split(',', 2)[0].Trim();
            string name = RequiredAttribute(method, "name");
            string prefix = className + ".";
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                name = name[prefix.Length..];
            string assembly = Path.GetFileNameWithoutExtension(RequiredAttribute(method, "codeBase").Replace('\\', '/')).ToLowerInvariant();
            Require(index.TryGetValue((assembly, className, name), out var mapped), "TRX test has no mapped method.");
            Require(definitions.TryAdd(ReadId(definition, "id"), mapped!) && definedMethods.Add(mapped!), "Duplicate TRX test definition.");
        }

        var results = new Dictionary<MethodEvidence, string>();
        foreach (var result in Element(run, "Results").Elements())
        {
            Require(result.Name == Trx + "UnitTestResult", "Unsupported TRX test result.");
            Require(definitions.TryGetValue(ReadId(result, "testId"), out var mapped), "TRX result has no test definition.");
            string outcome = RequiredAttribute(result, "outcome");
            Require(results.TryAdd(mapped!, outcome), "Duplicate TRX result for a method.");
        }

        ValidateCounters(counters, results.Values);
        var outcomes = methods.Select(method =>
        {
            string? outcome = results.GetValueOrDefault(method);
            return new MethodOutcome(method, outcome, Status(outcome));
        }).ToArray();
        return new(ReadId(run, "id"), outcomes.Where(row => row.Method.EvidenceLevel != "harness").ToArray(),
            outcomes.Where(row => row.Method.EvidenceLevel == "harness").ToArray());
    }

    private static void ValidateCounters(XElement counters, IReadOnlyCollection<string> outcomes)
    {
        const string mismatch = "TRX result counts do not match the summary.";
        var counts = outcomes.GroupBy(outcome => outcome, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        int Count(string outcome) => counts.GetValueOrDefault(outcome);

        Require(counters.Attribute("total") is not null, "Missing TRX total counter.");
        int explicitTimeouts = Count("Timeout");
        int failedIncludingTimeouts = Count("Failed") + explicitTimeouts;
        int timedOut = explicitTimeouts;
        if (counters.Attribute("timeout") is { } timeout)
        {
            Require(int.TryParse(timeout.Value, NumberStyles.None, CultureInfo.InvariantCulture, out timedOut)
                && timedOut >= explicitTimeouts && timedOut <= failedIncludingTimeouts, mismatch);
        }

        int failed = failedIncludingTimeouts - timedOut;
        int passed = Count("Passed");
        foreach (var counter in counters.Attributes())
        {
            int? expected = counter.Name.LocalName switch
            {
                "total" => outcomes.Count,
                "executed" => passed + failed,
                "passed" => passed,
                "failed" => failed,
                "timeout" => timedOut,
                "notExecuted" => Count("NotExecuted") + Count("Skipped"),
                "error" => Count("Error"),
                "aborted" => Count("Aborted"),
                "inconclusive" => Count("Inconclusive"),
                "passedButRunAborted" => Count("PassedButRunAborted"),
                "notRunnable" => Count("NotRunnable"),
                "pending" => Count("Pending"),
                "disconnected" or "warning" or "completed" or "inProgress" => 0,
                _ => null
            };
            Require(counter.Name.Namespace == XNamespace.None && expected is not null
                && int.TryParse(counter.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int actual)
                && actual == expected, mismatch);
        }
    }

    private static Dictionary<(string, string, string), MethodEvidence> Index(MethodEvidence[] methods)
    {
        var index = new Dictionary<(string, string, string), MethodEvidence>();
        foreach (var method in methods)
        {
            Require(method.ScenarioIds.All(Catalog.Value.Contains)
                && method.ScenarioIds.Length == method.ScenarioIds.Distinct().Count(), "Unknown or duplicate scenario ID.");
            Require(method.EvidenceLevel == "runtime" || !method.ScenarioIds.Contains("INT-06"), "INT-06 requires runtime evidence.");
            Require(index.TryAdd(method.Identity, method), "Duplicate scenario-map method.");
        }
        Require(index.Count > 0, "Empty scenario map.");
        return index;
    }

    private static string Status(string? outcome) => outcome switch
    {
        "Passed" => "passed",
        "Failed" or "Error" or "Timeout" or "Aborted" or "NotRunnable" or "PassedButRunAborted" => "failed",
        null or "Skipped" or "NotExecuted" or "Inconclusive" or "Pending" => "pending",
        _ => throw new InvalidDataException("Unknown TRX outcome.")
    };

    private static XElement Element(XElement parent, string name)
    {
        var elements = parent.Elements(Trx + name).ToArray();
        Require(elements.Length == 1, "Missing or duplicate required TRX element.");
        return elements[0];
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        string? value = (string?)element.Attribute(name);
        Require(!string.IsNullOrWhiteSpace(value), "Missing required TRX attribute.");
        return value!.Trim();
    }

    private static Guid ReadId(XElement element, string name)
    {
        Require(Guid.TryParse(RequiredAttribute(element, name), out var id) && id != Guid.Empty, "Invalid TRX ID.");
        return id;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
