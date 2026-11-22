# Native Status cards · Steam 26660232

**20 concrete Status types among 724 native card models.** Nineteen pass `CardType.Status` to `CardModel` and one overrides `Type` with the constant. Seven mock subclasses override the type getter; only `MockStatusCard` returns Status. `MadScience`, the one nonconstant override, uses `TinkerTimeType`; the implemented variants are Attack, Skill and Power. Counts describe shipped types, not the card library or cards players can receive as rewards.

The [decision table](../../mod/catalogs/26660232/status-cards.tsv) includes the exact English title verified for every ID. Static evidence: v0.113.1/d09d48e5 arm64 DLL SHA-256 `a671e9efb86a7b80405133105665867b053c787670d475b4347cf5ccc15cb5fb`, MVID `1a667627-d2cf-4b1d-ade2-39da43f6f1f0`, REA identity `ev_290d27a1738c0066781ee46939da72faedbc93476db73d296498d5307e6456c3`. We checked the English pack against known fingerprints, including deliberately empty keyword-only templates. Costs and amounts below are native base values, not promises about a particular instance.

| IDs | Native hook and decision |
|---|---|
| `BURN`, `INFECTION`, `WITHER` | Unplayable. End in hand: 2, 3, 3 blockable damage respectively. Wither’s fake upgrades add 3 to that copy. |
| `BECKON`, `TOXIC` | Normally playable for 1. If still in hand at turn end: Beckon loses 6 HP ignoring Block; Toxic deals 5 blockable damage. Toxic exhausts on play; Beckon has no inherent Exhaust. |
| `DAZED`, `VOID` | Both Ethereal and Unplayable. Void loses 1 energy on drawing that instance, not for holding it or merely generating it in hand. |
| `SADNESS`, `SOOT`, `WOUND` | Unplayable. **Sadness is a Status, not a separate type**; Retain keeps it in hand. Soot and Wound have no intrinsic play action. |
| `DEBRIS`, `SLIMED` | Both cost 1 and Exhaust. Debris clears itself; Slimed also draws one. |
| `FRANTIC_ESCAPE` | Play for 1 base energy to increase targeted Sandpit distance by one if present; each play raises its cost this combat. Context-dependent, no generic deck ranking. |
| `AMNESIA`, `DISINTEGRATION`, `MIND_ROT`, `SLOTH`, `WASTE_AWAY` | Knowledge Demon `IChoosable.OnChosen` applies a corresponding **power**, not an on-draw or hand penalty. Respectively: this turn draw 2 fewer/lose 1 energy; end-turn blockable damage (6); draw 1 fewer per turn; cap at 3 plays per turn; reduce maximum energy by 1. Ignore card identity alone for active-power forecasts. |
| `DEPRECATED_CARD`, `MOCK_STATUS_CARD` | The hidden legacy replacement draws one, Exhausts, and removes its deck copy if present. Mock Status Card is an Ethereal test fixture. Neither is a ranked reward. |

**Generator and support decisions.** `JEST` is a Skill: 14/18 Block and one generated Sadness; `SORROWFUL_STRIKE` is an Attack: 13/17 damage and two generated Sadness. `MOURNING_BLOW` is a zero-cost 10/15 Attack: after the last play in its series, it transforms into Sadness and changes a normal discard destination to hand. `CardModel` calls `CardCmd.Transform`; a successful combat-pile replacement records `CardGenerated` and calls `AfterCardGeneratedForCombat`, so this transform can trigger All Is Well and count for Anguish. The advisor stops that line to reobserve. `PUNCH_OF_JOY` can transform one random Sadness already in hand; `ECSTASY` transforms at turn start. **Joy is a zero-cost token Skill**, not a Status. The `ALL_IS_WELL` Power rewards generated Sadness; `GROWTH` exhausts Sadness, `COMPACT` transforms held Statuses and `FLAK_CANNON` exhausts Statuses. Naming a payoff does not add a second synergy weight. `LOCKED_IN` is a Skill retrieving a Shoot-tag card from the draw pile, not a Status, generator or guaranteed shot.

**Runtime policy.** Twelve ordinary statuses carry bounded draw/hand liabilities if actually offered. The other eight abstain from general reward scoring. Combat checks the entire observed hand including currently unplayable cards. A held end-trigger, unknown Status, active Disintegration or active Sloth suspends quantitative plans rather than claiming a safe kill or reliable HP outcome. Drawn Void does not charge energy twice; future Sadness is neither damage nor HP loss. The table does not simulate discard choices, draw order, all powers/relics, future status insertion, or encounter choice utility.

## Reproduce offline

1. Follow [the model-inventory recipe](card-coverage.md#repeat-the-checks) to set `native`, `app`, `work` and produce `$work/models.tsv`. Run `python3 tools/audit_card_coverage.py --assembly "$native" --pack "$app/Resources/Slay the Spire 2.pck" --models "$work/models.tsv"`; this also compares all 20 guide titles against the pack.
2. Use the existing disposable `card-play-effects-26660232/Scan.csproj` (net9, Mono.Cecil from ILSpy 9.1), copying it into a fresh directory to avoid touching that scanner. From repo root:

   ```sh
   mkdir "$work/status"
   cp "$TMPDIR/opencode/card-play-effects-26660232/Scan.csproj" "$work/status/"
   cp audit/26660232/status-models.cs "$work/status/Program.cs"
   dotnet run --project "$work/status" -- "$native" "$work/models.tsv" \
       mod/catalogs/26660232/status-cards.tsv > "$work/status-models.tsv"
   ```

   On another machine replace `Scan.csproj`’s `Mono.Cecil` hint path with the one from your ILSpy installation. The checker resolves the enum from metadata, checks each constructor or override, and fails if the resulting ID set differs from the decision table.
3. For each full name emitted, run `ilspycmd -t "$name" "$native"` and inspect `CanonicalKeywords`, `CanonicalVars`, `OnPlay`, `OnTurnEndInHand`, `AfterCardDrawn`, `OnChosen` and the corresponding power methods. Check `MegaCrit.Sts2.Core.Models.Cards.{MadScience,Jest,MourningBlow,SorrowfulStrike,PunchOfJoy,Joy,LockedIn}` individually for the non-Status distinctions above. `dotnet run --project tests/Scenarios.csproj` exercises the pure advisor. No game execution or save observation is needed for these checks.
