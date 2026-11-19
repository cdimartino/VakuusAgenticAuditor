param([string]$GameDir = "${env:ProgramFiles(x86)}\Steam\steamapps\common\Slay the Spire 2")
$ErrorActionPreference = "Stop"
$id = "VakuusAgenticAuditor"
if (-not (Test-Path (Join-Path $GameDir "data_sts2_windows_x86_64"))) {
    throw "Game not found at $GameDir. Pass -GameDir with the folder opened by Steam > Manage > Browse local files."
}
$src = Join-Path $PSScriptRoot $id
$dst = Join-Path (Join-Path $GameDir "mods") $id
foreach ($ext in "dll", "json") {
    if (-not (Test-Path -LiteralPath (Join-Path $src "$id.$ext") -PathType Leaf)) { throw "Extract the entire release ZIP first." }
}
New-Item -ItemType Directory -Force -Path $dst | Out-Null
foreach ($ext in "dll", "json") { Copy-Item -LiteralPath (Join-Path $src "$id.$ext") -Destination $dst -Force }
Write-Output "Installed in $dst. Restart Slay the Spire 2 and enable the Auditor in Mods."
