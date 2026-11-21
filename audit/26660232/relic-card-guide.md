# Relics and card fit · build 26660232

The English catalog has 324 relic identities. A static inheritance scan found 316 concrete native relic models; the remaining eight titles are localization-only, not evidence they can drop. Sea Glass’s six character-specific display titles share the same relic identity. Run `python3 tools/audit_relic_guide.py --pack /path/to/game.pck` to check the catalog, shards, duplicates and exact card references.

The embedded `relic-guide-*.tsv` tables distinguish useful card matches from conditions, drawbacks, and utility. 117 relics have a card selector; the others still have a short explanation. Only a matching card in the actual deck/offer or planned line is named. Unknown or foreign models do not inherit native rules by ID. Matching indicates a deck-fit heuristic, not a full turn simulation or proof that a relic counter is ready. Detailed combat cues do not change the damage preview or the exported card sequence.

Examples: Snecko Skull supports a real Poison application. Helical Dart triggers on playing the Shiv, not merely generating it. Fiddle conflicts with mid-turn draw, not a draw scheduled for the next turn. Bronze Scales retaliates when hit; playing a Defend alone does not trigger it. Darv’s Lost Lute comparison checks the deck for an actual Melody source.
