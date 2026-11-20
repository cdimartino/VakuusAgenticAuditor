# Bridge patch provenance

`bridge-beta.patch` is a focused local patch to STS2 MCP, the untracked local source
tree `/Users/chris/Source/sts2-ai`. Its README identifies the upstream project as
https://github.com/Gennadiyev/STS2MCP . No upstream commit identity is available for
this local checkout; do not claim the patch applies to every upstream release.
The exact original edited files (Actions, StateBuilder and SettingsUI) are backed up outside public tooling at ignored
`dist/bridge-originals/`. The patch includes only the changed lines without surrounding context.
Reproduction requires obtaining the matching upstream source and owned game DLLs;
no game assemblies or complete third-party source files are bundled here.

The SettingsUI fix addresses a pre-existing beta API mismatch observed in authorized
smoke logs: HoverTip.Id has no setter. Owned beta metadata confirms the constructor
HoverTip(LocString title, string description, Texture2D icon) and non-public Title
setter; NFastModeHoverTip._Ready IL uses `settings_ui` / `FASTMODE` for its title.
The patch invokes that constructor with the custom description, then uses the
verified Title setter. It never assigns Id. This compiled against owned beta DLLs;
corrected live startup/hover behavior has not been tested by this task. Rebuilt DLL,
manifest and hashes are in ignored dist/playtest-bridge/.

The local upstream LICENSE contains the following MIT license:

Copyright 2026 Yikun Ji (Kunologist)

Permission is hereby granted, free of charge, to any person obtaining a copy of this
software and associated documentation files (the “Software”), to deal in the Software
without restriction, including without limitation the rights to use, copy, modify,
merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be included in all copies
or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
