"""Opt-in, serial, fail-closed local advice runner. No live actions on import."""

import datetime
import hashlib
import json
import threading
import time
import urllib.request
from pathlib import Path

_MUTATION_LOCK = threading.Lock()


class Stop(RuntimeError):
    pass


class Journal:
    def __init__(self, path, narrate=None):
        self.path = Path(path)
        self.narrate = narrate

    def emit(self, kind, **fields):
        row = dict(
            utc=datetime.datetime.now(datetime.UTC).isoformat(),
            monotonicNs=time.monotonic_ns(),
            kind=kind,
            **fields,
        )
        with self.path.open("a") as stream:
            stream.write(json.dumps(row, ensure_ascii=False) + "\n")
        if self.narrate:
            self.narrate(row)
        return row


class Bridge:
    def __init__(self, url="http://127.0.0.1:15526/api/v1/singleplayer"):
        from urllib.parse import urlparse

        parsed = urlparse(url)
        if parsed.hostname not in ("localhost", "127.0.0.1") or parsed.scheme != "http":
            raise ValueError("local HTTP bridge only")
        self.url = url
        self.opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))

    def action(self, params):
        request = urllib.request.Request(
            self.url, json.dumps(params).encode(), {"Content-Type": "application/json"}
        )
        with self.opener.open(request, timeout=10) as response:
            return json.load(response)


def snapshot(path):
    return json.loads(Path(path).read_text())


def preflight(state, approved_profile, evidence):
    """Evidence is collected externally, read-only; never guess a profile."""
    setup = state.get("setup", {})
    checks = {
        "optIn": state.get("enabled") is True and state.get("schemaVersion") == 1,
        "profile": bool(approved_profile)
        and evidence.get("profileId") == approved_profile
        and state.get("profileId") == approved_profile,
        "noActiveRunBeforeSelection": evidence.get("noActiveRun") is True,
        "setup": setup.get("guidance") == "Detailed"
        and setup.get("mute") is False
        and setup.get("curse") is False
        and setup.get("catalogMatch") == "Audited",
        "build": bool(setup.get("gameBuild"))
        and setup.get("gameBuild") == evidence.get("gameBuild"),
        "version": bool(setup.get("modVersion"))
        and setup.get("modVersion") == evidence.get("modVersion"),
        "workshop": bool(evidence.get("expectedSha256"))
        and evidence.get("workshopSha256") == evidence.get("expectedSha256"),
        "connectivity": evidence.get("bridgeConnected") is True,
        "capture": evidence.get("captureAvailable") is True,
    }
    if not all(checks.values()):
        raise Stop("preflight: " + ", ".join(k for k, v in checks.items() if not v))
    return checks


def file_sha256(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


class Runner:
    # End-turn, potions, purchases and transitions deliberately need a human.
    SAFE = frozenset({"play_card"})

    def __init__(
        self,
        read_snapshot,
        bridge,
        journal,
        approved_profile,
        run_id,
        readiness_timeout=2.0,
    ):
        self.read = read_snapshot
        self.bridge = bridge
        self.journal = journal
        self.profile = approved_profile
        self.run = run_id
        self.readiness_timeout = min(max(readiness_timeout, 0), 2.0)
        self.seen = set()
        if journal.path.exists():
            for line in journal.path.read_text().splitlines():
                row = json.loads(line)
                if row.get("kind") == "decision":
                    self.seen.add(row["advice"]["decisionId"])
        self.lock = _MUTATION_LOCK

    def guard(self, state, origin, require_ready=True):
        try:
            generated = datetime.datetime.fromisoformat(state["generatedUtc"])
            age = (datetime.datetime.now(datetime.UTC) - generated).total_seconds()
        except (KeyError, TypeError, ValueError) as error:
            raise Stop("missing or invalid heartbeat") from error
        if not 0 <= age <= 5:
            raise Stop("stale or future heartbeat")
        if not self.profile or not self.run:
            raise Stop("explicit approved profile and run required")
        if state.get("schemaVersion") != 1 or state.get("enabled") is not True:
            raise Stop("snapshot disabled or unsupported")
        if state.get("profileId") != self.profile or state.get("runId") != self.run:
            raise Stop("profile/run changed")
        if any(state.get(k) != origin.get(k) for k in ("roomId", "turn")):
            raise Stop("room/turn boundary")
        ready = state.get("readiness", {})
        if ready.get("picker") is not False:
            raise Stop("picker / unknown picker state")
        if require_ready and ready.get("actionable") is not True:
            raise Stop("not actionable / picker")
        if not state.get("stateRevision"):
            raise Stop("missing revision")

    def wait_fresh(self, origin, previous_revision=None):
        """Poll only the local atomic file, never resend a game action."""
        deadline = time.monotonic() + self.readiness_timeout
        start = time.monotonic_ns()
        while True:
            current = self.read()
            self.guard(current, origin, require_ready=False)
            delivered = current.get("delivered") or {}
            if (
                current.get("readiness", {}).get("actionable") is True
                and (
                    previous_revision is None
                    or current["stateRevision"] != previous_revision
                )
                and delivered.get("decisionId") not in self.seen
                and delivered.get("decisionId")
                and delivered.get("text")
                and delivered.get("postCurse") is True
            ):
                self.journal.emit(
                    "readiness",
                    waitNs=time.monotonic_ns() - start,
                    stateRevision=current["stateRevision"],
                )
                return current
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                self.journal.emit(
                    "stop",
                    reason="fresh advice timeout",
                    waitNs=time.monotonic_ns() - start,
                )
                raise Stop("fresh advice timeout; no action retried")
            time.sleep(min(0.05, remaining))

    def follow_current_advice(self, max_actions=8):
        if not self.lock.acquire(blocking=False):
            raise Stop("runner busy")
        try:
            return self._follow(max_actions)
        finally:
            self.lock.release()

    def _follow(self, max_actions):
        origin = self.read()
        origin = self.wait_fresh(origin)
        advice = origin.get("delivered") or {}
        decision = advice.get("decisionId")
        if (
            not decision
            or decision in self.seen
            or advice.get("postCurse") is not True
            or not advice.get("text")
        ):
            raise Stop("no fresh delivered advice")
        self.seen.add(decision)  # never replay after timeout or partial completion
        self.journal.emit(
            "decision", advice=advice, stateRevision=origin["stateRevision"]
        )
        actions = advice.get("actions") or []
        results = []
        expected_revision = origin["stateRevision"]
        for proposed in actions[: min(max_actions, 8)]:
            current = self.read()
            self.guard(current, origin)
            if current["stateRevision"] != expected_revision:
                raise Stop("unexpected state change")
            if proposed.get("action") not in self.SAFE or proposed.get("unsafe"):
                raise Stop("unknown or unsafe action")
            matches = [
                a
                for a in current.get("legalActions", [])
                if all(
                    a.get(k) == proposed.get(k)
                    for k in ("action", "cardRef", "targetRef")
                )
            ]
            if (
                len(matches) != 1
                or not matches[0].get("params")
                or matches[0].get("unsafe")
            ):
                raise Stop("stable reference not uniquely legal")
            legal = matches[0]
            params = legal["params"]
            if params.get("action") != proposed["action"]:
                raise Stop("parameter action mismatch")
            # Narrow race window: no mutations between this check and POST by this runner.
            refreshed = self.read()
            self.guard(refreshed, origin)
            keys = (
                "stateRevision",
                "runId",
                "profileId",
                "roomId",
                "turn",
                "legalActions",
            )
            if (
                any(refreshed.get(k) != current.get(k) for k in keys)
                or refreshed.get("delivered") != current.get("delivered")
                or (current.get("delivered") or {}).get("decisionId") != decision
            ):
                raise Stop("snapshot changed before execution")
            start = time.monotonic_ns()
            self.journal.emit(
                "action",
                decisionId=decision,
                stateRevision=expected_revision,
                selectedTarget=proposed.get("targetRef"),
                params=params,
            )
            try:
                result = self.bridge.action(params)
            except Exception as error:
                self.journal.emit(
                    "deviation",
                    reason="unacknowledged action; no retry",
                    error=str(error),
                )
                raise Stop("unacknowledged action; do not retry") from error
            after = self.read()
            self.journal.emit(
                "result", result=result, state=after, toolNs=time.monotonic_ns() - start
            )
            results.append(result)
            if (
                not isinstance(result, dict)
                or result.get("error")
                or not (result.get("success") is True or result.get("status") == "ok")
            ):
                raise Stop("action failed")
            if (
                proposed.get("recheckAfter")
                or proposed.get("boundary")
                or legal.get("recheckAfter")
                or legal.get("boundary")
            ):
                return {"actions": results, "state": after, "stopBoundary": True}
            after = self.wait_fresh(origin, expected_revision)
            # A changed hand/targets needs fresh delivered advice, not a guessed continuation.
            break  # Parent invalidates delivery on fingerprint change. Require a fresh decision.
        return {"actions": results, "state": self.read()}

    def drive_room(self, max_decisions=4, max_seconds=15):
        origin = self.read()
        start = time.monotonic()
        results = []
        for _ in range(min(max_decisions, 16)):
            if time.monotonic() - start >= min(max_seconds, 30):
                break
            self.guard(self.read(), origin, require_ready=False)
            try:
                result = self.follow_current_advice()
                results.append(result)
                if result.get("stopBoundary"):
                    break
            except Stop as error:
                self.journal.emit("stop", reason=str(error))
                break
        return results
