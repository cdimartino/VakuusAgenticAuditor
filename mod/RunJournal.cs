using System.Diagnostics;
using System.Text.Json;
namespace Auditor;

// Deliberately typed allowlist: callers supply game facts, never saves, account IDs or assets.
internal record JournalCard(string Id, string Name, string? Reference, int Cost, bool Upgraded);
internal record JournalChoice(string Reference, string Name, bool Enabled, string? Description = null);
internal record JournalPower(string Id, int Amount);
internal record JournalEnemy(string Id, string Name, string? Reference, int Hp, int Block, JournalPower[] Powers, string[] Intents);
internal record JournalState(int Act, int Floor, string Room, string? Encounter, string? Combat, int Turn,
    string Phase, int Hp, int MaxHp, int Block, int Energy, int Gold, JournalCard[] Hand,
    JournalCard[] Deck, string[] Relics, string[] Potions, JournalPower[] Powers, JournalEnemy[] Enemies, int Stars = 0, int Arrows = 0, int DrawCount = 0, int DiscardCount = 0, int ExhaustCount = 0, string? Overlay = null, string? Selection = null, string? Character = null, int Ascension = 0, string? CatalogBuild = null, JournalChoice[]? Choices = null);
internal record JournalAdvice(string DecisionId, long StateSequence, string DeliveredText, bool Cursed,
    string[] ActionReferences, string? RecheckBoundary, int? ExpectedHpLoss = null);
internal record JournalAction(string Kind, string? CardReference, string? TargetReference, string Evidence);
// Prediction endpoints are explicit; a transition is NOT evidence of a card/potion action.
internal record JournalForecast(string DecisionId, long StartSequence, long EndSequence, int ExpectedHpLoss,
    bool Comparable, string? Limitation);
internal record JournalTransition(long FromSequence, long ToSequence, string Description);
internal record JournalEntry(int Schema, string RunId, string SessionId, long Sequence, DateTimeOffset Utc,
    long MonotonicTicks, long MonotonicFrequency, string Kind, JsonElement Data);

internal sealed class RunJournal
{
    internal const int SchemaVersion = 1;
    readonly object gate = new();
    readonly string directory;
    readonly long maxBytes;
    readonly int maxRuns;
    string? previousState;
    long sequence;
    internal string RunId { get; }
    internal string SessionId { get; } = Guid.NewGuid().ToString("N");
    internal string FilePath => Path.Combine(directory, RunId + ".jsonl");
    internal string? Failure { get; private set; }
    internal bool Truncated { get; private set; }

    // Persist RunId alongside the host run identity; seed alone is not unique. Reuse it on resume.
    internal RunJournal(string directory, string? resumeRunId = null, long maxBytes = 32 * 1024 * 1024, int maxRuns = 12)
    {
        this.directory = directory;
        this.maxBytes = Math.Max(1024, maxBytes);
        this.maxRuns = Math.Max(1, maxRuns);
        RunId = resumeRunId ?? Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(RunId, "N", out _)) throw new ArgumentException("Run identity must be a journal-generated GUID.", nameof(resumeRunId));
        try
        {
            Directory.CreateDirectory(directory);
            foreach (var entry in Read(FilePath)) sequence = Math.Max(sequence, entry.Sequence);
            foreach (var file in new DirectoryInfo(directory).GetFiles("*.jsonl")
                .Where(f => f.Name != RunId + ".jsonl" && Guid.TryParseExact(Path.GetFileNameWithoutExtension(f.Name), "N", out _))
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(this.maxRuns - 1)) file.Delete();
            Append("session", new { Resumed = resumeRunId != null && sequence > 0 });
        }
        catch (Exception e) when (DiskError(e)) { Failure = e.GetType().Name; }
    }

    internal long? State(JournalState state)
    {
        lock (gate)
        {
            var json = JsonSerializer.Serialize(state);
            if (json == previousState) return null;
            var result = Append("state", state);
            if (result != null) previousState = json;
            return result;
        }
    }
    internal long? Advice(JournalAdvice advice) => Append("advice", advice);
    internal long? ObservedAction(JournalAction action) => Append("observed-action", action);
    internal long? Transition(JournalTransition transition) => Append("transition", transition);
    internal long? Forecast(JournalForecast forecast) => Append("forecast", forecast);

    long? Append<T>(string kind, T data)
    {
        lock (gate)
        {
            if (Failure != null || Truncated) return null;
            try
            {
                var entry = new JournalEntry(SchemaVersion, RunId, SessionId, sequence + 1, DateTimeOffset.UtcNow,
                    Stopwatch.GetTimestamp(), Stopwatch.Frequency, kind, JsonSerializer.SerializeToElement(data));
                var line = JsonSerializer.Serialize(entry) + "\n";
                if ((File.Exists(FilePath) ? new FileInfo(FilePath).Length : 0) + System.Text.Encoding.UTF8.GetByteCount(line) > maxBytes)
                { Truncated = true; return null; }
                // A crash may leave a partial final line; separate it before resuming append-only writes.
                using var stream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (stream.Length > 0) { stream.Seek(-1, SeekOrigin.End); if (stream.ReadByte() != '\n') { stream.Seek(0, SeekOrigin.End); stream.WriteByte((byte)'\n'); } }
                stream.Seek(0, SeekOrigin.End);
                var bytes = System.Text.Encoding.UTF8.GetBytes(line);
                stream.Write(bytes);
                stream.Flush();
                return ++sequence;
            }
            catch (Exception e) when (DiskError(e)) { Failure = e.GetType().Name; return null; }
        }
    }

    static bool DiskError(Exception e) => e is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException;
    internal static JournalEntry[] Read(string path)
    {
        var entries = new List<JournalEntry>();
        try
        {
            if (!File.Exists(path)) return [];
            foreach (var line in File.ReadLines(path))
            {
                try { if (JsonSerializer.Deserialize<JournalEntry>(line) is { Schema: SchemaVersion } entry) entries.Add(entry); }
                catch (JsonException) { /* Interrupted/malformed records are not evidence. */ }
            }
        }
        catch (Exception e) when (DiskError(e)) { /* Analysis remains optional when storage is unavailable. */ }
        return entries.ToArray();
    }
}
