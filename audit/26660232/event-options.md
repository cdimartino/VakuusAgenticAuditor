# Event option scope — build 26660232

The owned English event table contains 842 localization entries across 68 roots; 66 event IDs are inventoried, and the other two roots are shared text. These are inventory counts, not counts of fully understood or playtested events. Option visibility, locks, actual quantities and legal targets depend on the current game state.

At runtime the evaluator uses the enabled choices on the current page. It compares recognized effects with current resources. An unrecognized clause stays attached to its option and limits the recommendation; it is not a zero-cost effect. Named Ancient relic and Courier choices have separate handlers.

Spirit Grafter is a regression case for combined effects: cap the displayed heal at missing HP, account for adding Metamorphosis, and compare against losing HP for one eligible upgrade. Metamorphosis is an Event Skill, not a curse; its random Attacks enter the draw pile, not the hand. Tests include a full-health player, lethal HP cost, missing upgrade targets, and unresolved or random outcomes.

To reproduce the inventory, count rows with category `events` in `inventory.tsv`. For localization, call `english_cards(Path(owned_pck), "events")` from `tools/catalog.py` and count keys and distinct prefixes before the first dot. No event callback or RNG needs to run.
