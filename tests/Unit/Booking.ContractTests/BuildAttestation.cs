using System.Security.Cryptography;
using System.Text;

namespace Booking.ContractTests;

internal static class BuildAttestation
{
    internal static readonly string[] OwnedAssemblies =
    [
        "Booking.ContractTests.dll", "Booking.Application.dll", "Booking.Contracts.dll", "Booking.Domain.dll"
    ];

    internal static string ArtifactDigest(string directory)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var files = ArtifactFiles(directory)
            .Select(file => (Path: file, Relative: Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/')))
            .OrderBy(file => file.Relative, StringComparer.Ordinal);
        foreach (var file in files)
        {
            digest.AppendData(Encoding.UTF8.GetBytes(file.Relative + "\0"));
            digest.AppendData(FileDigest(file.Path));
        }
        return Convert.ToHexStringLower(digest.GetHashAndReset());
    }

    internal static string AssemblyDigest(string directory, string name) =>
        Convert.ToHexStringLower(FileDigest(Path.Combine(directory, name)));

    private static byte[] FileDigest(string path)
    {
        using var file = File.OpenRead(path);
        return SHA256.HashData(file);
    }

    private static IEnumerable<string> ArtifactFiles(string directory)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(path);
            if (attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Linked artifact path.");
            if (attributes.HasFlag(FileAttributes.Directory))
            {
                foreach (string file in ArtifactFiles(path))
                    yield return file;
            }
            else
            {
                yield return path;
            }
        }
    }
}
