#!/bin/sh
set -eu
cd "$(dirname "$0")"
id=VakuusAgenticAuditor
case "$(uname -s)" in
  Darwin) default="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2";;
  Linux) default="$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2";;
  *) echo "Use Install.ps1 on Windows."; exit 1;;
esac
game=${1:-$default}
if [ -d "$game/SlayTheSpire2.app/Contents/MacOS" ]; then
  target="$game/SlayTheSpire2.app/Contents/MacOS/mods"
elif [ -d "$game/data_sts2_linuxbsd_x86_64" ]; then
  target="$game/mods"
else
  echo "Game not found at: $game"
  echo 'Run this installer again with the folder opened by Steam → Manage → Browse local files as its first argument.'
  exit 1
fi
test -r "$id/$id.dll" && test -r "$id/$id.json" || { echo 'Extract the whole ZIP before running the installer.'; exit 1; }
mkdir -p "$target/$id"
for ext in dll json; do
  cp "$id/$id.$ext" "$target/$id/.$id.$ext.new"
  mv "$target/$id/.$id.$ext.new" "$target/$id/$id.$ext"
done
echo "Installed in $target/$id. Restart Slay the Spire 2 and enable the Auditor in Mods."
