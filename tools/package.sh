#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
dotnet build mod -c Release "$@"
dotnet run --project tests -c Release
id=VakuusAgenticAuditor
mkdir -p "dist/$id"
cp "mod/bin/Release/net9.0/$id.dll" "mod/$id.json" "dist/$id/"
cp tools/Install.command tools/Install.ps1 dist/
chmod +x dist/Install.command
(cd dist && python3 ../tools/make_zip.py "$id.zip" "$id/$id.dll" "$id/$id.json" Install.command Install.ps1)
sh tools/workshop.sh
