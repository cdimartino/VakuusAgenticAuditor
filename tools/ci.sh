#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
pkg=book.sts2.reflib
ver=0.113.1-beta
curl --fail --silent --show-error --location --retry 3 "https://api.nuget.org/v3-flatcontainer/$pkg/$ver/$pkg.$ver.nupkg" -o "$tmp/ref.zip"
echo "044dd3e576a576c56b63b1ec7261c86517321903907cb18e4531af5db571de81  $tmp/ref.zip" | shasum -a 256 -c -
unzip -q "$tmp/ref.zip" 'ref/net9.0/*' -d "$tmp"
export GameData="$tmp/ref/net9.0"
dotnet format whitespace mod/VakuusAgenticAuditor.csproj --verify-no-changes
dotnet format whitespace tests/Scenarios.csproj --verify-no-changes
for s in tools/*.sh tools/Install.command; do sh -n "$s"; done
python3 -m compileall -q tools
python3 tools/audit_card_coverage.py
python3 tools/audit_relic_guide.py
sh tools/package.sh -warnaserror -t:Rebuild
python3 tools/release_check.py
mkdir -p "$tmp/library/SlayTheSpire2.app/Contents/MacOS"
unzip -q dist/VakuusAgenticAuditor.zip -d "$tmp/release"
for attempt in 1 2; do "$tmp/release/Install.command" "$tmp/library"; done
cmp dist/VakuusAgenticAuditor/VakuusAgenticAuditor.dll "$tmp/library/SlayTheSpire2.app/Contents/MacOS/mods/VakuusAgenticAuditor/VakuusAgenticAuditor.dll"
