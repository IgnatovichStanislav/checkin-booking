using System.Text.Json;
using System.Xml;

namespace Booking.ContractTests;

internal sealed record EvidenceInvocation(string Directory, string? ContextPath);

internal static class EvidenceEntryPoint
{
    private const string EvidenceFlag = "--scenario-evidence";
    private const string ContextFlag = "--run-context";
    private const string Usage = "Usage: --scenario-evidence <fresh-run-directory> [--run-context <parent-run-json-path>]";

    public static async Task<int> Main(string[] args)
    {
        EvidenceInvocation? invocation;
        try
        {
            invocation = ParseArguments(args);
        }
        catch (InvalidDataException)
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        if (invocation is null)
            return await MicrosoftTestingPlatformApplication.RunAsync(args);

        try
        {
            return await ScenarioEvidence.RunAsync(invocation, RunPlatformQuietlyAsync);
        }
        catch (InvalidDataException error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or XmlException or JsonException or ArgumentException)
        {
            Console.Error.WriteLine("Could not produce or read scenario evidence.");
            return 1;
        }
    }

    internal static EvidenceInvocation? ParseArguments(string[] args)
    {
        if (!args.Contains(EvidenceFlag, StringComparer.Ordinal) && !args.Contains(ContextFlag, StringComparer.Ordinal))
            return null;

        return args switch
        {
            [EvidenceFlag, var directory] when IsValue(directory) => new(directory, null),
            [EvidenceFlag, var directory, ContextFlag, var context] when IsValue(directory) && IsValue(context) => new(directory, context),
            _ => throw new InvalidDataException(Usage)
        };
    }

    private static bool IsValue(string value) => !string.IsNullOrWhiteSpace(value) && !value.StartsWith('-');

    private static async Task<int> RunPlatformQuietlyAsync(SafeTestEvidence collector, string[] args)
    {
        TextWriter stdout = Console.Out;
        TextWriter stderr = Console.Error;
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        try
        {
            return await SafeEvidenceBuilderHook.RunAsync(collector, args);
        }
        finally
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
        }
    }
}
