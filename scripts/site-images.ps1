# Erzeugt die App-Bilder der Projektseite (site/assets/en, site/assets/de) aus den UI-Snapshots.
#   pwsh -File scripts/site-images.ps1            (doppelte Auflösung, für scharfe Bilder auf HiDPI-Bildschirmen)
#   pwsh -File scripts/site-images.ps1 -Scale 1
#   pwsh -File scripts/site-images.ps1 -OgOnly   (nur die Vorschaubilder og.png aus den vorhandenen hero.png neu zeichnen)
param([double]$Scale = 2, [switch]$OgOnly)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$snapshots = Join-Path ([System.IO.Path]::GetTempPath()) "shotmark-site-snapshots"
if (-not $OgOnly) {
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
# Vorschaubild für soziale Netzwerke (1200×630): Überschrift mit rotem Rahmen wie auf der Seite, daneben der Editor
Add-Type -AssemblyName System.Drawing
$og = @{
    en = @{ Before = 'Show what '; Marked = 'matters'; After = '.'; Sub = 'Free screenshot and markup tool for Windows' }
    de = @{ Before = 'Zeig, was '; Marked = 'zählt'; After = '.'; Sub = 'Kostenloses Screenshot-Werkzeug für Windows' }
}
function New-OgImage([string]$lang, [string]$heroPath, [string]$outPath) {
    $t = $og[$lang]
    $bmp = [System.Drawing.Bitmap]::new(1200, 630)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'; $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#1B1B1D'))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#ECECEE'))
    $dim = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#B4B4BB'))
    $red = [System.Drawing.ColorTranslator]::FromHtml('#FC4242')

    # Editor rechts unten, angeschnitten
    $hero = [System.Drawing.Image]::FromFile($heroPath)
    $heroHeight = [int](820 * $hero.Height / $hero.Width)
    $g.DrawImage($hero, [System.Drawing.Rectangle]::new(600, 340, 820, $heroHeight))
    $hero.Dispose()
    $g.DrawRectangle([System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#3A3A40'), 2), 600, 340, 820, $heroHeight)

    # Marke
    $g.FillRectangle([System.Drawing.SolidBrush]::new($red), 72, 64, 40, 40)
    $corner = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 3)
    foreach ($p in @(@(82,80,82,74,88,74), @(96,74,102,74,102,80), @(102,88,102,94,96,94), @(88,94,82,94,82,88))) {
        $g.DrawLines($corner, [System.Drawing.Point[]]@([System.Drawing.Point]::new($p[0],$p[1]), [System.Drawing.Point]::new($p[2],$p[3]), [System.Drawing.Point]::new($p[4],$p[5])))
    }
    $g.DrawString('ShotMark', [System.Drawing.Font]::new('Segoe UI Variable Display Semib', 26), $white, 124, 64)

    # Überschrift mit Rahmen um das markierte Wort
    $font = [System.Drawing.Font]::new('Segoe UI Variable Display', 64, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $format = [System.Drawing.StringFormat]::new([System.Drawing.StringFormat]::GenericTypographic)
    $format.FormatFlags = $format.FormatFlags -bor [System.Drawing.StringFormatFlags]::MeasureTrailingSpaces
    $x = 72; $y = 150
    $g.DrawString($t.Before, $font, $white, $x, $y, $format)
    $x += $g.MeasureString($t.Before, $font, [System.Drawing.PointF]::Empty, $format).Width
    $markedWidth = $g.MeasureString($t.Marked, $font, [System.Drawing.PointF]::Empty, $format).Width
    $g.DrawString($t.Marked, $font, $white, $x, $y, $format)
    $pen = [System.Drawing.Pen]::new($red, 5)
    $pen.LineJoin = 'Miter'
    $g.DrawRectangle($pen, [int]($x - 8), [int]($y - 4), [int]($markedWidth + 16), 84)
    $g.DrawString($t.After, $font, $white, $x + $markedWidth + 10, $y, $format)

    $g.DrawString($t.Sub, [System.Drawing.Font]::new('Segoe UI Variable Text', 26, [System.Drawing.GraphicsUnit]::Pixel), $dim, 72, 262)
    $g.Dispose()
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

foreach ($lang in 'en', 'de') {
    $target = Join-Path $repo "site/assets/$lang"
    New-Item -ItemType Directory -Force $target | Out-Null
    if (-not $OgOnly) {
        foreach ($entry in $images.GetEnumerator()) {
            Copy-Item (Join-Path $snapshots "$lang/$($entry.Key)") (Join-Path $target $entry.Value) -Force
        }
    }
    New-OgImage $lang (Join-Path $target 'hero.png') (Join-Path $target 'og.png')
    Get-ChildItem $target -Filter *.png | ForEach-Object { '{0,8:N0} KB  {1}/{2}' -f ($_.Length / 1KB), $lang, $_.Name }
}
