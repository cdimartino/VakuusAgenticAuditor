namespace Auditor;

// Dispatched only from the selected build's exact native model hook. An empty
// result means this component has no evidence of value on the supplied board.
internal static class CardPlayEffects
{
    internal readonly record struct Result(int Guard = 0, bool Boundary = false,
        bool DrawBoundary = false, double Fit = 0, string? Reason = null);

    internal static bool OwnsDraw(string? effect) => effect is
        "draw-cards" or "draw-calculated" or "draw-fill" or "draw-discard" or "draw-stretch";

    // handRoom is space AFTER playing this card. drawAvailable includes the
    // reshufflable discard, or zero when drawing is blocked. Unknown vars = 0.
    internal static Result Resolve(string? effect, Func<string, int> preview,
        int drawAvailable, int discardAvailable, int handRoom, int orbCount, bool debuffUnblocked = false)
    {
        int room = Math.Clamp(handRoom, 0, 10);
        int Get(string key) => Math.Max(0, preview(key));
        static Result Option(int units, string reason, double weight = 1) => units > 0
            ? new(Boundary: true, Fit: Math.Min(4, units * weight), Reason: reason) : default;

        if (OwnsDraw(effect))
        {
            int requested = effect switch { "draw-fill" => room, "draw-calculated" => Get("CalculatedCards"), _ => Get("Cards") };
            int drawn = Math.Min(room, Math.Min(Math.Max(0, drawAvailable), requested));
            int stretch = effect == "draw-stretch" ? Get("StretchPower") : 0;
            if (drawn == 0)
            {
                if (effect == "draw-discard" && room < 10)
                    return new(Boundary: true, Reason: "A discard can still change the hand even when no card is drawn.");
                return Option(stretch, "Recheck the next block preview after gaining Stretch.");
            }
            // Drawing then discarding is selection, not a net increase in hand size.
            return new(Boundary: true, DrawBoundary: true, Fit: Math.Min(4, drawn * (effect == "draw-discard" ? .5 : 1d)),
                Reason: effect == "draw-discard" ? "Recheck the hand after drawing and discarding." : "Recheck the cards actually drawn before continuing.");
        }

        return effect switch
        {
            "flat-guard" => new(Guard: Get("Block")),
            "calculated-guard" => new(Guard: Get("CalculatedBlock")),
            "create-one" or "create-cards" or "create-fill" =>
                Option(Math.Min(room, effect == "create-one" ? 1 : effect == "create-fill" ? room : Get("Cards")),
                    "Recheck the new hand before choosing another card."),
            "recover-cards" => Option(Math.Min(room, Math.Min(discardAvailable, Get("Cards"))), "Recheck which cards returned from discard."),
            "exhaust-up-to" => Option(Math.Min(10 - room, Get("Cards")), "Choose whether to exhaust; recheck the remaining hand.", .5),
            "channel-one" or "channel-two" or "channel-three" or "channel-repeat" =>
                Option(effect switch { "channel-one" => 1, "channel-two" => 2, "channel-three" => 3, _ => Get("Repeat") },
                    "Recheck the orb queue after channeling; a full queue can evoke."),
            "evoke-twice" or "evoke-repeat" when orbCount > 0 => Option(effect == "evoke-twice" ? 2 : Get("Repeat"),
                "Recheck after the rightmost orb resolves; its outcome depends on the queue."),
            "gain-stars" => Option(Get("Stars"), "Recheck which cards the additional Stars can pay for.", .5),
            "gain-arrows" => Option(Get("Repeat"), "Recheck the available Arrows before shooting.", .5),
            "gain-energy" => Option(Get("Energy"), "Recheck the playable hand after gaining energy."),
            "vigor" => Option(Get("VigorPower"), "Recheck the next attack preview after gaining Vigor.", .5),
            "stretch" => Option(Get("StretchPower"), "Recheck the next block preview after gaining Stretch.", .5),
            "focus" => Option(Get("FocusPower"), "Recheck the orb previews after gaining Focus.", orbCount > 0 ? 1 : .25),
            "intangible" => Option(Get("IntangiblePower"), "Recheck incoming hits after gaining Intangible; prevention is not projected."),
            "thorns" => Option(Get("ThornsPower"), "Thorns matters when attacked; no retaliation damage is projected.", .5),
            "poison" when debuffUnblocked => Option(Get("PoisonPower"), "Recheck Poison stacks; a later tick is not current damage.", .5),
            _ => default
        };
    }
}
