# Local playtest runner (opt-in, not installed)

`python3 -m unittest discover -s tools/playtest -p 'test_*.py' -v`

The Entry-owned atomic snapshot is `user://mod_configs/VakuusAgenticAuditor/playtest-state.json`.
`--approved-profile` is an integer, matching snapshot/evidence profileId. Before any
mutation, generatedUtc must be timezone-aware, no more than five seconds old and not
in the future. Fresh heartbeat-only refreshes do not invalidate unchanged decision,
revision, identity and legal-action mappings. CLI narration prints concise action,
result and stop records directly from the same journal stream.
The runner requires schemaVersion 1, enabled=true, approved external profile and run,
roomId/turn/stateRevision, actionable=true/picker=false, and delivered postCurse advice.
Delivered actions identify action/cardRef/targetRef; only the unique matching current
legalActions entry supplies params. No prose parsing or filtered-hand indexing.

`playtest.py preflight --snapshot PATH --evidence evidence.json --approved-profile 2`
requires externally collected evidence: profileId, noActiveRun, gameBuild, modVersion,
workshopSha256, expectedSha256, bridgeConnected, captureAvailable. `--workshop-dll PATH`
computes the actual hash read-only. Snapshot setup must say guidance=Detailed,
catalogMatch=Audited, mute=false, curse=false with matching build/version.
noActiveRun means the **recorded before-selection check**, not the current run.
Setup-only title-screen snapshots may pass preflight without delivered advice.
Execution still requires fresh delivered advice. Evidence provenance is caller-owned;
these assertions are not independent proof of loaded Workshop DLL or active profile.

`follow` or `room` additionally require `--approved-run RUN --allow-game-actions`.
Both default to no mutation without that explicit flag. Room is bounded to 4 decisions/
15 seconds and never crosses a room or turn. Only play_card is enabled; no potion,
end-turn, shop, picker, transition or fallback autoplay. Each mutation requires new
delivered advice. Local snapshot readiness polling is bounded to two seconds by
default, with 50 ms sleeps. It waits for a fresh revision and delivered advice after
acknowledged actions; picker, room, turn, profile and run boundaries stop immediately.
Only the local file is polled, never an action endpoint. No action retries, including timeout.
The CLI uses an advisory process lock beside the snapshot and journals consumed
decision IDs; keep the same journal for the whole run. Other clients/manual game
input must be quiescent: ordinary bridge HTTP provides no atomic compare-and-act.
The library also serializes cooperating threads; only the CLI adds cross-process locking.

`report --journal PATH --output DIR` generates timeline.md, report.md and timing.json.
`history --history EXPLICIT_EXPORTED_JSON --approved-run SEED` checks seed and sums
available native player_stats damage_taken/hp_healed/gold_spent. It never scans saves.
Native history coverage is preliminary; missing fields are not synthesized, turn-level
forecasts are explicitly unavailable, and floor-one HP grants are not tactical healing.

## Reproduce bridge fix

Original edited source files are preserved locally in ignored dist/bridge-originals/
(not part of this public tooling). Apply from sts2-ai:

```
patch -p1 < /path/to/tools/playtest/bridge-beta.patch
```

Both inventory reads use NMerchantRoom.Instance.Inventory.Inventory, so read and buy
share AllEntries ordering. Beta removed OnTryPurchaseWrapper; purchase now uses
RunManager.Instance.MerchantSynchronizer.PurchaseLocalEntry(entry). This remains a
fire-and-forget ordinary bridge action: acknowledgement is not completed-purchase proof.
Do not retry it blindly. Building the patch does not install the bridge.
The zero-context patch also repairs Instant Mode HoverTip initialization: beta Id
is getter-only, so construct the tip through the verified beta constructor instead
of assigning Id. See BRIDGE_PROVENANCE.md for metadata evidence and licensing.

Build in a fresh temporary directory containing only McpMod*.cs and STS2_MCP.csproj,
then `dotnet build TEMP/STS2_MCP.csproj -p:STS2GameDir='OWNED GAME DIR' -o TEMP_OUTPUT`.
Moving only BaseIntermediateOutputPath on this untracked tree includes old generated
assembly files and fails duplicate attributes. Isolated source build succeeds against
the owned macOS ARM64 beta DLLs with zero warnings/errors.

## Validation / remaining work

Fourteen fixture/localhost mock-HTTP tests pass (exit 0), including heartbeat freshness, atomic-file readiness
waiting and no mutation retry on timeout. Isolated bridge build passes
(exit 0). Earlier direct-tree isolated-obj builds failed duplicate assembly attributes;
one attempted semicolon exclude argument failed MSBuild parsing. First isolated-source
build exposed missing OnTryPurchaseWrapper, now corrected and rebuilt successfully.
The offline tests do not perform game actions, installations, restarts, save/profile access or uploads.

Not implemented: multi-select upgrade counts, Instant Mode setting/restore verification,
automatic one-option transitions, UI checkpoint capture, live game readiness validation,
live smoke or benchmark, full history schema certification, measured model/game/UI time.
Current timing reports tool latency only and labels the other lanes missing. The runner
is intentionally conservative rather than claiming full B1–B8 completion.
