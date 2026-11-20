# Enemy knowledge — Steam 26660232

Verified against the owned arm64 installation: appmanifest 2868840 build 26660232; `release_info.json` v0.113.1 / d09d48e5 / main assembly hash -1414792094. `sts2.dll` SHA-256 `a671e9efb86a7b80405133105665867b053c787670d475b4347cf5ccc15cb5fb`, MVID `1a667627-d2cf-4b1d-ade2-39da43f6f1f0`. REA metadata identity: `ev_d58ea4726b56c6153a9e6d627cee5e62d02bf8276d63eb91a9070308796dc421` (partial member coverage). Behavior below comes from direct offline decoding of the installed method bodies with System.Reflection.Metadata, not from the REA member summary or a live run.

## Exoskeleton

| Visible move | What it does | Normal follow-up if it completes |
| --- | --- | --- |
| Skitter | Multi-hit attack | The RAND node excludes a logged Skitter repeat, leaving Mandibles. Without matching history, report only the branch. |
| Mandibles | Single attack | Enrage. |
| Enrage | Gains 2 Strength | RAND can pick either attack. No forecast of the roll. |

`Models.Monsters.Exoskeleton.GenerateMoveStateMachine`: CIL offsets 0006–0086 construct the three moves and intents; 0087–00AA add Skitter/Mandibles to RAND, each with literal weight 1 and repeat enum 2 (`CannotRepeat`); 00AF–010B register opening slot checks: first → Skitter, second → Mandibles, third → Enrage, fourth → RAND. 010C–011E wire the three follow-ups. `RandomBranchState.GetStateWeight` excludes a repeat by checking recent logged states; branch nodes themselves do not log. This is a local no-repeat rule, **not** an alternating two-attack cycle. Normal fight and weak fight have different slot rosters. Read the actual move of each creature rather than assuming encounter order.

`Exoskeleton.<EnrageMove>…MoveNext` applies StrengthPower(2) to self; `<AfterAddedToRoom>…MoveNext` applies HardToKillPower(9). `HardToKillPower.ModifyDamageCap` returns the current power amount for its owner. Report the observed cap rather than assuming it persisted. Additional Strength is especially relevant to Skitter. Damage numbers should come from the visible intent and game modifiers.

## Coverage and derivation

- **296** distinct (monster ID, move state ID) intent summaries for **104** monsters, against **360** move-title keys and **127** named monster IDs in the English pack. These are move signatures, not 104 exhaustively scripted fights.
- For each candidate monster's `GenerateMoveStateMachine`, inspect literal state ID, straight-line construction of an `AbstractIntent[]`, its length and each concrete intent constructor leading into `new MoveState`. Keep fully recognized, visible signatures only, matching owner and move against `localization/eng/monsters.json` (remove terminal `_MOVE` for localization lookup). The TSV stores identifiers and classifications. Branching, inherited or dynamic constructions can remain uncataloged. A missing tag does not mean a move does nothing.
- **12** current-power cues. Their semantics are supported by the installed `localization/eng/powers.json` descriptions: Hard to Kill, Illusion, Intangible, Slippery, Buffer, Thorns, Enrage, Curl Up, Painful Stabs, Ritual, Artifact, Strength. Emit at most one present positive power, prioritizing kill/retaliation constraints. Unmapped powers add no advice.
- Normal follow-up topology applies independently of that table. `MonsterModel.NextMove` and `MoveStateMachine` read stored fields. `MoveState.GetNextState` prefers `FollowUpState.Id`, then `FollowUpStateId`; instead of calling it, read the object or look up the ID in the already-built machine's `States` dictionary. A direct MoveState is a fixed **edge**, not a guarantee against death, stun, forced transitions, phase changes or subsequent player actions. Conditional/random nodes are labeled without evaluating delegates, weights or RNG. Missing/unrecognized nodes remain unknown.
- Use only after the caller's intent-visibility checks; disable on approximate builds. Do not call move generation, roll, branch evaluation or move execution. Regression scenarios cover the three Exoskeleton transitions, a missing state log, an unrelated random branch, mixed intents, power priority and exact-profile gating. No game was started.
