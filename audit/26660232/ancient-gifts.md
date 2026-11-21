# Ancient gift advice — build 26660232

Read-only verification used the locally owned English PCK and `sts2.dll` (arm64). No event callbacks, RNG, saves or active-run writes were used. This document contains original observations, not extracted source or localization.

## Measured scope

`AncientOffer.Assess` covers all **12 distinct Darv relic options** found in the installed model: ASTROLABE, BLACK_STAR, CALLING_BELL, EMPTY_CAGE, PANDORAS_BOX, RUNIC_PYRAMID, SNECKO_EYE, ECTOPLASM, SOZU, PHILOSOPHERS_STONE, VELVET_CHOKER, DUSTY_TOME. This is bounded advice coverage, not complete outcome valuation or all-Ancient coverage. The pictured three have a dedicated concise comparison. Other Ancient option sets are left to their existing handlers.

Verified magnitudes: Cage removes 2; Astrolabe transforms 3 and upgrades results; Black Star adds one relic reward per elite; Bell adds Curse of the Bell and 3 random relics; Pandora transforms removable basic Strikes/Defends; Pyramid prevents hand flushing; Snecko adds 2 draw and Confused; the four energy relics add 1 maximum energy. Ectoplasm blocks gold gains, Sozu potion acquisition, Stone gives enemies 1 Strength, Choker prevents plays after 6 started card plays.

Darv offers from eleven singleton relic groups, with Dusty Tome optionally replacing one of the three slots. Ectoplasm/Sozu require zero-based act index 1, Stone/Choker index at least 1. Pandora is excluded by deck-clearing modifiers. Runtime enabled options, not these static pools, define the actual comparison.

## Read-only integration contract

`AncientOffer.Compare(IEnumerable<string> ids, Deck deck, bool explicitPick, AncientOffer.Context? context = null)` returns nullable display advice.

`AncientOffer.Assess(string id, Deck deck, AncientOffer.Context? context = null)` returns nullable `Evaluation(Id, Score, Reason, Limited)` for detailed diagnostics. Scores are fit heuristics, not win probabilities; NaN denotes unmodeled value, never zero.

`Context(int? Removable, int? Transformable, int? Upgradeable, Card? TomeCard, Card[]? RemovalTargets, Card[]? TransformTargets)` has optional null defaults. Supply current legal target arrays/counts, not a guessed subset of attractive targets. Upgradeable is reserved for callers sharing their live eligibility snapshot; these gifts do not upgrade existing deck cards.

Runtime predicates from `CardSelectCmd`: removal uses `c.IsRemovable`; transform uses `c.Type != CardType.Quest && c.IsTransformable`; upgrade uses `c.IsUpgradable`, over `PileType.Deck.GetPile(player).Cards`. Cage/Astrolabe request exact counts (min=max), but automatically take all eligible cards when the pool is smaller. They are not impossible when fewer than 2/3 cards remain, nor freely optional “up to” selections.

Read `DustyTome.AncientCard` (public nullable ModelId) from the actual offered relic. Do **not** call `SetupForPlayer`: that consumes rewards RNG. Its candidate pool is unlocked character Ancient cards excluding Archaic Tooth's transcendence cards. Resolve the offered card through the runtime adapter, using a detached real upgrade preview if necessary. Never invent upgraded costs/stats or mutate the canonical model/live deck.

Lost Lute is a 3-cost Ancient Power; its real upgrade reduces cost by 1. Its trigger plays the top draw-pile card whenever Melody is gained. The identity and trigger are verified, but Melody frequency/autoplay payoff are not modeled, so it never receives a fabricated numeric score.

## Limits and regression checks

Random relic/card outcomes, exact potion/gold opportunity costs, future elite routes, hand-clog simulation, Melody frequency, autoplay order, and individual upgrade deltas are not predicted. Unknown relics return null; partially recognized offers provide a supported observation without a universal winner. Removal/transform basic-card heuristics do not declare specific important nonbasic cards expendable.

Focused regression entry: `AncientOfferRegression.Run(Action<bool,string>)`. Wire `../mod/AncientOffer.cs` into Scenarios.csproj and call that entry from the scenario runner. Fourteen focused assertions passed using an isolated temporary runner: concise trio, order invariance, explicit-pick preference, legal short pools, zero targets, random/unknown value handling, partial offers, deck-sensitive removal/Snecko fit, all 12 IDs, and upgrade-name hygiene.
