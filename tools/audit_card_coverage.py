"""Verify the card ledger offline, or reproduce its classifications from the installed pack.

No game or profile text is written. --write requires the exact release and pack fingerprints.
"""

import argparse
import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

from catalog import english_cards, providers

ROOT = Path(__file__).resolve().parents[1]
BUILD = "26660232"
AUDIT = ROOT / "audit" / BUILD
CAT = ROOT / "mod/catalogs" / BUILD
LEDGER = AUDIT / "card-coverage.tsv"
# Independent managed CardModel inheritance scan (documented in card-coverage.md).
TEXT_ONLY = set(
    """CHRYSALIS DESPAIR FLAME_IN_THE_DARK GHOST_EATER GOOD_INSTINCTS IMMORTALITY
IMPALE OFF_BY_ONE PAELS_STRIKE PITY QUICK_SLASH RESERVES SHARP_EDGE SPIKED STAR_BLAST STAR_SHIELD
SWIFT_STRIKE TEZCATARAS_MIGHT TRAINING_STRIKE VETERAN VISIONS_OF_GRANDEUR WARP_SPACE""".split()
)
MODEL_ONLY = {"MOCK_TURN_END_IN_HAND_RECORDER_CARD"}
FIELDS = (
    "id source indexed_gives indexed_needs base_immediate upgraded_immediate needs effect "
    "base_reward upgraded_reward evidence deferred_gives setup_only"
).split()
CURSES = set(
    """INJURY DECAY DOUBT SHAME REGRET NORMALITY BAD_LUCK GUILTY CLUMSY WRITHE
CURSE_OF_THE_BELL ASCENDERS_BANE GREED DEBT ENTHRALLED FOLLY POOR_SLEEP SPORE_MIND""".split()
)
EXTRA_NEEDS = {
    "card creation": {"PILLAR_OF_CREATION"},
    "melody": {
        "LOST_LUTE",
        "MOTIF",
        "HARMONIC_SHIELD",
        "MELODIC_SHOT",
        "RHYTHM_STRIKE",
        "CODA",
    },
    "impale": {"RECALL", "WREST", "UPROOT"},
}
MELODY = set(
    """CHO_BARBS CHO_CHORUS CHO_FIRM CHO_KI CHO_LIMBS CHO_READY KI_CHANT KI_HOP
KI_REST KI_VERSE WAH_BLAST WAH_CHO WAH_CHO_KI WAH_HAMMER WAH_KI WAH_SWEEP WAH_SYMPHONY WAH_TOUCH""".split()
)
# Verified getters in the native models (not title matching).
EXTRA_GIVES = {
    **{card: {"melody"} for card in MELODY},
    "HIDDEN_DAGGERS": {"shivs"},
    "DIRGE": {"souls"},
    "REAVE": {"souls"},
    "WELL_LAID_PLANS": {"retain hand"},
    "STACK": {"guard"},
    "SHOCKWAVE": {"vulnerable"},
    "THUNDERCLAP": {"vulnerable", "area damage"},
    "PILLAGE": {"draw"},
    "VOLTAIC": {"orbs"},
    "TEMPEST": {"orbs"},
    "RALLY": {"guard"},
    "MIRAGE": {"guard"},
    "HUDDLE_UP": {"draw"},
    "SCRAWL": {"draw"},
    "PURITY": {"exhaust"},
    "ENERGY_SURGE": {"energy"},
    "ROYAL_GAMBLE": {"stars"},
    "YOGA": {"stretch"},
    "THE_MOON": {"shivs"},
    "CALTROPS": {"thorns"},
}
# Deck synergy only. Never feed delayed effects into the current play's previews.
DEFERRED = {
    "NOXIOUS_FUMES": "poison",
    "DEMON_FORM": "strength",
    "FURNACE": "forge",
    "AFTERIMAGE": "guard",
    "DARK_EMBRACE": "draw",
    "TOOLS_OF_THE_TRADE": "discard,draw",
    "REAPER_FORM": "doom",
    "RUPTURE": "strength",
    "INFINITE_BLADES": "shivs",
    "SPINNER": "orbs",
    "ARSENAL": "strength",
    "CORROSIVE_WAVE": "poison",
    "ENNUI": "draw,sadness",
    "DELAY": "energy",
    "INVOKE": "energy,summon",
    "LIGHTNING_ROD": "lightning,orbs",
    "COUNTDOWN": "doom",
    "DEMESNE": "draw,energy",
    "STORM": "lightning,orbs",
    "MACHINE_LEARNING": "draw",
    "SHROUD": "guard",
    "TRASH_TO_TREASURE": "orbs",
    "WAH_KI": "draw,energy",
    "RELAX": "draw,energy",
    "THE_STAR": "draw,energy",
    # The native play installs a power, not its later trigger.
    "ARCHERY": "aim",
    "AUTOMATION": "energy",
    "CHILD_OF_THE_STARS": "guard",
    "COOLANT": "guard",
    "DANSE_MACABRE": "guard",
    "ENVENOM": "poison",
    "GENESIS": "stars",
    "HIDDEN_ROOTS": "stretch",
    "ITERATION": "draw",
    "LULLABY": "guard",
    "ORBIT": "energy",
    "OUTMANEUVER": "energy",
    "PAGESTORM": "draw",
    "PALE_BLUE_DOT": "draw",
    "PILLAR_OF_CREATION": "guard",
    "PLOT": "draw",
    "PROLONG": "guard",
    "RAGE": "guard",
    "SIDESTEP": "energy",
    "SONG_PATH": "guard",
    "SPIRIT_OF_ASH": "guard",
    "SUBROUTINE": "energy",
    "THE_SEALED_THRONE": "stars",
    "TYRANNY": "draw,exhaust",
}
# Power setup/recheck only; do not predict prevention or future retained block.
SETUP_ONLY = {"BUFFER", "BARRICADE"}
LEGACY_HOOKS = {
    "requires-impale",
    "sadness-one",
    "sadness-cards",
    "convert-sadness",
    "repeat-attacks",
    "repeat-skills",
    "block-damage",
    "hand-upgrade",
    "exhaust-choice",
    "uncertain-draw",
}


def combat_path(immediate, needs, effect, deferred, setup):
    if effect:
        return "legacy-hook" if effect in LEGACY_HOOKS else "action-profile"
    if setup == "yes":
        return "setup-only"
    if immediate:
        return "role-preview-candidate"
    if needs or deferred:
        return "deck-evidence-only-candidate"
    return "ineligible"


def table(path, columns):
    result = {}
    for row in csv.reader(path.read_text().splitlines(), delimiter="\t"):
        if len(row) != columns or row[0] in result:
            raise ValueError(f"{path.name}: invalid/duplicate row {row[0]}")
        result[row[0]] = row[1:]
    return result


def equal_ids(label, expected, actual):
    if expected != actual:
        raise ValueError(
            f"{label}: missing={sorted(expected - actual)}, extra={sorted(actual - expected)}"
        )


def upgraded_text(text, up):
    """Resolve only IfUpgraded:show alternatives; their branches can contain nested variables."""
    prefix = "{ifupgraded:show:"
    while prefix in text:
        start = text.rindex(prefix)
        depth, split = 0, None
        for pos in range(start + len(prefix), len(text)):
            char = text[pos]
            if char == "}" and not depth:
                break
            if char == "|" and not depth:
                split = pos
            depth += (char == "{") - (char == "}")
        else:
            raise ValueError("unterminated IfUpgraded field")
        left = text[start + len(prefix) : pos if split is None else split]
        right = "" if split is None else text[split + 1 : pos]
        text = text[:start] + (left if up else right) + text[pos + 1 :]
    return text


def direct_roles(card, text, up):
    if card == "MAD_SCIENCE":
        return set()  # Randomized card type and riders cannot be selected by stable ID.
    text = upgraded_text(re.sub(r"\[/?\w+\]", "", text.lower()), up)
    text = re.sub(r"\{\w+:plural:(\w+)\|\w+\}", r"\1", text)
    # Wrapping a delayed/conditional effect does not turn it into an immediate action.
    text = re.sub(r",\s*\n", ", ", text)
    text = text.partition("{incombat:")[0]
    tags = set()
    for line in text.splitlines():
        for clause in re.split(r"\.\s+", line.strip()):
            if not re.match(
                r"(?:gain|apply|lose|heal|deal|draw|discard|exhaust|retain|"
                r"channel|forge|summon|add|shoot|osty deals|transform this into)\b",
                clause,
            ):
                continue
            for part in re.split(
                r"\s+and\s+(?=apply|gain|draw|deal|channel|forge|summon)", clause
            ):
                for role, pattern in providers.items():
                    if re.match(pattern, part):
                        tags.add(role)
    return tags | EXTRA_GIVES.get(card, set())


def policy(card, gives, needs):
    if card in {"UNDEATH", "TRANSFIGURE", "OBLIVION"}:
        return "bespoke-reward"
    if card in CURSES:
        return "typed-curse"
    return "role-fit" if gives or needs else "abstain"


def native_data(pack):
    release = json.loads((pack.parent / "release_info.json").read_text())
    if (release["version"], release["commit"], release["main_assembly_hash"]) != (
        "v0.113.1",
        "d09d48e5",
        -1414792094,
    ):
        raise ValueError("wrong installed release")
    data = english_cards(pack)
    records = {}
    for key, value in data.items():
        card, _, field = key.partition(".")
        records.setdefault(card, {})[field] = value
    hashes = {
        k: hashlib.sha256(json.dumps(v, sort_keys=True).encode()).hexdigest()
        for k, v in records.items()
    }
    if hashes != json.loads((AUDIT / "card-text-hashes.json").read_text()):
        raise ValueError("installed card fingerprints differ")
    return records


def generate(ids, roles, effects, records):
    rows = []
    for card in sorted(ids | MODEL_ONLY):
        src = (
            "text-only"
            if card in TEXT_ONLY
            else "model-only"
            if card in MODEL_ONLY
            else "model+text"
        )
        indexed, payoff = roles.get(card, ["", ""])
        effect = effects.get(card, [""])[0]
        text = records.get(card, {}).get("description", "")
        base, up, needs = set(), set(), set()
        if (
            src == "model+text"
            and not card.startswith("MOCK_")
            and card != "DEPRECATED_CARD"
        ):
            base, up = (direct_roles(card, text, flag) for flag in (False, True))
            if effect in {"create-one", "create-cards", "create-fill"}:
                base.add("card creation")
                up.add("card creation")
            needs = set(filter(None, payoff.split(",")))
            needs.update(role for role, cards in EXTRA_NEEDS.items() if card in cards)
        notes = []
        if "{IfUpgraded:" in text:
            notes.append("upgrade-branch")
        if re.search(
            r"\b(if|whenever|when|next turn|at the (?:start|end)|for each)\b",
            text,
            re.I,
        ):
            notes.append("conditional-text")
        if card == "MAD_SCIENCE":
            notes.append("variable-riders")

        def csv_tags(tags):
            return ",".join(sorted(tags))

        deferred = DEFERRED.get(card, "")
        rows.append(
            [
                card,
                src,
                indexed,
                payoff,
                csv_tags(base),
                csv_tags(up),
                csv_tags(needs),
                effect,
                policy(card, base or deferred, needs),
                policy(card, up or deferred, needs),
                ",".join(notes) or "-",
                deferred,
                "yes" if card in SETUP_ONLY else "no",
            ]
        )
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pack", type=Path)
    parser.add_argument(
        "--assembly",
        type=Path,
        help="verify the exact managed artifact used for action profiles",
    )
    parser.add_argument(
        "--models",
        type=Path,
        help="TSV from card-coverage-models.cs; validate native and installed-mod inventory",
    )
    parser.add_argument("--write", action="store_true")
    args = parser.parse_args()
    if args.assembly:
        with args.assembly.open("rb") as stream:
            digest = hashlib.sha256()
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        if (
            digest.hexdigest()
            != "a671e9efb86a7b80405133105665867b053c787670d475b4347cf5ccc15cb5fb"
        ):
            raise ValueError("managed action profiles require the audited assembly")
    ids = set(json.loads((AUDIT / "card-text-hashes.json").read_text()))
    if args.models:
        expected_models = ids - TEXT_ONLY | MODEL_ONLY
        normalized = {key.replace("_", ""): key for key in expected_models}
        found = set()
        for line in args.models.read_text().splitlines():
            assembly, name = line.split("\t")
            key = name.rsplit(".", 1)[-1].upper()
            if assembly != "sts2.dll" or key not in normalized:
                raise ValueError(f"new model needs review: {assembly}:{name}")
            card = normalized[key]
            if card in found:
                raise ValueError(f"ambiguous model ID: {card}")
            found.add(card)
        equal_ids("concrete managed models", expected_models, found)
    roles = table(CAT / "roles.tsv", 3)
    effects = table(CAT / "effects.tsv", 2)
    equal_ids("roles vs pack fingerprints", ids, set(roles))
    equal_ids("unaccounted bespoke effects", set(), set(effects) - ids)
    if args.write and not args.pack:
        parser.error("--write requires --pack")
    if args.pack:
        rows = generate(ids, roles, effects, native_data(args.pack))
        expected = "\n".join("\t".join(row) for row in [FIELDS, *rows]) + "\n"
        if args.write:
            LEDGER.write_text(expected)
        elif LEDGER.read_text() != expected:
            raise ValueError("installed pack classifications differ from card ledger")
    rows = table(LEDGER, len(FIELDS))
    if rows.pop("id", None) != FIELDS[1:]:
        raise ValueError("unexpected ledger header")
    equal_ids("ledger vs native union", ids | MODEL_ONLY, set(rows))
    counts, paths = Counter(), Counter()
    for card, row in rows.items():
        src, gives, wants, base, up, needs, effect, bp, upolicy, _, deferred, setup = (
            row
        )
        source = (
            "model-only"
            if card in MODEL_ONLY
            else "text-only"
            if card in TEXT_ONLY
            else "model+text"
        )
        if (
            src != source
            or [gives, wants] != roles.get(card, ["", ""])
            or effect != effects.get(card, [""])[0]
            or bp != policy(card, base or deferred, needs)
            or upolicy != policy(card, up or deferred, needs)
            or deferred != DEFERRED.get(card, "")
            or setup != ("yes" if card in SETUP_ONLY else "no")
            or (source != "model+text" and any((base, up, needs, effect)))
        ):
            raise ValueError(f"inconsistent evidence for {card}")
        counts[bp] += 1
        paths[combat_path(base, needs, effect, deferred, setup)] += 1
    print(
        f"Cards: {len(rows)} accounted ({len(ids)} localized, {len(rows) - len(TEXT_ONLY)} concrete models); "
        f"base reward policies: {dict(sorted(counts.items()))}; "
        f"base combat evidence: {dict(sorted(paths.items()))}."
    )


if __name__ == "__main__":
    main()
