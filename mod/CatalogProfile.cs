using System.Text.Json;

namespace Auditor;

internal sealed record CatalogProfile(string Build, string Version, string Commit, int AssemblyHash, string Rules)
{
    internal static Stream Open(string path) => typeof(CatalogProfile).Assembly
        .GetManifestResourceStream("auditor/catalogs/" + path) ?? throw new InvalidDataException($"Missing catalog: {path}");

    internal static CatalogProfile? Find(string? version, string? commit, int? hash)
    {
        using var file = Open("index.json");
        var profiles = JsonSerializer.Deserialize<CatalogProfile[]>(file, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        // The hand-coded combat/effect interpreters in this binary implement
        // rules revision 1131. A data directory alone cannot opt a build in.
        return profiles.SingleOrDefault(p => p.Version == version && p.Commit == commit && p.AssemblyHash == hash && p.Rules == "1131");
    }
}
