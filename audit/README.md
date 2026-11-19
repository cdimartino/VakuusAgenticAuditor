# Compendium audit — STS2 public beta v0.113.1, Auditor 0.1.14

**Content identification is complete for the four published wiki lists. Mechanical coaching is partial.** There is no basis for certifying optimal play for every card, enemy, relic or event. The inventory distinguishes an item the mod can identify from an effect it can reason about.

| Category | Wiki entries matched in local beta | Evidence and limits |
|---|---:|---|
| Cards | 716 / 716 | All 745 local card IDs are in the shipped index, including placeholders. 498 wiki cards have at least one reward/mechanic tag; 218 are ID-only in that index. Runtime previews can supply additional damage/block data. Conversely, a tagged card need not have all its side effects simulated. |
| Relics | 315 / 315 | 17 explicit card-trigger profiles and 27 additional deck-fit profiles among wiki relics. 271 lack either profile. Some have other uses in the mod, e.g. the Ancient gift evaluator or a game-calculated preview. Those are not proof of a complete relic evaluation. |
| Potions | 67 / 67 | 66 active policies and one automatic revival policy. Recommendations also check the live belt, usability, resources, and hand. Not every timing condition has an individual behavioral test. Reward swaps still have substantially less coverage than combat use. |
| Events | 62 / 62 | Outcome parsing covers supported combinations of English clauses and selected Ancient gifts. Matching the event title does not mean the coach can compare every branch. |
| Characters | 6 playable | Ironclad, Silent, Regent, Necrobinder, Defect and Fool have a shared solo coach and per-character synergy scenarios. Colorless is a card pool. Other local character records include placeholders. |
| Bosses | 12 / 12 local `*_BOSS` encounters | Each has an encounter-specific tactical note; combat uses visible intents. These notes are not executable models of phase transitions, intent schedules or special damage caps. |
| Other local content | See inventory | Includes 127 named monster records, 326 named powers, enchantments, afflictions, orbs and acts. These rows are an inventory, **not** per-effect implementation claims. |

## Where to look

- [inventory.tsv](26660232/inventory.tsv): one row per named beta record with its ID, label, wiki match where applicable, and type of evidence. Mock, deprecated and locked entries are retained rather than silently counted as playable.
- [wiki.tsv](26660232/wiki.tsv): name, pool, topic metadata, source list and revision as retrieved November 19, 2026. Wiki tags can describe a requirement, a downside, or an effect; the coach does **not** treat every listed keyword as a benefit.
- [Wiki Compendium](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Main/Compendium): [cards](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Cards_List), [relics](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Relics_List), [potions](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Potions_List), [events](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Events_List). The [Bestiary](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Bestiary) is under construction. Boss coverage uses the installed encounter catalog and the individual wiki boss pages (e.g. [Kaiser Crab](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Kaiser_Crab), [Test Subject](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Test_Subject), [Vantom](https://slaythespire.wiki.gg/wiki/Slay_the_Spire_2:Vantom)).
- Wiki metadata attribution: Slay the Spire Wiki contributors, [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). No wiki images or game assemblies are included in the mod.

## Repeat the checks

From the project directory, set `PACK` to the installed `Slay the Spire 2.pck`.

```sh
python3 tools/audit.py "$PACK"
sh tools/package.sh
```

## Bringing a new main or beta build online

The **Watch Steam game builds** workflow compares Steam app 2868840’s `public` and `public-beta` branch build IDs every six hours using the SteamCMD metadata mirror. It opens a tracking issue per unknown branch/build. `python3 tools/watch_builds.py` checks without opening issues. Metadata detects releases; it does not contain audited card effects.

1. Use a locally owned installation of the requested branch. Confirm `buildid` in `steamapps/appmanifest_2868840.acf` and keep its `release_info.json` with the pack. Run `python3 tools/catalog.py "$PACK" BUILD_ID` to generate candidate tables in a **new** `dist/catalog-candidates/BUILD_ID/` folder.
2. Supply `audit/PREVIOUS_BUILD/card-text-hashes.json` as the optional third argument to the generator to emit a `card-changes.json` report of additions, removals and text changes. Save the new fingerprints with the new audit. Compare costs, base/upgraded values and triggers against the previous build and patch notes: an unchanged localization template does not establish unchanged numbers. Review generator `consumers`, bespoke `effects.tsv`, relic/boss/potion tables, and C# effect interpreter assumptions. Test changed mechanics against that branch’s assembly. Keep old profiles intact; if semantics differ beyond table data, implement and dispatch a new rules revision.
3. Stage reviewed tables under `mod/catalogs/BUILD_ID/` and wiki evidence under `audit/BUILD_ID/`. Register the exact version, commit and assembly hash from `release_info.json` in `mod/catalogs/index.json`. Run `python3 tools/audit.py "$PACK" --write`, inspect the resulting inventory diff, then rerun without `--write`. Add regression cases for the changed effects and run `sh tools/ci.sh`.
4. Smoke-test the target game build, update supported versions and changelog, then tag the mod release. Do not lower the minimum game version until runtime API compatibility is verified on that version.
