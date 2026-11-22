using System.Text.Json;

namespace Auditor;

internal sealed record CatalogProfile(string Build, string Version, string Commit, int AssemblyHash, string Rules)
{
    internal bool Exact { get; init; } = true;
    internal static Stream Open(string path) => typeof(CatalogProfile).Assembly
        .GetManifestResourceStream("auditor/catalogs/" + path) ?? throw new InvalidDataException($"Missing catalog: {path}");

    internal static CatalogProfile? Find(string? version, string? commit, int? hash, bool allowClosest = false)
    {
        using var file = Open("index.json");
        var profiles = JsonSerializer.Deserialize<CatalogProfile[]>(file, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return Select(profiles, version, commit, hash, allowClosest);
    }

    internal static CatalogProfile? Select(IEnumerable<CatalogProfile> profiles, string? version, string? commit, int? hash, bool allowClosest)
    {
        var compatible = profiles.Where(p => p.Rules == "1131").ToArray();
        var exact = compatible.SingleOrDefault(p => p.Version == version && p.Commit == commit && p.AssemblyHash == hash);
        if (exact != null || !allowClosest || !System.Version.TryParse(version?.TrimStart('v'), out var game)) return exact;
        // Compare numeric release components, not lexical strings or Steam build IDs.
        return compatible.Select(p => (profile: p, v: System.Version.TryParse(p.Version.TrimStart('v'), out var n) ? n : null))
            .Where(x => x.v?.Major == game.Major)
            .OrderBy(x => Math.Abs(x.v!.Minor - game.Minor))
            .ThenBy(x => Math.Abs((long)x.v!.Build - game.Build))
            .ThenBy(x => Math.Abs((long)x.v!.Revision - game.Revision))
            .ThenBy(x => x.v).ThenByDescending(x => long.Parse(x.profile.Build))
            .Select(x => x.profile with { Exact = false }).FirstOrDefault();
    }
}
