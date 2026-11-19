# Fast, evidence-based Vakuu playtests

Normal users do not need the MCP bridge or this runner. These are local QA tools, not part of the Workshop payload. The v0.1.21 candidate adds recorded state and structured advice; the previously uploaded v0.1.20 cannot export this protocol.

## What changed after the first full run

The Ironclad A0 run on seed `26J0YKBB13FM` reached a natural defeat at Act 2/F25. Native history records 7,153 seconds and 47 combat turns. Artifact timestamps span roughly 2h49m from setup through the final report. During that broader window, 408 Code Mode requests mentioned game-state reads and 356 mentioned visual inspection; these counts overlap and are not a latency breakdown. The original report remains unchanged.

Read-only reconciliation against the matching history recovered its seed, elapsed time, two Strike/two Defend upgrades at Tezcatara, and the recorded totals: 56/89 damage and 100/45 healing by act, three potions, 306 gold spent and ten upgrades. Floor-one healing includes 80 recorded HP; that is not evidence of tactical healing. Individual Kin turn forecasts cannot be recovered from this history. Balanced guidance does not establish or refute exact catalog selection.

The next run should spend fewer model turns on routine inputs, not skip the visual assertions or rewrite missing evidence after the fact.

## Preflight before starting

1. Stage the candidate, complete the normal build/package checks, and separately smoke-test its UI and bridge. Do not label it live-tested or publish it based on offline checks alone.
2. Quit the game safely before any installation/update. Verify the downloaded Workshop manifest and DLL against the intended package, not the upload workspace alone. Avoid a duplicate manual Auditor install.
3. Use an explicitly approved disposable profile. Do not abandon or resume anyone else's run. Record that the chosen profile had no active run before character selection.
4. Enable **STS2_MCP** and verify a read-only state request. The beta merchant fix is in [bridge-beta.patch](../tools/playtest/bridge-beta.patch), with [provenance and build details](../tools/playtest/BRIDGE_PROVENANCE.md). The patch is not automatically installed. It fixes both inventory reading and purchase dispatch; compile success is not proof of live shop behavior.
5. In Auditor settings, apply the **Detailed playtest preset**. Independently verify loaded mod/game/catalog, Detailed guidance, mute off, Curse off, and exact catalog matching. The preset is reversible and restart-persistent; restoring leaves settings alone if they no longer match its owned values. Do not assume applying the preset changed a catalog already loaded with an override; restart and verify if necessary.
6. Enable the default-off local playtest export. On macOS its snapshot is normally `~/Library/Application Support/SlayTheSpire2/mod_configs/VakuusAgenticAuditor/playtest-state.json`. Use the actual Godot user directory if your installation differs.
7. Capture the window once and confirm visual advice can be read. Keep every original setting needed for restoration, including text size, positions and any Instant Mode setting.

Collect preflight evidence from real observations into a local JSON file. Fields are `profileId` (integer), `noActiveRun` (the before-selection observation), `gameBuild`, `modVersion`, `workshopSha256`, `expectedSha256`, `bridgeConnected`, and `captureAvailable`. Build/version are strings. Do not fill these with assumptions. The preflight checks consistency of supplied evidence; it does not independently certify its provenance.

```sh
python3 tools/playtest/playtest.py preflight \
  --snapshot "$SNAPSHOT" --evidence "$EVIDENCE" --approved-profile 2
```

Title-screen snapshots can pass setup preflight without a delivered recommendation. The action runner additionally requires a fresh, actionable, post-Curse recommendation.

## Rapid execution without stale inputs

The export uses stable per-instance card references, creature combat IDs, and current legal action parameters. Do not parse bubble text into card indices. If an action changes hand, resources or targets, the old delivery is retired before a new one can run. Curse-withheld or transformed advice is non-executable unless a corresponding action is explicitly supplied; the runner never substitutes the honest internal plan.

Start only the approved run. Read its opaque `runId` from the live snapshot, then authorize that specific run:

```sh
python3 tools/playtest/playtest.py room \
  --snapshot "$SNAPSHOT" --evidence "$EVIDENCE" \
  --approved-profile 2 --approved-run "$RUN_ID" \
  --journal "$RUN_DIR/actions.jsonl" --allow-game-actions
```

`room` is deliberately bounded to the same room and turn. It follows fresh combat card recommendations serially, not an arbitrary scripted sequence. It waits on the local atomic snapshot for at most two seconds, sleeping between checks; it never resends a mutation after a timeout. Multiple cooperating CLI instances are locked out. Do not operate another input client simultaneously: the ordinary MCP API has no atomic compare-and-act transaction.

Current automatic coverage is **combat card plays only**. Potions, end turn, picks, purchases, transitions and unknown actions remain explicit agent/player operations. A draw, hand-upgrade/exhaust picker, stale snapshot, room/turn change, or unsupported advice stops the driver. Resume from fresh state after resolving that boundary. A safety stop is not a completed run or proof of a product bug.

Use MCP directly for the unsupported ordinary actions, following the same visible advice. Keep visual checks for the first hint in each screen type, long text/scrolling, click-to-dismiss, drag persistence, mute, and the results-review button. Do not screenshot every routine card. Emit concise narration at each decision/turn and immediately for an anomaly; the CLI streams action/result/stop events from the same JSONL journal.

The bridge's existing Instant Mode can reduce animation waits, but its target-build setting and restoration must first be verified. Keep normal-speed samples for animation and bubble assertions. Never enable a speed control by editing saves.

## End-of-run evidence and performance

```sh
python3 tools/playtest/playtest.py report \
  --journal "$RUN_DIR/actions.jsonl" --output "$RUN_DIR/generated"
python3 tools/playtest/playtest.py history \
  --history "$EXPORTED_HISTORY" --approved-run "$SEED"
```

For `history`, the argument is the game's seed, not the live snapshot's opaque run ID. Select the exported history by profile, start time and outcome as well; a repeated seed is not a unique run. The command never scans or modifies saves.

The runner report is a mechanical evidence index, not a replacement for the final QA findings. Attach the mod's journal, results screenshot, completed history and settings-restore result. Preserve uncertainty and do not infer a winning counterfactual from a death.

Measure actionable-state-to-first-action latency, tool time, model/tool round trips, state reads per action, screenshots per floor and setup/reporting time. The current runner measures tool/readiness timings, not all model/UI time; report unavailable measurements as missing. Use fixture replay and a short live smoke before a full run. The intended 25-floor throughput target is at most 40 minutes of active runner time versus the old ~119-minute game timer; **that speedup has not been demonstrated**. Also report end-to-end wall time and human waits separately. Never abandon a run at floor 25 just to meet a benchmark.

## Offline checks

```sh
dotnet run --project tests -c Release
python3 -m unittest discover -s tools/playtest -p 'test_*.py' -v
sh tools/ci.sh
```

No offline test certifies actual Workshop delivery, live UI input, game-preview parity, Instant Mode, or the claimed throughput target. Complete those before the next certification playtest.
