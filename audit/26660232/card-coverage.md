# Card accounting · Steam build 26660232

**746 distinct stable IDs, not 746 fully modeled cards.** The installed public-beta manifest, pack release metadata and native arm64 assembly agree on v0.113.1, commit `d09d48e5`, main assembly hash `-1414792094`. The assembly SHA-256 is `a671e9efb86a7b80405133105665867b053c787670d475b4347cf5ccc15cb5fb`. Its REA metadata inspection (`ev_f92b77b3b0b1af2f1eb97e95fc7a89808cef17e073a8f2c11e48d4edd73e58ad`) and the independent managed inheritance scan below are static, not playtests.

| Source | Count | Cross-check |
|---|---:|---|
| Native English pack | 745 | All have description records (including empty templates) and fingerprints; legacy `roles.tsv` matches all IDs, with 512 nonempty role/payoff rows and 233 empty ones. |
| Concrete managed card types | 724 | 723 match pack IDs; `MOCK_TURN_END_IN_HAND_RECORDER_CARD` is a model-only test helper. The 22 localization-only IDs do not establish current playable models. Mock and legacy rows stay visible. |
| Live Card Library, read-only comparison | 691 | All 691 discovered IDs match both the native pack and concrete models. No unknown discovered IDs; 54 pack IDs were not discovered. The compendium is a subset, not the release denominator. |
| Installed additional card sources | 0 observed | Across the native assembly and 25 additional managed DLLs in the local mod and Workshop folders, only the native assembly supplied concrete card subclasses in this scan. Five mod PCKs and the Workshop resource ZIP contained no `*/cards.json` catalogs. Fool is native in this build. Dynamically registered, nested, generic or externally supplied types in other installations are not ruled out. |

## What the coach actually uses

[card-coverage.tsv](card-coverage.tsv) is the build-scoped embedded lookup. Its indexed columns preserve the original `roles.tsv` attribution. `base_immediate` and `upgraded_immediate` are effective role evidence from the respective template branches plus reviewed overrides. `deferred_gives` is available to deck-fit analysis, never to current-play preview extraction. `needs` records payoffs, not guaranteed results. `effect` names the existing `effects.tsv` combat hook, if any. The two reward columns are policies:

- **539 role-fit, partial** (both base and upgraded): rank marginal deck fit using the recorded evidence. For example, 16 verified hand-generation profiles can support Pillar of Creation; returning an existing card from discard does not qualify. Neither a role nor a finite score means every condition, side effect, numeric change or alternative is modeled.
- **3 bespoke-reward, partial:** Undeath, Transfigure and Oblivion use explicit choice valuation. This classification grants no combat roles.
- **18 typed-curse, partial:** the runtime must confirm the native model is a Curse. Use marginal dilution, plus a specific liability only where `CurseValuation` implements one. Merely matching an ID is insufficient.
- **12 typed-status, partial:** native Status identity and exact release required; [the 20-model audit](status-cards.md) distinguishes actual deck liabilities from encounter choices, legacy and mock models.
- **174 abstain:** 151 model+text, 22 text-only and one model-only. Cost, deck size, upgrades, duplicates and Skip do not turn an unknown effect into a score. Foreign models cannot inherit native roles or liabilities.

Combat eligibility is separate. **550 IDs** pass the catalog's base play identity gate; **196** do not. Observing a status in hand is separate from proposing to play it. The following disjoint counts describe *why an ID can enter*, not a count of fully simulated cards:

| Base combat evidence | IDs | Meaning |
|---|---:|---|
| New exact action profile | 76 | Explicit input, action or boundary in `CardPlayEffects`; seven of these still abstain on rewards. |
| Pre-existing bespoke hook | 11 | Includes targeting requirements, variable hit counts and choice boundaries. |
| Setup-only exception | 2 | Buffer and Barricade can prompt a recheck; future prevention or retained block is not projected. |
| Immediate role candidate, no catalog hook | 397 | Requires native target legality and usable previews or other adapter evidence. |
| Deferred/payoff candidate only, no catalog hook | 64 | No direct amount follows from these roles; an independent adapter rule or setup boundary is needed. |
| Ineligible | 196 | No play route by catalog identity. Includes non-playable records as well as unresolved playable effects. |

The **76 new profiles** break down into 21 draw/draw-discard, 19 hand creation/recovery/optional exhaust, 14 orb channel/evoke, 12 status, 8 resource, and 2 block previews. Before this change some already supplied attack, block or a generic pivot; the new capability is the specific effect or its board-sensitive valuation. Of the 76, only the two block hooks return current mitigation directly. Others express *bounded option value* and stop for a fresh observation. For example, Mirage reads `CalculatedBlock`, not `Block`; Quadcast requires an occupied orb and reads `Repeat`; Scrawl requires drawable cards and hand space; later Poison ticks contribute no current damage. A random card's arrival can be known while its identity, cost and eventual play remain unknown. Generic ally-only, delayed, on-kill and X-cost effects do not inherit these profiles. Reward-only evidence includes Outmaneuver’s next-turn energy and Envenom’s later conditional Poison; neither enters the immediate role column.

Static cross-check: REA identified the installed assembly (`ev_290d27a1738c0066781ee46939da72faedbc93476db73d296498d5307e6456c3`). Across the 746-ID union we compared pack fingerprints and concrete type membership. For the 76 selected IDs we also inspected their native play methods, including compiler-generated async bodies. All 53 variable-reading profiles have the corresponding accessor or named lookup in the play path; literal counts and fill/choice behavior were checked against the matching templates and methods. Profiles deliberately omit unmeasured riders. This is not an execution trace or proof of all relic, immunity, multiplayer, and draw-order interactions.

The ledger flags 36 explicit upgrade templates, 248 templates containing selected timing/condition phrases, and the random-rider Mad Science template. Flags overlap and are **review signals**, not an exhaustive inventory of code-level conditions. Numeric-only upgrades need runtime previews. Spinner distinguishes recurring orb supply from the upgraded immediate channel. Whistling Arrows no longer consumes an arrow or counts as a shot enabler. Well-Laid Plans no longer counts as discard; Mad Science no longer supplies all possible riders. The 18 verified `CanonicalTags` Melody sources support Lost Lute’s payoff. Hand previews for Stack can use calculated block rather than assuming a flat value.

## Repeat the checks

Without the game or profile: `python3 tools/audit_card_coverage.py`. This compares exact ID sets, source labels, original role columns, effect references and reward policies; duplicate, missing and extra IDs fail. With the matching installation, add `--pack "$PACK"` to recompute all effective roles/branches and verify every localization fingerprint. `--pack "$PACK" --write` regenerates the ledger. Test behavior with `dotnet run --project tests/Scenarios.csproj`.

For an independent assembly comparison, use a temporary workspace and the locally installed .NET 9 SDK. On the audited Mac:

```sh
work=$(mktemp -d)
dotnet new console --framework net9.0 --output "$work/model-scan"
cp audit/26660232/card-coverage-models.cs "$work/model-scan/Program.cs"
dotnet build "$work/model-scan" --verbosity quiet
steam="$HOME/Library/Application Support/Steam/steamapps"
app="$steam/common/Slay the Spire 2/SlayTheSpire2.app/Contents"
native="$app/Resources/data_sts2_macos_arm64/sts2.dll"
find "$app/MacOS/mods" "$steam/workshop/content/2868840" -name '*.dll' -print0 |
  xargs -0 dotnet "$work/model-scan/bin/Debug/net9.0/model-scan.dll" "$native" > "$work/models.tsv"
python3 tools/audit_card_coverage.py --pack "$app/Resources/Slay the Spire 2.pck" --assembly "$native" --models "$work/models.tsv"
```

For a future active profile, read only `get_compendium().sections.card_library.discovered_ids` and compare its unique ID set to the ledger **in memory**. Report only counts and unmatched stable card IDs. Do not save a compendium response, account identifier, progress statistics or raw card descriptions.
