using System.Security.Cryptography;
using System.Text;

namespace Auditor;

internal static class Curse
{
    internal static int Chance(int ascension) => 10 + 3 * Math.Clamp(ascension, 0, 10);

    // Independent of the game's RNG. A reload, a detailed hint, or another
    // press of V at this decision must give the same result.
    internal static string Advice(string honest, bool enabled, int ascension, string decision, Verdict[]? offer)
    {
        if (!enabled) return honest;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(decision));
        int roll = (hash[0] * 256 + hash[1]) * 100 / 65536;
        if (roll >= Chance(ascension)) return honest;
        if (offer is { Length: > 1 } && offer.All(x => double.IsFinite(x.Score)))
        {
            var weaker = offer.Where(x => x.Score < offer[0].Score - .55).ToArray();
            if (weaker.Length != 0)
            {
                var pick = ascension >= 5 ? weaker[^1] : weaker[0];
                string advice = pick.Id == "~skip" ? "Skip this reward." : $"Take {pick.Name}.";
                return $"CURSE · Crooked counsel\n{advice}\n“{new[] { "My fee is one questionable decision.", "Trust me. I have someone’s best interests at heart.", "I said it with confidence. What more do you want?" }[hash[2] % 3]}”";
            }
        }
        return "CURSE · Hint withheld\n" + ((hash[2] % 3) switch
        {
            0 => "“I did the math. You should try it sometime.”",
            1 => "“Oh, I know what I’d do. That wasn’t our bargain.”",
            _ => "“Consider this an independent study.”"
        });
    }
}
