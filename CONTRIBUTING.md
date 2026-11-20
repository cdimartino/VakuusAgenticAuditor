# Build, test, release

Before changing the manifest version, add a matching `## vX.Y.Z` section to [CHANGELOG.md](CHANGELOG.md), newest first. Write bullets describing what players will notice and how to use it. Packaging requires exactly one matching entry and uses it for both the GitHub Release and Steam Workshop change note. For Nexus Mods, paste the same entry into the version changelog: `python3 tools/release_notes.py vX.Y.Z`.

Use .NET SDK 9 and the target game beta (v0.113.1). Run `sh tools/package.sh` on a standard Apple Silicon Steam install. Elsewhere, pass `-p:GameData="/path/to/directory/containing/sts2.dll"`. Output: the player ZIP and `WorkshopWorkspace.zip` under `dist/`.

`dotnet run --project tests` runs the offline coaching, transition and placement scenarios. `sh tools/ci.sh` is the portable Linux/macOS pre-release check: it fetches checksum-pinned [compile-only game references](https://www.nuget.org/packages/Book.StS2.RefLib/0.113.1-beta), checks C# formatting against `.editorconfig`, treats build warnings as errors, runs scenarios, validates the ZIPs, and smoke-tests a fresh install and update in a temporary library. Requirements: .NET 9, Python 3, `curl`, `shasum`, `unzip`, `zip`. No game files are released.

Public pull requests use GitHub-hosted CI. Trusted main/tag pushes and manual runs use the disposable Linux homelab lane when `AUDITOR_CI_POOL=homelab`; unset the variable to fall back to hosted. A `v*` tag matching `mod/VakuusAgenticAuditor.json` publishes the verified ZIPs and checksums as a GitHub Release. Publishing to Steam is a separate manual step.

Before tagging, test a real run: drag a bubble on the map, use a different spot in combat, click to dismiss, press V to recall, change resolution and restart. Confirm the two spots survive, reward choices still receive their clicks, and **Reset bubble positions** restores defaults.

For an instrumented agent test, follow [PLAYTESTING.md](docs/PLAYTESTING.md). Game-state journals are local-only; do not commit player histories or account paths. Run the Python runner regressions with `python3 -m unittest discover -s tools/playtest -p 'test_*.py' -v`. The optional patched MCP binary is separate from the Workshop mod and must be installed only with the game safely closed.

Community conventions used here: [BaseLib](https://github.com/Alchyr/BaseLib-StS2/releases) ships versioned downloads; [Minty Spire 2](https://github.com/erasels/Minty-Spire-2) documents manual installation and makes intrusive UI optional. Workshop staging follows [Mega Crit’s official uploader](https://github.com/megacrit/sts2-mod-uploader). None of these mods is a runtime dependency.
