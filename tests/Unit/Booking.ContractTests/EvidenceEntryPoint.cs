using System.Text.Json;
using System.Xml;

namespace Booking.ContractTests;

internal static class EvidenceEntryPoint
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] != "--scenario-evidence")
            return await MicrosoftTestingPlatformApplication.RunAsync(args);

        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: --scenario-evidence <fresh-run-directory>");
            return 1;
        }

        try
        {
            return await ScenarioEvidence.RunAsync(args[1], MicrosoftTestingPlatformApplication.RunAsync);
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
}
