"""Rebuild semantic tags from a locally owned, unencrypted Godot 4.5 game pack.
Only card identifiers and our classifications are emitted, never game text/art."""

import json
import hashlib
import re
import struct
import sys
from pathlib import Path


def english_cards(pack, table="cards"):
    with pack.open("rb") as f:
        if f.read(8) != b"GDPC\x03\0\0\0":
            raise ValueError("Expected Godot pack format 3")
        f.seek(24)
        base, directory = struct.unpack("<QQ", f.read(16))
        f.seek(directory)
        for _ in range(struct.unpack("<I", f.read(4))[0]):
            name = f.read(struct.unpack("<I", f.read(4))[0]).rstrip(b"\0")
            offset, length = struct.unpack("<QQ", f.read(16))
            f.read(20)
            if name == f"localization/eng/{table}.json".encode():
                f.seek(base + offset)
                return json.loads(f.read(length))
        raise ValueError(f"No English {table} catalog")


# Identify effects, not every mention of a keyword. Conditional payoffs are
# curated separately so e.g. “whenever a card is Exhausted” is not an enabler.
providers = {
    "guard": r"^gain [^.\n]*? block\b",
    "hit": r"^(deal|osty deals|shoot an arrow to deal) .*?damage",
    "draw": r"^draw (?:\{|\d|a card|until\b)",
    "energy": r"^gain \{energy:energyicons",
    "exhaust": r"\bexhaust (?:your|all|a card|1|it\b)",
    "strength": r"\bgain [^.\n]{1,55} strength\b",
    "poison": r"\bapply [^.\n]{1,55} poison\b",
    "shivs": r"\badd [^.\n]{1,55} shivs?\b",
    "discard": r"\bdiscard (?:\d|\{|your hand|a card)",
    "orbs": r"\bchannel (?:\d|\{|a |an )",
    "forge": r"\bforge (?:\d|\{)",
    "stars": r"\bgain\s+\{stars:staricons",
    "doom": r"\bapply [^.\n]{1,55} doom\b",
    "summon": r"\bsummon (?:\d|\{)",
    "souls": r"\badd [^.\n]{1,60} souls?\b",
    "shots": r"\bshoot an arrow",
    "sadness": r"\badd [^.\n]{1,55} sadness\b|transform this into sadness",
    "weak": r"^apply \{(?:weakpower|power):diff\(\)\} weak\b",
    "vulnerable": r"^apply \{(?:vulnerablepower|power):diff\(\)\} vulnerable\b",
    "hp loss": r"^lose \{hploss:diff\(\)\} hp\b",
    "multiple hits": r"^deal \{damage:diff\(\)\} damage(?: to all enemies)? (?:\{hitcount:|twice\b|three times\b)",
    "Osty attacks": r"^osty deals\b",
    "stretch": r"^gain [^.\n]{1,70} stretch\b",
    "aim": r"^gain [^.\n]{1,70} aim\b",
    "impale": r"\bshoot an arrow",
    "frost": r"^channel [^.\n]{1,60} frost\b",
    "lightning": r"^channel [^.\n]{1,60} lightning\b",
    "focus": r"^gain [^.\n]{1,60} focus\b",
    "dexterity": r"^gain [^.\n]{1,60} dexterity\b",
    "artifact": r"^gain [^.\n]{1,60} artifact\b",
    "thorns": r"^gain [^.\n]{1,60} thorns\b",
    "plating": r"^gain [^.\n]{1,60} plating\b",
    "retain hand": r"^retain your hand",
    "healing": r"^heal (?:\d|\{)",
    "area damage": r"^deal [^.\n]+damage to all enemies",
}
consumers = {
    "exhaust": "FEEL_NO_PAIN DARK_EMBRACE ASHEN_STRIKE EVIL_EYE MIDNIGHT",
    "strength": "HEAVY_BLADE SWORD_BOOMERANG WHIRLWIND",
    "poison": "ACCELERANT CATALYST",
    "shivs": "ACCURACY",
    "discard": "TACTICIAN REFLEX",
    "orbs": "BARRAGE DEFRAGMENT CAPACITOR",
    "forge": "CONQUEROR",
    "stars": "RADIATE",
    "doom": "SHROUD DEATHS_DOOR NO_ESCAPE",
    "summon": "PROTECTOR UNLEASH CALCIFY NECRO_MASTERY",
    "souls": "SOUL_STORM",
    "shots": "HONING QUIETUS WHISTLING_ARROWS",
    "sadness": "ALL_IS_WELL GROWTH ANGUISH ECSTASY",
    "vulnerable": "BULLY DISMANTLE VICIOUS",
    "hp loss": "RUPTURE",
    "weak": "TRACKING",
    "impale": "NAIL WAH_HAMMER SERRATED_ARROWS",
    "aim": "HEAVY_SHOT",
    "frost": "HAILSTORM",
    "lightning": "VOLTAIC",
    "guard": "BODY_SLAM ENTRENCH",
}
if __name__ == "__main__":
    pack = Path(sys.argv[1])
    release = json.loads((pack.parent / "release_info.json").read_text())
    if not sys.argv[2].isdecimal():
        raise SystemExit("Supply the installed Steam build ID as the second argument")
    # Generate in a fresh staging directory, never over a shipped snapshot.
    out = Path(__file__).resolve().parents[1] / "dist/catalog-candidates" / sys.argv[2]
    out.mkdir(parents=True, exist_ok=False)
    (out / "release_info.json").write_text(json.dumps(release, indent=2) + "\n")
    data = english_cards(pack)
    # Store digests, not game text. Include upgrade text and other per-card
    # localization keys; assembly-only numeric changes still require review.
    records = {}
    for key, value in data.items():
        card, _, field = key.partition(".")
        records.setdefault(card, {})[field] = value
    fingerprints = {
        card: hashlib.sha256(json.dumps(fields, sort_keys=True).encode()).hexdigest()
        for card, fields in sorted(records.items())
    }
    (out / "card-text-hashes.json").write_text(
        json.dumps(fingerprints, indent=2) + "\n"
    )
    if len(sys.argv) > 3:
        previous = json.loads(Path(sys.argv[3]).read_text())
        changes = {
            "added": sorted(fingerprints.keys() - previous.keys()),
            "removed": sorted(previous.keys() - fingerprints.keys()),
            "changed_text": sorted(
                k
                for k in previous.keys() & fingerprints.keys()
                if previous[k] != fingerprints[k]
            ),
        }
        (out / "card-changes.json").write_text(json.dumps(changes, indent=2) + "\n")
    lines = []
    for key, value in sorted(data.items()):
        if not key.endswith(".description"):
            continue
        name = key.split(".")[0]
        text = re.sub(r"\[/?\w+\]", "", value.lower())
        text = re.sub(r"\{\w+:plural:(\w+)\|\w+\}", r"\1", text)
        tags = [k for k, pattern in providers.items() if re.search(pattern, text, re.M)]
        needs = [k for k, ids in consumers.items() if name in ids.split()]
        lines.append("\t".join([name, ",".join(tags), ",".join(needs)]))
    (out / "roles.tsv").write_text("\n".join(lines) + "\n")
    # Require whole-clause matches. "Heal ... You can no longer ..." is
    # materially different from a free heal and must not be scored as one.
    patterns = {
        "max hp": r"upon pickup, (?:raise your max hp by|gain) \{maxhp\}(?: max hp)?\.",
        "gold": r"upon pickup, gain \{gold\} gold\.",
        "sustain": r"at the (?:start|end) of (?:each )?combat, heal \{heal\} hp\.",
        "rest heal": r"whenever you rest, heal an additional \{heal\} hp\.",
        "opening block": r"start each combat with \{block\} block\.",
        "opening energy": r"start each combat with an additional \{energy:energyicons\(\)\}\.",
        "energy": r"every .*?turns\}, gain \{energy:energyicons\(\)\}\.",
        "opening draw": r"at the start of each combat, draw \{cards\} additional .*?\.",
        "slots": r"upon pickup, gain \{potionslots\} potion .*?\.",
        "strength": r"start each combat with \{strengthpower\} strength\.",
        "dexterity": r"start each combat with \{dexteritypower\} dexterity\.",
    }
    relics = english_cards(Path(sys.argv[1]), "relics")
    lines = []
    for key in sorted(k for k in relics if k.endswith(".title") and k.count(".") == 1):
        name = key.split(".")[0]
        text = re.sub(r"\[/?\w+\]", "", relics.get(name + ".description", "").lower())
        lines.append(
            name
            + "\t"
            + next(
                (tag for tag, pat in patterns.items() if re.fullmatch(pat, text)), ""
            )
        )
    (out / "relic-fit.tsv").write_text("\n".join(lines) + "\n")
