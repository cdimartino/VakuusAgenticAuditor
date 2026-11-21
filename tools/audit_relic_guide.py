"""Check exact relic coverage and card selectors; optionally check the installed pack."""

import argparse
import csv
from pathlib import Path
from collections import Counter
from catalog import english_cards

ROOT = Path(__file__).resolve().parents[1]
CAT = ROOT / "mod/catalogs/26660232"
AUDIT = ROOT / "audit/26660232"
SELECTORS = set(
    "attack|skill|power|cheap-attack|multi-hit|expensive|zero-cost|exhaust|discard|draw|guard|poison|orbs|frost|lightning|focus|forge|stars|doom|summon|souls|shots|sadness|weak|vulnerable|shivs|strength|energy|area damage|Osty attacks|melody|hp loss|basic-strike|basic-defend".split(
        "|"
    )
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path)
    args = parser.parse_args()
    expected = {
        r["beta_id"]
        for r in csv.DictReader((AUDIT / "inventory.tsv").open(), delimiter="\t")
        if r["category"] == "relics"
    }
    cards = {
        r["id"]
        for r in csv.DictReader((AUDIT / "card-coverage.tsv").open(), delimiter="\t")
    }
    found = set()
    counts = Counter()
    linked = 0
    for name in ["a-g", "h-p", "q-z"]:
        for rid, kind, selectors, cue in csv.reader(
            (CAT / f"relic-guide-{name}.tsv").open(), delimiter="\t"
        ):
            if (
                rid in found
                or not name[0] <= rid[0].lower() <= name[-1]
                or kind not in {"tradeoff", "synergy", "utility", "conditional"}
                or not cue.strip()
            ):
                raise ValueError(f"Invalid or duplicate row: {rid}")
            if selectors != "-":
                if kind not in {"synergy", "tradeoff"} or not all(
                    s in SELECTORS or s.startswith("ID:") and s[3:] in cards
                    for s in selectors.split(",")
                ):
                    raise ValueError(f"Unverified selector on {rid}: {selectors}")
                linked += 1
            found.add(rid)
            counts[kind] += 1
    if expected != found:
        raise ValueError(f"Relic IDs differ from inventory: {sorted(found ^ expected)}")
    if args.pack:
        live = {
            k.split(".", 1)[0]
            for k in english_cards(args.pack, "relics")
            if k.endswith(".title")
        }
        if live != found:
            raise ValueError(
                f"Relic IDs differ from installed pack: {sorted(found ^ live)}"
            )
    print(
        f"{len(found)} relic IDs accounted for; {linked} with named-card matching; {dict(sorted(counts.items()))}"
    )


if __name__ == "__main__":
    main()
