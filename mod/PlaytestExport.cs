using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Auditor;

// A local, opt-in observation channel. This class never sends game inputs.
internal sealed class PlaytestExport
{
    readonly string path;
    string fingerprint = "";
    long revision;
    object? delivered;
    internal string Revision => revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
    internal bool Changed { get; private set; }
    internal PlaytestExport(string path) => this.path = path;

    internal void Observe(object state)
    {
        var next = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state))));
        Changed = next != fingerprint;
        if (!Changed) return;
        fingerprint = next;
        revision++;
        delivered = null;
    }
    internal void Delivered(string text, object[] actions) => delivered = new
    {
        decisionId = $"{Revision}:{fingerprint[..12]}",
        text,
        postCurse = true,
        actions
    };
    internal void Write(bool enabled, int? profileId, string runId, string roomId, int turn, bool actionable, bool picker, object setup, object[] legalActions)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(path)) File.Delete(path);
                delivered = null;
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                enabled,
                generatedUtc = DateTimeOffset.UtcNow,
                stateRevision = Revision,
                profileId,
                runId,
                roomId,
                turn,
                delivered,
                readiness = new { actionable, picker },
                setup,
                legalActions
            }));
            File.Move(path + ".tmp", path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unavailable observation file cannot interrupt a player's run.
        }
    }
}
