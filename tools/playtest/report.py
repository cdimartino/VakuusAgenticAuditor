"""Generate evidence tables without inventing missing turn measurements."""

import collections
import json
import math
from pathlib import Path


def generate(journal, output):
    rows = [json.loads(line) for line in Path(journal).read_text().splitlines() if line]
    counts = collections.Counter(row["kind"] for row in rows)
    durations = sorted(row["toolNs"] / 1e6 for row in rows if "toolNs" in row)

    def percentile(p):
        return (
            durations[max(0, math.ceil(len(durations) * p) - 1)] if durations else None
        )

    summary = {
        "counts": dict(counts),
        "toolMedianMs": percentile(0.5),
        "toolP95Ms": percentile(0.95),
        "modelTime": "missing",
        "gameWaitTime": "missing",
        "uiTime": "missing",
    }
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    (output / "timing.json").write_text(json.dumps(summary, indent=2))
    (output / "timeline.md").write_text(
        "# Timeline\n\n"
        + "\n".join(
            f"- {r['utc']} [{r['monotonicNs']}] {r['kind']}: `{json.dumps(r, ensure_ascii=False)}`"
            for r in rows
        )
    )
    (output / "report.md").write_text(
        "# Runner evidence\n\n```json\n"
        + json.dumps(summary, indent=2)
        + "\n```\n\nTurn forecasts absent from the journal remain missing.\n"
    )
    return summary


def reconcile_history(history, expected_seed):
    """Read an explicitly supplied exported history, never scan profiles/saves."""
    data = json.loads(Path(history).read_text())
    if data.get("seed") != expected_seed:
        raise ValueError("history seed does not match")
    floors = data.get("map_point_history", [])
    totals = collections.Counter()
    floor_count = 0
    for floor in floors:
        # Native history groups visits by act; tolerate flat sanitized fixtures.
        for visit in floor if isinstance(floor, list) else [floor]:
            floor_count += 1
            totals["combat_turns"] += sum(
                room.get("turns_taken", 0) for room in visit.get("rooms", [])
            )
            for stats in visit.get("player_stats", []):
                for key in ("damage_taken", "hp_healed", "gold_spent"):
                    if isinstance(stats.get(key), (int, float)):
                        totals[key] += stats[key]
                for key in (
                    "potion_used",
                    "cards_gained",
                    "cards_removed",
                    "upgraded_cards",
                ):
                    if isinstance(stats.get(key), list):
                        totals[key] += len(stats[key])
    return {
        "seed": expected_seed,
        "runTime": data.get("run_time"),
        "floors": floor_count,
        "outcome": "victory" if data.get("win") is True else "not victory",
        "killedBy": data.get("killed_by_encounter"),
        "totals": dict(totals),
        "turnForecasts": "not reconstructable from history",
        "startingHpGrants": "not classified; inspect floor-one grants separately",
    }
