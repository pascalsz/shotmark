# Prüft die Projektseite unter site/ (lokal und vor jedem Deploy in .github/workflows/pages.yml).
#   pwsh -File scripts/check-site.ps1            beide Sprachen
#   pwsh -File scripts/check-site.ps1 -Only en   nur eine Seite
# Exit-Code 0 und "Site OK (<n> checks)" bei Erfolg, sonst 1 und je Fehler eine Zeile "FAIL <seite>: <grund>".
param(
    [ValidateSet('en', 'de')][string]$Only,
    [string]$Root
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repo = Split-Path -Parent $PSScriptRoot
if (-not $Root) { $Root = Join-Path $repo 'site' }
$Root = [System.IO.Path]::GetFullPath($Root)

$SiteUrl = 'https://pascalsz.github.io/shotmark/'
$SetupUrl = 'https://github.com/pascalsz/shotmark/releases/latest/download/ShotMark.App-win-Setup.exe'
$AllowedLinkHosts = @('github.com', 'docs.github.com')
$Trademark = @{
    en = 'Snagit is a trademark of TechSmith Corporation. ShotMark is not affiliated with TechSmith.'
    de = 'Snagit ist eine Marke der TechSmith Corporation. ShotMark steht in keiner Verbindung zu TechSmith.'
}
$Pages = [ordered]@{ en = 'index.html'; de = 'de/index.html' }

$failures = [System.Collections.Generic.List[string]]::new()
$script:checks = 0
function Check([bool]$ok, [string]$page, [string]$message) {
    $script:checks++
    if (-not $ok) { $failures.Add("FAIL ${page}: $message") }
}
function Tags([string]$html, [string]$name) {
    [regex]::Matches($html, "<$name\b[^>]*>", 'IgnoreCase') | ForEach-Object Value
}
function Attr([string]$tag, [string]$name) {
    $m = [regex]::Match($tag, "\s$name\s*=\s*(?:""([^""]*)""|'([^']*)')", 'IgnoreCase')
    if (-not $m.Success) { return $null }
    if ($m.Groups[1].Success) { $m.Groups[1].Value } else { $m.Groups[2].Value }
}
function Text([string]$html) {
    $plain = [regex]::Replace($html, '<[^>]+>', ' ')
    [regex]::Replace([System.Net.WebUtility]::HtmlDecode($plain), '\s+', ' ').Trim()
}
function Inner([string]$html, [string]$name) {
    [regex]::Matches($html, "<$name\b[^>]*>(.*?)</$name>", 'IgnoreCase, Singleline') | ForEach-Object { Text $_.Groups[1].Value }
}
# Lokale Datei zu einer Referenz (relativ zur Seite oder absolut unter $SiteUrl); $null, wenn keine lokale Datei gemeint ist
function LocalPath([string]$pageFile, [string]$ref) {
    $clean = ($ref -split '[?#]')[0]
    if ($clean.StartsWith($SiteUrl)) { $full = Join-Path $Root $clean.Substring($SiteUrl.Length) }
    elseif ($clean -match '^[a-z][a-z0-9+.-]*:' -or $clean -eq '') { return $null }
    else { $full = Join-Path (Split-Path -Parent $pageFile) $clean }
    $full = [System.IO.Path]::GetFullPath($full)
    if ($clean.EndsWith('/') -or (Test-Path $full -PathType Container)) { $full = Join-Path $full 'index.html' }
    $full
}
# Existiert die Datei mit exakt dieser Schreibweise? GitHub Pages unterscheidet Groß/Klein, Windows nicht.
function ExistsExactCase([string]$full) {
    if (-not (Test-Path $full)) { return $false }
    $relative = [System.IO.Path]::GetRelativePath($Root, $full)
    if ($relative.StartsWith('..')) { return $true }
    $dir = $Root
    foreach ($part in $relative -split '[\\/]') {
        if (-not ((Get-ChildItem -LiteralPath $dir -Force | ForEach-Object Name) -ccontains $part)) { return $false }
        $dir = Join-Path $dir $part
    }
    $true
}

# Breite und Höhe einer PNG-Datei aus dem IHDR-Kopf (ohne System.Drawing, läuft auch auf Linux-Runnern)
function PngSize([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 24 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x4E -or $bytes[3] -ne 0x47) { return $null }
    $w = ([int]$bytes[16] -shl 24) -bor ([int]$bytes[17] -shl 16) -bor ([int]$bytes[18] -shl 8) -bor [int]$bytes[19]
    $h = ([int]$bytes[20] -shl 24) -bor ([int]$bytes[21] -shl 16) -bor ([int]$bytes[22] -shl 8) -bor [int]$bytes[23]
    "${w}x${h}"
}

$sectionIds = @{}
foreach ($lang in $Pages.Keys) {
    if ($Only -and $Only -ne $lang) { continue }
    $file = Join-Path $Root $Pages[$lang]
    if (-not (Test-Path $file)) { Check $false $lang "$($Pages[$lang]) fehlt"; continue }
    $html = Get-Content $file -Raw -Encoding UTF8

    # 1. Grundgerüst
    $htmlTag = (Tags $html 'html') | Select-Object -First 1
    Check ((Attr $htmlTag 'lang') -eq $lang) $lang "<html lang=""$lang""> fehlt"
    $metas = @(Tags $html 'meta')
    $links = @(Tags $html 'link')
    Check ([bool]($metas | Where-Object { (Attr $_ 'name') -eq 'viewport' })) $lang 'meta viewport fehlt'
    Check ((Inner $html 'title').Count -eq 1 -and (Inner $html 'title')[0]) $lang '<title> fehlt'
    Check ([bool]($metas | Where-Object { (Attr $_ 'name') -eq 'description' -and (Attr $_ 'content') })) $lang 'meta description fehlt'
    Check ([bool]($links | Where-Object { (Attr $_ 'rel') -eq 'canonical' })) $lang 'link canonical fehlt'

    # 2. hreflang
    foreach ($code in 'en', 'de', 'x-default') {
        Check ([bool]($links | Where-Object { (Attr $_ 'rel') -eq 'alternate' -and (Attr $_ 'hreflang') -eq $code })) $lang "hreflang $code fehlt"
    }

    # 3. Open Graph
    foreach ($prop in 'og:title', 'og:description', 'og:image') {
        Check ([bool]($metas | Where-Object { (Attr $_ 'property') -eq $prop -and (Attr $_ 'content') })) $lang "$prop fehlt"
    }

    # 4. Download-Link
    $anchors = @(Tags $html 'a')
    Check ([bool]($anchors | Where-Object { (Attr $_ 'href') -eq $SetupUrl })) $lang 'Download-Link auf die neueste Setup.exe fehlt'

    # 5. Bilder
    foreach ($img in Tags $html 'img') {
        $src = Attr $img 'src'
        Check ([bool](Attr $img 'alt')) $lang "img ohne alt: $src"
        Check ([bool](Attr $img 'width') -and [bool](Attr $img 'height')) $lang "img ohne width/height: $src"
    }

    # 6.–8. Referenzen
    $refs = @()
    foreach ($tag in @(Tags $html 'a') + @(Tags $html 'link') + @(Tags $html 'script') + @(Tags $html 'img') + @(Tags $html 'source')) {
        $name = ([regex]::Match($tag, '^<(\w+)')).Groups[1].Value.ToLowerInvariant()
        foreach ($attrName in 'href', 'src', 'srcset') {
            $value = Attr $tag $attrName
            if (-not $value) { continue }
            foreach ($ref in ($attrName -eq 'srcset' ? ($value -split ',' | ForEach-Object { ($_.Trim() -split '\s+')[0] }) : @($value))) {
                $refs += [pscustomobject]@{ Tag = $name; Rel = (Attr $tag 'rel'); Ref = $ref }
            }
        }
    }
    foreach ($meta in $metas | Where-Object { (Attr $_ 'property') -eq 'og:image' }) {
        $refs += [pscustomobject]@{ Tag = 'meta'; Rel = 'og:image'; Ref = (Attr $meta 'content') }
    }
    foreach ($r in $refs) {
        Check (-not $r.Ref.StartsWith('/')) $lang "root-relativer Pfad bricht unter /shotmark/: $($r.Ref)"
        $absoluteAllowed = ($r.Tag -eq 'link' -and $r.Rel -in 'canonical', 'alternate') -or $r.Tag -eq 'meta'
        if ($r.Ref -match '^https?://') {
            if ($absoluteAllowed) {
                Check ($r.Ref.StartsWith($SiteUrl)) $lang "$($r.Rel) muss mit $SiteUrl beginnen: $($r.Ref)"
            }
            elseif ($r.Tag -eq 'a') {
                $linkHost = ([uri]$r.Ref).Host
                Check ($r.Ref.StartsWith('https://') -and $linkHost -in $AllowedLinkHosts) $lang "externer Link nicht erlaubt: $($r.Ref)"
            }
            else {
                Check $false $lang "lädt Ressource von außen: $($r.Ref)"
            }
        }
        $local = LocalPath $file $r.Ref
        # Mit -Only darf die andere Sprachseite noch fehlen
        $otherPage = $Only -and ($r.Rel -eq 'alternate' -or $r.Tag -eq 'a') -and $local -and $local -ne [System.IO.Path]::GetFullPath($file) -and $local.EndsWith('index.html')
        if ($local -and -not $r.Ref.StartsWith('/') -and -not $otherPage) { Check (ExistsExactCase $local) $lang "Datei fehlt (oder falsche Groß-/Kleinschreibung): $($r.Ref)" }
        $other = $lang -eq 'en' ? 'de' : 'en'
        Check ($r.Ref -notmatch "assets/$other/") $lang "Bild der anderen Sprache: $($r.Ref)"
    }

    # 9. Snagit nur als Einordnung, Markenhinweis wörtlich
    foreach ($el in 'title', 'h1', 'h2', 'h3') {
        foreach ($t in Inner $html $el) { Check ($t -notmatch 'Snagit') $lang "Snagit in <$el>: $t" }
    }
    Check ((Text $html).Contains($Trademark[$lang])) $lang 'Markenhinweis fehlt oder weicht ab'

    # Vorschaubild für soziale Netzwerke: eigenes og.png der Sprache in 1200×630
    $ogImage = ($metas | Where-Object { (Attr $_ 'property') -eq 'og:image' } | ForEach-Object { Attr $_ 'content' } | Select-Object -First 1)
    Check ($ogImage -eq "${SiteUrl}assets/$lang/og.png") $lang "og:image soll ${SiteUrl}assets/$lang/og.png sein: $ogImage"
    $ogFile = Join-Path $Root "assets/$lang/og.png"
    Check ((Test-Path $ogFile) -and (PngSize $ogFile) -eq '1200x630') $lang "assets/$lang/og.png fehlt oder ist nicht 1200×630"

    $sectionIds[$lang] = @(Tags $html 'section' | ForEach-Object { Attr $_ 'id' } | Where-Object { $_ } | Sort-Object)
}

# 10. Gleiche Abschnitte in beiden Sprachen (Sprachumschalter führt an dieselbe Stelle)
if (-not $Only -and $sectionIds.Count -eq 2) {
    $diff = Compare-Object $sectionIds['en'] $sectionIds['de']
    Check (-not $diff) 'en/de' ("Abschnitt-IDs unterscheiden sich: " + (($diff | ForEach-Object { "$($_.InputObject)$($_.SideIndicator)" }) -join ', '))
}

# Begleitdateien
if (-not $Only) {
    foreach ($name in '404.html', 'robots.txt', 'sitemap.xml') {
        Check (Test-Path (Join-Path $Root $name)) 'site' "$name fehlt"
    }
    $sitemap = Join-Path $Root 'sitemap.xml'
    if (Test-Path $sitemap) {
        $xml = Get-Content $sitemap -Raw -Encoding UTF8
        foreach ($url in $SiteUrl, "${SiteUrl}de/") { Check ($xml.Contains("<loc>$url</loc>")) 'sitemap' "URL fehlt: $url" }
    }
}

# 11. Einblend-Effekte verstecken nichts ohne JavaScript
$css = Join-Path $Root 'assets/style.css'
if (Test-Path $css) {
    $content = [regex]::Replace((Get-Content $css -Raw -Encoding UTF8), '/\*.*?\*/', '', 'Singleline')
    foreach ($rule in [regex]::Matches($content, '([^{}]+)\{([^{}]*)\}')) {
        $selector = $rule.Groups[1].Value.Trim()
        $body = $rule.Groups[2].Value
        if ($body -match 'opacity\s*:\s*0(?![.\d])|visibility\s*:\s*hidden') {
            foreach ($part in $selector -split ',') {
                $s = $part.Trim()
                Check ($s -match '^(html)?\.js\s' -or $s -match '^@') 'css' "versteckt ohne JS: $s"
            }
        }
    }
}

if ($failures.Count) {
    $failures | ForEach-Object { Write-Output $_ }
    exit 1
}
Write-Output "Site OK ($($script:checks) checks)"
