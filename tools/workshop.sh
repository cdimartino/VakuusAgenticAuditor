#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
id=VakuusAgenticAuditor
out=dist/workshop
mkdir -p "$out/content/$id" "$out/previews"
cp "dist/$id/$id.dll" "dist/$id/$id.json" "$out/content/$id/"
cp workshop/image.png workshop/workshop.json "$out/"
python3 - "$out/workshop.json" <<'PY'
import json, sys
from pathlib import Path
sys.path.insert(0, "tools")
from release_notes import notes
path = Path(sys.argv[1])
template = json.loads(path.read_text())
version = json.loads(Path("mod/VakuusAgenticAuditor.json").read_text())["version"]
body = notes(version)
template["changeNote"] = version + "\n" + body.replace("**", "")
path.write_text(json.dumps(template, ensure_ascii=False, indent=2) + "\n")
Path("dist/release-notes.md").write_text(body + "\n")
PY
cp docs/images/event.jpg docs/images/potion-rewards.jpg "$out/previews/"
# Explicit allowlist: no local uploader state or Steam item ID in public assets.
(cd "$out" && python3 ../../tools/make_zip.py ../WorkshopWorkspace.zip workshop.json image.png previews/event.jpg previews/potion-rewards.jpg "content/$id/$id.dll" "content/$id/$id.json")
