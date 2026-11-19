# Publish to Steam Workshop

1. Download **WorkshopWorkspace.zip** from the same GitHub release as the player ZIP (or run `sh tools/package.sh`). Extract it to a persistent folder. It contains `workshop.json`, `image.png`, `previews/`, and `content/VakuusAgenticAuditor/` with only the mod DLL and manifest.
2. Install [Mega Crit’s ModUploader](https://github.com/megacrit/sts2-mod-uploader/releases) for your OS and sign into Steam as the intended owner.
3. Keep the initial listing **private**. From the uploader folder run `./ModUploader upload -w "/path/to/workspace"` (Windows: `ModUploader.exe upload -w "C:\path\to\workspace"`). Review and accept any Steam Workshop terms in Steam.
4. Keep the generated **mod_id.txt** with this workspace. Future updates use that ID; unpacking an update into a fresh folder without it can create another listing.
5. Remove the duplicate local install, subscribe to the private item, restart and test on the target beta. Check picking/skipping, suggestions on V, dragging across screens, and the configuration menu. Set the Steam listing’s supported beta branch. When satisfied, set visibility to public and add its real subscription URL to the README.

Do not put Steam credentials or `mod_id.txt` in the public ZIP. The CI pipeline prepares the workspace; it does not upload it to Steam.
