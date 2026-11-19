"""Compare both live Steam branches with audited, shipped builds."""

import json
import subprocess
import sys
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
API = "https://api.steamcmd.net/v1/info/2868840"


def main():
    profiles = json.loads((ROOT / "mod/catalogs/index.json").read_text())
    with urlopen(API, timeout=30) as response:
        branches = json.load(response)["data"]["2868840"]["depots"]["branches"]
    for channel in ("public", "public-beta"):
        build = str(branches[channel]["buildid"])
        if not build.isdecimal():
            raise ValueError("Invalid Steam build ID")
        if any(p["build"] == build for p in profiles):
            print(channel, build, "audited")
            continue
        title = f"Audit STS2 {channel} build {build}"
        print(title, flush=True)
        if "--notify" not in sys.argv:
            continue
        matches = json.loads(
            subprocess.check_output(
                [
                    "gh",
                    "issue",
                    "list",
                    "--state",
                    "all",
                    "--search",
                    f'"{title}" in:title',
                    "--json",
                    "title",
                ]
            )
        )
        if not any(issue["title"] == title for issue in matches):
            subprocess.run(
                [
                    "gh",
                    "issue",
                    "create",
                    "--title",
                    title,
                    "--body",
                    f"Steam reports `{channel}` at build `{build}`. No audited catalog is shipped for that build. "
                    "Stage tables from this exact installation, review card text and numeric/mechanical changes, "
                    "run the audit and coaching scenarios, then register its release identity. See audit/README.md.",
                ],
                check=True,
            )


if __name__ == "__main__":
    main()
