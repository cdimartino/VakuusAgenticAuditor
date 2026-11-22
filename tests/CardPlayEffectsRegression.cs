using Auditor;

internal static class CardPlayEffectsRegression
{
    internal static void Run(Action<bool, string> check)
    {
        CardPlayEffects.Result Play(string id, string key = "", int amount = 0,
            int draw = 0, int discard = 0, int room = 3, int orbs = 0, bool unblocked = false) =>
            CardPlayEffects.Resolve(Catalog.Effect(id), k => k == key ? amount : 0, draw, discard, room, orbs, unblocked);
        check(Play("MIRAGE", "CalculatedBlock", 13).Guard == 13 && Play("MIRAGE", "Block", 13) == default,
            "Mirage consumes the live calculated preview only");
        check(Play("RALLY", "Block", 9).Guard == 9 && Play("LIFT", "Block", 9) == default,
            "all-player block includes self; ally-only block does not");
        check(Play("MASTER_OF_STRATEGY", "Cards", 4, draw: 1).Fit == 1 &&
            Play("HUDDLE_UP", "Cards", 4, draw: 6, room: 2).Fit == 2 &&
            Play("HUDDLE_UP", "Cards", 4) == default, "draw is capped by legal supply and hand space");
        check(Play("SCRAWL", draw: 5, room: 2).DrawBoundary &&
            Play("SCRAWL", draw: 5, room: 0) == default, "fill-hand draw requires space");
        check(Play("COMPILE_DRIVER", "Cards", 3, draw: 3) == default &&
            Play("COMPILE_DRIVER", "CalculatedCards", 2, draw: 3).Fit == 2, "calculated draw is not a flat Cards field");
        check(Play("ACROBATICS", "Cards", 3, draw: 4).Fit < Play("SKIM", "Cards", 3, draw: 4).Fit,
            "discard selection does not count all draws as net additions");
        check(Play("DEEP_STRETCH", "StretchPower", 2) is { Boundary: true, DrawBoundary: false },
            "an empty draw pile does not erase the self-buff or predict a card");
        check(Play("DISCOVERY") is { Boundary: true, DrawBoundary: false } &&
            Play("DISCOVERY", room: 0) == default, "generation is not a draw and needs board space");
        check(Play("DREDGE", "Cards", 2, draw: 4) == default &&
            Play("DREDGE", "Cards", 2, discard: 1).Fit == 1, "recover only existing discards");
        check(Play("PURITY", "Cards", 3, room: 10) == default &&
            Play("PURITY", "Cards", 3, room: 9).Boundary, "optional exhaust needs another card");
        check(Play("QUADCAST", "Repeat", 4) == default && Play("QUADCAST", "Repeat", 4, orbs: 1).Boundary &&
            Play("ZAP") is { Guard: 0, Boundary: true }, "evoke requires an orb; channel does not predict an evoke target or passive");
        check(Play("DEADLY_POISON", "PoisonPower", 5) == default &&
            Play("DEADLY_POISON", "PoisonPower", 5, unblocked: true) is { Boundary: true, Guard: 0 },
            "Poison is option value only and cannot assume away Artifact");
        check(Play("HIDDEN_CACHE", "StarNextTurnPower", 5) == default &&
            Play("HIDDEN_CACHE", "Stars", 2).Boundary && Play("KNOCKOUT_BLOW", "Stars", 2) == default,
            "do not credit next-turn or on-kill Stars");
        check(Play("CALTROPS", "ThornsPower", 4) is { Guard: 0, Boundary: true } &&
            Play("PILLAR_OF_CREATION", "Block", 4) == default &&
            Play("OUTMANEUVER", "Energy", 3) == default,
            "installing a trigger does not grant block, energy or retaliation damage now");
        check(Play("NEW_MOD_CARD", "Cards", 5, draw: 5) == default &&
            !CardPlayEffects.OwnsDraw(Catalog.Effect("PILLAGE")), "foreign IDs and legacy random-draw hooks do not inherit profiles");
        var generator = Catalog.Get("ABUNDANCE", "Abundance", 1, false, false);
        var pillar = Catalog.Get("PILLAR_OF_CREATION", "Pillar", 1, false, false);
        var empty = new Deck([], 50, 50, 1, false, []);
        check(Coach.Assess(generator, empty with { Cards = [pillar] }).Score > Coach.Assess(generator, empty).Score
            && !pillar.CombatHas("guard") && !Catalog.Get("DREDGE", "", 1, false, false).Has("card creation"),
            "Pillar values actual card generation; recovering a card is not generation or immediate block");
        using var reader = new StreamReader(CatalogProfile.Open($"{Catalog.Profile.Build}/effects.tsv"));
        foreach (var line in reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.TrimEnd('\r').Split('\t');
            if (fields[1] is "requires-impale" or "sadness-one" or "sadness-cards" or "convert-sadness"
                or "repeat-attacks" or "repeat-skills" or "block-damage" or "hand-upgrade" or "exhaust-choice" or "uncertain-draw") continue;
            var value = CardPlayEffects.Resolve(fields[1], _ => 2, 3, 2, 3, 1, true);
            check(Catalog.Get(fields[0], "", 1, false, false).CombatKnown && (value.Guard > 0 || value.Boundary && value.Fit > 0),
                $"{fields[0]}: native ID dispatches to a nonempty action on a supported board");
        }
    }
}
