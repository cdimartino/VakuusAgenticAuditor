# Changelog

## v0.1.21

- Settings now responds to a delivered shortcut press instead of waiting for a frame to sample the held key. **Settings → General → Vakuu’s Agentic Auditor…** remains available without a keyboard shortcut.
- Auditor settings now appears inside the game window, so the panel and its playtest controls can be captured alongside the game. The title-screen export also reports whether run recording is enabled.

- Vakuu now records local game-state snapshots, delivered advice and completed card plays for end-of-run analysis. The review highlights observed HP drops, links them to recorded advice, and shows a per-floor history. Recording can be disabled in settings; gaps are identified rather than filled in.
- Combat advice keeps lethal-risk warnings visible, distinguishes current damage from Strength/Illusion threats, stops at uncertain draw and hand-changing effects, and considers gaining block before Body Slam. Damage forecasts remain estimates.
- Added on-demand guidance on forced map routes, Potion Courier tradeoffs, accurate Ancient upgrade counts and remaining multi-upgrade priorities.
- Settings now shows the coaching setup, offers a reversible Detailed playtest preset, and exposes an opt-in local advice snapshot for the guarded playtest runner. The results-screen review button is anchored at the top right.
- Added a separate local playtest runner and a reproducible beta merchant-API patch for the optional STS2 MCP bridge. Neither is required for normal play or automatically installed.

## v0.1.20

- On an unknown game patch, you can now opt into the closest audited catalog from Auditor settings. Restart to apply. Settings identifies approximate advice and shows which catalog is active. Exact-build matching remains the default.

## v0.1.19

- Coaching now checks the game’s version, commit and assembly identity before loading its card list and effect rules. An unreviewed update no longer inherits advice from another build.
- Main and public-beta Steam builds are monitored independently. The first audited catalog is public-beta v0.113.1, build 26660232.

## v0.1.18

- Added **Curse Mode**, an optional challenge at the top of Vakuu’s settings (F8). It’s off by default.
- A cursed hint recommends a weaker card reward or withholds advice with a snarky remark. The chance starts at 10% and rises by three percentage points per Ascension, up to 40% at A10. From A5, crooked counsel recommends the weakest available reward.
- Asking again doesn’t reroll the curse. Post-pick feedback and run reviews stay honest.

## v0.1.17

- Speech bubbles now grow to fit wrapped hints at your chosen text size, while keeping reward controls clear.
- When a comparison is too long to fit, scroll inside the bubble with the mouse wheel or trackpad. It stays open until you dismiss it or the decision changes.

## v0.1.16

- Vakuu now recognizes 20 build directions across six characters. Card reward advice names the enabler and payoff, so you can tell an emerging possibility from a working combo.
- Added a solo run review with deck strengths, damage and healing by act, potion use, and the most costly floor. Loss reviews separate the recorded cause of defeat from suggestions for the next run.
- Reopen your latest assessment from Auditor settings, or disable automatic reviews there.

## v0.1.15

- Initial public source tag for the offline solo coach on STS2 public-beta v0.113.1. Press V for help with card rewards, combat turns, potions, events, and map routes.
- Drag a speech bubble to save its position separately for each view. Click to dismiss it, or reset all positions in Auditor settings.
- Added quick installers and a private-by-default Steam Workshop staging package. The first downloadable GitHub release was v0.1.16.
