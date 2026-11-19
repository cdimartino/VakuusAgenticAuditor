"""Check the distributable, not just the build output."""
import json
import os
from zipfile import ZipFile
from pathlib import Path

name = "VakuusAgenticAuditor"
with ZipFile(f"dist/{name}.zip") as release:
    expected = {f"{name}/{name}.dll", f"{name}/{name}.json", "Install.command", "Install.ps1"}
    if set(release.namelist()) != expected or release.testzip():
        raise SystemExit("Unexpected or corrupt release payload")
    manifest = json.loads(release.read(f"{name}/{name}.json"))
    if manifest["id"] != name or manifest["affects_gameplay"] or manifest["has_pck"]:
        raise SystemExit("Unexpected manifest")
    if os.getenv("GITHUB_REF_TYPE") == "tag" and manifest["version"] != os.getenv("GITHUB_REF_NAME"):
        raise SystemExit("Tag must equal manifest version")
    for suffix in ("dll", "json"):
        path = f"{name}/{name}.{suffix}"
        if release.read(path) != (Path("dist") / path).read_bytes():
            raise SystemExit("Staged Workshop payload differs from release")
with ZipFile("dist/WorkshopWorkspace.zip") as workspace:
    if json.loads(workspace.read("workshop.json"))["visibility"] != "private":
        raise SystemExit("Distribution template must default to private")
    for img in ("image.png", "previews/event.jpg", "previews/potion-rewards.jpg"):
        if not 0 < workspace.getinfo(img).file_size < 1_000_000:
            raise SystemExit("Workshop preview exceeds uploader limits")
    for ext in ("dll", "json"):
        path = f"{name}/{name}.{ext}"
        if workspace.read(f"content/{path}") != (Path("dist") / path).read_bytes():
            raise SystemExit("Workshop build differs from player download")
