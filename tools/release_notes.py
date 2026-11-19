"""Print the exact, player-facing changelog entry for a release."""

import re
import sys
from pathlib import Path


def notes(version):
    text = (Path(__file__).resolve().parents[1] / "CHANGELOG.md").read_text()
    sections = re.split(r"^## (.+)$", text, flags=re.MULTILINE)
    entries = [
        body.strip()
        for heading, body in zip(sections[1::2], sections[2::2])
        if heading == version
    ]
    if len(entries) != 1 or not re.search(r"^- \S", entries[0], flags=re.MULTILINE):
        raise ValueError(f"Expected one changelog section with bullets for {version}")
    return entries[0]


if __name__ == "__main__":
    print(notes(sys.argv[1]))
