"""Explicit commands only; preflight/report are read-only toward the game."""

import argparse
import fcntl
import json
from pathlib import Path

from report import generate, reconcile_history
from runner import Bridge, Journal, Runner, Stop, file_sha256, preflight, snapshot


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "command", choices=["preflight", "follow", "room", "report", "history"]
    )
    parser.add_argument("--snapshot")
    parser.add_argument("--evidence")
    parser.add_argument("--approved-profile", type=int)
    parser.add_argument("--approved-run")
    parser.add_argument("--journal", default="playtest.jsonl")
    parser.add_argument("--output", default="playtest-report")
    parser.add_argument("--history")
    parser.add_argument("--workshop-dll")
    parser.add_argument("--allow-game-actions", action="store_true")
    args = parser.parse_args()
    if args.command == "report":
        print(json.dumps(generate(args.journal, args.output)))
        return
    if args.command == "history":
        print(json.dumps(reconcile_history(args.history, args.approved_run), indent=2))
        return
    state = snapshot(args.snapshot)
    evidence = json.loads(Path(args.evidence).read_text())
    if args.workshop_dll:
        evidence["workshopSha256"] = file_sha256(args.workshop_dll)
    checks = preflight(state, args.approved_profile, evidence)

    def narrate(row):
        if row["kind"] in ("action", "result", "stop", "deviation"):
            result = row.get("result") or {}
            detail = (
                row.get("params")
                or row.get("reason")
                or result.get("status", result.get("success", "response received"))
            )
            print(f"{row['utc']} {row['kind']}: {detail}", flush=True)

    journal = Journal(args.journal, narrate=narrate)
    journal.emit("preflight", checks=checks, evidence=evidence)
    if args.command == "preflight":
        print(json.dumps(checks))
        return
    if not args.allow_game_actions:
        raise Stop("--allow-game-actions required; nothing sent")
    # One cooperating CLI process across journals. No automatic lock retries.
    with Path(args.snapshot).with_suffix(".runner.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        runner = Runner(
            lambda: snapshot(args.snapshot),
            Bridge(),
            journal,
            args.approved_profile,
            args.approved_run,
        )
        result = (
            runner.drive_room()
            if args.command == "room"
            else runner.follow_current_advice()
        )
        print(json.dumps(result))


if __name__ == "__main__":
    main()
