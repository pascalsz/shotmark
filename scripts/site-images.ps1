# Erzeugt die App-Bilder der Projektseite (site/assets/en, site/assets/de) aus den UI-Snapshots.
#   pwsh -File scripts/site-images.ps1            (doppelte Auflösung, für scharfe Bilder auf HiDPI-Bildschirmen)
#   pwsh -File scripts/site-images.ps1 -Scale 1
param([double]$Scale = 2)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$snapshots = Join-Path ([System.IO.Path]::GetTempPath()) "shotmark-site-snapshots"
if (Test-Path $snapshots) { Remove-Item $snapshots -Recurse -Force }

$env:SHOTMARK_SNAPSHOT_DIR = $snapshots
$env:SHOTMARK_SNAPSHOT_SCALE = $Scale.ToString([System.Globalization.CultureInfo]::InvariantCulture)
try {
    dotnet test (Join-Path $repo 'tests/ShotMark.App.Tests') --filter UiSnapshots --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Die UI-Snapshots konnten nicht erzeugt werden.' }
}
finally {
    Remove-Item Env:SHOTMARK_SNAPSHOT_DIR, Env:SHOTMARK_SNAPSHOT_SCALE -ErrorAction SilentlyContinue
}

# Snapshot-Name -> Name auf der Seite
$images = [ordered]@{
    '01-editor.png'               = 'hero.png'
    '12-overlay-drag.png'         = 'capture.png'
    '13-overlay-scroll.png'       = 'scroll.png'
    '15-scroll-panel-manual.png'  = 'scroll-panel.png'
    '05-editor-filter-laptop.png' = 'history.png'
    '07-editor-conflict.png'      = 'conflict.png'
}
foreach ($lang in 'en', 'de') {
    $target = Join-Path $repo "site/assets/$lang"
    New-Item -ItemType Directory -Force $target | Out-Null
    foreach ($entry in $images.GetEnumerator()) {
        Copy-Item (Join-Path $snapshots "$lang/$($entry.Key)") (Join-Path $target $entry.Value) -Force
    }
    Get-ChildItem $target -Filter *.png | ForEach-Object { '{0,8:N0} KB  {1}/{2}' -f ($_.Length / 1KB), $lang, $_.Name }
}
