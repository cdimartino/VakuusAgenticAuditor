#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
id=VakuusAgenticAuditor
out=dist/workshop
mkdir -p "$out/content/$id" "$out/previews"
cp "dist/$id/$id.dll" "dist/$id/$id.json" "$out/content/$id/"
cp workshop/image.png workshop/workshop.json "$out/"
cp docs/images/event.jpg docs/images/potion-rewards.jpg "$out/previews/"
# Explicit allowlist: no local uploader state or Steam item ID in public assets.
(cd "$out" && python3 ../../tools/make_zip.py ../WorkshopWorkspace.zip workshop.json image.png previews/event.jpg previews/potion-rewards.jpg "content/$id/$id.dll" "content/$id/$id.json")
