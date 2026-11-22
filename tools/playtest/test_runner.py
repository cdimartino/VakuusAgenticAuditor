import copy
import datetime
import json
import tempfile
import threading
import unittest
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

from report import generate
from runner import Bridge, Journal, Runner, Stop, preflight


def fixture():
    action = {"action": "play_card", "cardRef": "card-1", "targetRef": "17"}
    return {
        "schemaVersion": 1,
        "enabled": True,
        "generatedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "profileId": 2,
        "runId": "seed-session",
        "roomId": "1:2:Combat",
        "turn": 1,
        "stateRevision": "1",
        "readiness": {"actionable": True, "picker": False},
        "delivered": {
            "decisionId": "d1",
            "text": "Strike",
            "postCurse": True,
            "actions": [action],
        },
        "legalActions": [
            dict(
                **action,
                params={"action": "play_card", "card_index": 2, "target": "17"},
            )
        ],
    }


class Tests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name) / "events.jsonl"
        self.state = fixture()
        self.calls = []

        class Mock:
            def action(_, params):
                self.calls.append(params)
                self.state["stateRevision"] = "2"
                self.state["delivered"]["decisionId"] = "d2"
                return {"success": True}

        self.runner = Runner(
            lambda: copy.deepcopy(self.state),
            Mock(),
            Journal(self.path),
            2,
            "seed-session",
            readiness_timeout=0.1,
        )

    def test_maps_current_index_and_never_replays(self):
        self.runner.follow_current_advice()
        self.assertEqual(
            self.calls, [{"action": "play_card", "card_index": 2, "target": "17"}]
        )
        self.state["delivered"]["decisionId"] = "d1"
        with self.assertRaises(Stop):
            self.runner.follow_current_advice()
        self.assertEqual(
            generate(self.path, Path(self.temp.name) / "report")["counts"]["action"], 1
        )

    def test_boundaries_and_unknown_are_nonmutating(self):
        for change in [
            {"readiness": {"actionable": True, "picker": True}},
            {"runId": "other"},
            {"profileId": None},
            {"delivered": None},
            {"legalActions": []},
        ]:
            self.state = fixture() | change
            self.runner.seen.clear()
            with self.assertRaises(Stop):
                self.runner.follow_current_advice()
        self.assertEqual(self.calls, [])

    def test_target_disappeared(self):
        self.state["legalActions"][0]["targetRef"] = "18"
        with self.assertRaises(Stop):
            self.runner.follow_current_advice()
        self.assertEqual(self.calls, [])

    def test_legal_params_require_action(self):
        del self.state["legalActions"][0]["params"]["action"]
        with self.assertRaises(Stop):
            self.runner.follow_current_advice()
        self.assertEqual(self.calls, [])

    def test_setup_only_preflight(self):
        self.state["delivered"] = None
        self.state["setup"] = {
            "guidance": "Detailed",
            "mute": False,
            "curse": False,
            "catalogMatch": "Audited",
            "gameBuild": "build",
            "modVersion": "v",
        }
        evidence = {
            "profileId": 2,
            "noActiveRun": True,
            "gameBuild": "build",
            "modVersion": "v",
            "expectedSha256": "hash",
            "workshopSha256": "hash",
            "bridgeConnected": True,
            "captureAvailable": True,
        }
        self.assertTrue(all(preflight(self.state, 2, evidence).values()))
        with self.assertRaisesRegex(Stop, "profile"):
            preflight(self.state | {"profileId": None}, 2, evidence)

    def test_stale_heartbeat_never_mutates(self):
        self.state["generatedUtc"] = (
            datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(seconds=6)
        ).isoformat()
        with self.assertRaisesRegex(Stop, "heartbeat"):
            self.runner.follow_current_advice()
        self.assertEqual(self.calls, [])

    def test_fresh_heartbeat_refresh_does_not_invalidate_mapping(self):
        def read():
            state = copy.deepcopy(self.state)
            state["generatedUtc"] = datetime.datetime.now(
                datetime.timezone.utc
            ).isoformat()
            return state

        self.runner.read = read
        self.runner.follow_current_advice()
        self.assertEqual(len(self.calls), 1)

    def test_heartbeat_stales_between_validation_and_post(self):
        reads = 0

        def read():
            nonlocal reads
            reads += 1
            state = copy.deepcopy(self.state)
            if reads >= 4:
                state["generatedUtc"] = (
                    datetime.datetime.now(datetime.timezone.utc)
                    - datetime.timedelta(seconds=6)
                ).isoformat()
            return state

        self.runner.read = read
        with self.assertRaisesRegex(Stop, "heartbeat"):
            self.runner.follow_current_advice()
        self.assertEqual(self.calls, [])

    def test_timeout_never_retried(self):
        def fail(params):
            self.calls.append(params)
            raise TimeoutError()

        self.runner.bridge.action = fail
        for _ in range(2):
            with self.assertRaises(Stop):
                self.runner.follow_current_advice()
        self.assertEqual(len(self.calls), 1)

    def test_preflight_empty_evidence_fails(self):
        with self.assertRaises(Stop):
            preflight(self.state, "2", {})

    def test_room_driver_stops_without_new_advice(self):
        self.state["delivered"]["actions"][0]["recheckAfter"] = True
        self.runner.drive_room()
        self.assertEqual(len(self.calls), 1)

    def test_atomic_file_readiness_wait(self):
        from runner import snapshot

        path = Path(self.temp.name) / "state.json"
        state = fixture()
        state["delivered"] = None
        path.write_text(json.dumps(state))
        first_read = threading.Event()
        self.runner.readiness_timeout = 3

        def read():
            current = snapshot(path)
            first_read.set()
            return current

        self.runner.read = read

        def publish():
            if not first_read.wait(timeout=3):
                return
            fresh = fixture()
            fresh["stateRevision"] = "2"
            temporary = path.with_suffix(".tmp")
            temporary.write_text(json.dumps(fresh))
            temporary.replace(path)

        timer = threading.Thread(target=publish)
        timer.start()
        try:
            result = self.runner.wait_fresh(state, "1")
            self.assertEqual(result["stateRevision"], "2")
            self.assertEqual(self.calls, [])
        finally:
            timer.join()

    def test_readiness_timeout_does_not_retry_mutation(self):
        def action(params):
            self.calls.append(params)
            return {"status": "ok"}

        self.runner.bridge.action = action
        with self.assertRaises(Stop):
            self.runner.follow_current_advice()
        with self.assertRaises(Stop):
            self.runner.follow_current_advice()
        self.assertEqual(len(self.calls), 1)

    def test_real_http_transport_against_mock(self):
        calls = self.calls

        class Handler(BaseHTTPRequestHandler):
            def do_POST(self):
                calls.append(
                    json.loads(self.rfile.read(int(self.headers["Content-Length"])))
                )
                self.send_response(200)
                self.end_headers()
                self.wfile.write(b'{"status":"ok"}')

            def log_message(self, *args):
                pass

        server = HTTPServer(("127.0.0.1", 0), Handler)
        thread = threading.Thread(target=server.handle_request)
        thread.start()
        try:
            result = Bridge(
                f"http://127.0.0.1:{server.server_port}/api/v1/singleplayer"
            ).action({"action": "play_card", "card_index": 1})
            self.assertEqual(result["status"], "ok")
            self.assertEqual(calls[0]["card_index"], 1)
        finally:
            thread.join(timeout=2)
            server.server_close()


if __name__ == "__main__":
    unittest.main()
