# Luna: next Ironclad A0 run

**Question:** Can Luna follow the advice a player actually sees, at speed, and produce a review traceable to observed game state? A natural defeat is a valid outcome. The previous run reached floor 25 in 7,153 seconds on the game timer; it did not certify Detailed guidance.

**Local title-screen gate: passed on `f2b0d28`.** Both F8 and **General → Vakuu’s Agentic Auditor…** opened the readable in-game panel. The UI applied Detailed, showed recording enabled, and enabled export. Two fresh title snapshots confirmed the audited build, mute/Curse off, and no actionable run. The UI restored Balanced and disabled export; the original installation was restored. This clears the earlier popup blocker for the local candidate, not for an arbitrary downloaded copy. **Workshop certification is still blocked** until item 3842451639 delivers the verified candidate instead of v0.1.20. Repeat the title gate on that copy, then use the first-combat checkpoint below on an authorized run.

### 1. Prove settings access without starting a run

- With permission for the installation being tested, verify the loaded DLL hash and source against the new package, not just its version string. Earlier candidates also say v0.1.21. Keep only one Auditor source enabled. Save original settings, including the bound shortcut and export/recording toggles.
- On **Profile 2**, verify both the real main menu and a read-only MCP state. Confirm there is no active run/save before selection. Stop on a discrepancy; do not use another profile.
- Try the bound settings key **once** outside a transition. Save the before/after window captures. Look for a new `[VakuuAuditor] Settings popup visible` line in the same time window. A sent key is not evidence of a visible popup; a new visibility line without a corresponding picture points to a capture discrepancy.
- If that fails, **do not retry F8 in a loop**. From the main menu use MCP `menu_select(option="settings")`; verify the resulting `menu_screen="settings"`. In the game UI choose **General → Vakuu’s Agentic Auditor…**. This opens the same popup without a function key. If MCP does not advertise Settings, use the visible Settings button. Never click assumed coordinates on a stale capture.
- Verify the Auditor popup visually, apply the Detailed preset, enable recording and local export, and verify **Audited / build 26660232 / Detailed / mute off / Curse off** in the UI and fresh setup export where available. Keep one screenshot. Close the popup and verify normal input resumes. If only the button worked, record **shortcut failed, settings accessible**; do not mark F8 passed. If neither route works, **stop at title** and preserve the screenshots, binding, log, setup snapshot (if available), and last MCP state. Do not edit preferences to bypass this gate.

### 2. Start one authorized baseline

For Workshop certification, wait for separate release/upload approval and verify the **downloaded** item 3842451639 against the candidate package and startup log. A local developer smoke is not a Workshop result. Obtain permission for this fresh run; the earlier startup-only authorization is not gameplay authorization.

Use Ironclad, A0, uncursed, and the previous seed `26J0YKBB13FM` if the normal game flow supports it without changing rules. Otherwise record the actual seed and label the run non-paired. Follow the [preflight and runner commands](PLAYTESTING.md), passing the actual downloaded DLL via `--workshop-dll`. Capture the approved run ID from its first fresh snapshot. Do not continue if profile, build, catalog or guidance differs.

### 3. First-turn checkpoint, then fast play

At the first advised combat turn, compare a visible bubble with the exported delivered text. Check that the journal has a state, the delivered advice, and a subsequent completed card play. Let the guarded runner play one advised card; confirm card instance, target and resulting revision. If the action times out, inspect the new state **without resending it**. If parity, journaling or targeting fails, pause safely and report; do not play an uninstrumented run for hours.

After that checkpoint, use the bounded same-turn runner for ordinary card plays. On draw/picker boundaries read the fresh decision. Handle end turns, potions, rewards, map travel and purchases with ordinary MCP actions, serially. Use only legal delivered advice; when none applies, label the fallback and rationale in the timeline. Never buy twice on an ambiguous acknowledgment. Narrate decisions from the action journal as they happen; do not reconstruct a timeline afterward.

Take representative screenshots at normal speed: first combat, reward hitboxes, long text, drag/dismiss/recall, forced-route request, settings and end review. Check the merchant read/purchase path if encountered naturally and recommended. Mark missing encounters **not exercised**, not passed. Use Instant Mode only after its behavior and restoration have been verified; it is not required.

### 4. Finish, measure, restore

Continue to natural victory or defeat, not a floor or time cutoff. At game over, dismiss and reopen the review from results, save its text, and reconcile its floor totals with the matching native history. Distinguish observed actions and net HP changes from causal claims about the loss.

Report game time, end-to-end wall time, setup/reporting and human waits separately. At floor 25, if reached, note elapsed active time; the target is **≤40 minutes** (not yet demonstrated). Also report model/tool calls per combat turn, state reads per action, screenshot count, median/p95 runner latency, and evidence gaps. Flag missing timing components rather than treating them as zero.

Deliver `report.md`, a UTC `timeline.md`, and an `evidence/` folder with run journal, runner JSONL, key screenshots, review, matching history and settings/restore evidence. Restore only test-owned changes, including recording/export and any speed setting. Verify original profile/install state; never delete a save to tidy up. If blocked, deliver the same report with the last valid state and next safe action.
