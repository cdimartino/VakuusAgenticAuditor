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

- **496 role-fit, partial** (both base and upgraded): rank marginal deck fit using the recorded evidence. Neither a role nor a finite score means every condition, side effect, numeric change or alternative is modeled.
- **18 typed-curse, partial:** the runtime must confirm the native model is a Curse. Use marginal dilution, plus a specific liability only where `CurseValuation` implements one. Merely matching an ID is insufficient.
- **232 abstain:** no defensible reward fit. Cost, deck size, upgrades, duplicates and the presence of a Skip option do not turn an unknown effect into a score. Foreign models, even with a colliding native ID, cannot inherit native roles or liabilities.

Combat eligibility is separate: **498 native IDs** may enter the evaluator (496 role-bearing cards and two setup-only powers, Buffer and Barricade). All 11 bespoke combat hooks are eligible independently of reward policy. Eligibility is not a playable line: the adapter still needs a legal play and usable preview, effect or setup boundary. Setup-only powers allow a reevaluation after play; the planner does not simulate future prevention or retained block. The other 248 IDs do not enter combat search by identity.

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
python3 tools/audit_card_coverage.py --pack "$app/Resources/Slay the Spire 2.pck" --models "$work/models.tsv"
```

For a future active profile, read only `get_compendium().sections.card_library.discovered_ids` and compare its unique ID set to the ledger **in memory**. Report only counts and unmatched stable card IDs. Do not save a compendium response, account identifier, progress statistics or raw card descriptions.
