# ShotMark-Projektseite – Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Zweisprachige, statische Projektseite (Englisch unter `/`, Deutsch unter `/de/`) im Stil „C dunkel“. Sie wird per GitHub Pages veröffentlicht.

**Architecture:** Handgeschriebenes HTML/CSS mit sehr wenig JS unter `site/`. Die App-Bilder rendert `UiSnapshots` und `scripts/site-images.ps1` kopiert sie nach `site/assets/{en,de}/`. Ein Prüfskript `scripts/check-site.ps1` ist der Test für die Seite und läuft lokal und vor jedem Deploy in `pages.yml`.

**Tech Stack:** HTML5, CSS (Custom Properties, Grid/Flex), Vanilla-JS (optional), PowerShell 7 (`pwsh`), GitHub Actions (Pages).

**Spec:** `docs/superpowers/specs/2026-11-17-project-website-design.md`

## Global Constraints

- **URLs:** Englisch unter `site/index.html`, Deutsch unter `site/de/index.html`. Live-Adresse ist `https://pascalsz.github.io/shotmark/`. Alle internen Links sind relativ, root-relative Pfade (`href="/…"`) gibt es nicht.
- **Downloads:**
  - Download-Knopf: `https://github.com/pascalsz/shotmark/releases/latest/download/ShotMark.App-win-Setup.exe`
  - Portable: `https://github.com/pascalsz/shotmark/releases/latest/download/ShotMark.App-win-Portable.zip`
  - Alle Releases: `https://github.com/pascalsz/shotmark/releases`
  - Quellcode: `https://github.com/pascalsz/shotmark`
- **Keine externen Ressourcen:** keine Webfonts, keine CDNs, keine Skripte oder Bilder von Dritten, keine Cookies und keine Statistik. Externe `<a>`-Links gehen nur auf `github.com` und `docs.github.com`.
- **Farben** wie in der App:
  - Fläche `#1B1B1D`, Panels `#232326`/`#2A2A2E`, Linien `#3A3A40`, Text `#ECECEE`/`#A4A4AC`
  - Markierungsrot `#FC4242`, Haken-Grün `#3DBE5C`
  - Blau `#2D8CFF` nur als Akzent; Knopf-Hintergrund dunkler (um `#1F6FE0`), sodass weißer Text ≥ 4,5 : 1 hat.
- **Schrift:** `"Segoe UI Variable Text", "Segoe UI", system-ui, -apple-system, sans-serif`. Eine Display-Variante für Überschriften nur über `"Segoe UI Variable Display"` aus demselben Stack.
- **Markierungen** (Merkmal der Seite) übernehmen die Formen der App:
  - Rahmen: eckig (Miter), Strich 4 px
  - Pfeil: gefüllter Schaft mit Spitze
  - Haken: Polylinie (0.14, 0.54) → (0.40, 0.80) → (0.88, 0.20) im Kasten, Strich = 0.14 × Kastenmaß, rund
  - Kreuz: (0.2, 0.2)–(0.8, 0.8) und (0.8, 0.2)–(0.2, 0.8), Strich = 0.13 × Kastenmaß, rund
  - Schatten in drei Lagen: (1, 2, α 40/255), (2, 3, α 30/255), (3, 4, α 20/255)
- **Snagit** kommt nur im Abschnitt „Warum ShotMark?“ als sachliche Einordnung vor, dazu der Markenhinweis in der Fußzeile, wörtlich:
  - EN: „Snagit is a trademark of TechSmith Corporation. ShotMark is not affiliated with TechSmith.“
  - DE: „Snagit ist eine Marke der TechSmith Corporation. ShotMark steht in keiner Verbindung zu TechSmith.“
  - Nicht in `<title>`, `<h1>`–`<h3>` und nicht in URLs.
- **Sprachen:** Deutsch per „du“. Die englische Seite zeigt nur Bilder aus `assets/en/`, die deutsche nur aus `assets/de/`.
- **Voraussetzungen** im Text: „Windows 10 und 11, 64-Bit · keine Adminrechte · kein .NET nötig“ (EN sinngemäß).
- **Pfade:** Das Repo ist der öffentliche Remote `public` (`pascalsz/shotmark`). Der Branch `feat/website` baut auf `feat/localization` auf.
- **Pages-Actions:**
  - `actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1` (v7.0.1)
  - `actions/configure-pages@45bfe0192ca1faeb007ade9deae92b16b8254a0d` (v6.0.0)
  - `actions/upload-pages-artifact@fc324d3547104276b827a68afc52ff2a11cc49c9` (v5.0.0)
  - `actions/deploy-pages@368f82528645a54fb793d4d04e342629a3f51346` (v5.0.1)
- **Frontend-Regel des Nutzers:** Nach jeder größeren Änderung die Seite im Browser bei Desktop- (1440 px) und Handybreite (375 px) prüfen und Fehler selbst beheben. Vor Abschluss laufen `/impeccable audit` und eine Prüfung nach Web-Design-Richtlinien. Für die Gestaltung wird der Skill `impeccable` geladen.

## Review Focus

1. **Handy mit 360–375 px Breite:** kein horizontales Scrollen, Überschrift und Markierungs-Deko bleiben lesbar, Knöpfe sind mindestens 44 px hoch. Die Sichtprüfung bei 375 px steht in Task 3 und 4; `check-site.ps1` prüft zusätzlich `<meta name="viewport">`.
2. **Ohne JavaScript oder mit `prefers-reduced-motion`:** Alles ist sichtbar, nichts startet versteckt. `check-site.ps1` prüft, dass Einblend-Klassen nur unter `html.js` greifen (Task 2). Die Sichtprüfung ohne JS steht in Task 7.
3. **Veröffentlichung unter dem Unterpfad `/shotmark/`:** Kein Link und kein Bild bricht. `check-site.ps1` meldet root-relative `href`/`src` und fehlende lokale Dateien (Task 2).
4. **Tastatur:** Sprachumschalter, Navigation und Download sind per Tab erreichbar und haben einen sichtbaren Fokus. Die Prüfung steht in Task 7 (Audit).
5. **Sprachumschalter:** Er führt auf dieselbe Stelle der anderen Sprache, die Abschnitt-IDs sind in beiden Seiten gleich. `check-site.ps1` vergleicht die IDs (Task 2).

---

### Task 1: Schärfere Snapshots und Bilder für die Seite

**Files:**
- Modify: `tests/ShotMark.App.Tests/UiSnapshots.cs` (Renderaufrufe in `SaveOnBackdrop`, `Save`, `SaveElement`, nicht in `SamplePage`)
- Create: `tests/ShotMark.App.Tests/SnapshotScaleTests.cs`
- Create: `scripts/site-images.ps1`
- Create: `site/assets/en/*.png`, `site/assets/de/*.png` (erzeugt)

**Interfaces:**
- Produces:
  - `internal static class SnapshotScale { public static double Parse(string? value); }` in `UiSnapshots.cs` oder einer eigenen Datei im Testprojekt. Ungültig/leer ergibt 1, sonst wird auf [1, 4] begrenzt, die Zahl wird invariant gelesen.
  - `UiSnapshots` liest `SHOTMARK_SNAPSHOT_SCALE` und rendert mit `RenderTargetBitmap(w*s, h*s, 96*s, 96*s, …)`.
  - `scripts/site-images.ps1`:
    1. setzt `SHOTMARK_SNAPSHOT_DIR` auf einen temporären Ordner und `SHOTMARK_SNAPSHOT_SCALE=2`
    2. ruft `dotnet test tests/ShotMark.App.Tests --filter UiSnapshots` auf
    3. kopiert je Sprache `01-editor.png → hero.png`, `12-overlay-drag.png → capture.png`, `13-overlay-scroll.png → scroll.png`, `15-scroll-panel-manual.png → scroll-panel.png`, `05-editor-filter-laptop.png → history.png`, `07-editor-conflict.png → conflict.png` nach `site/assets/<lang>/`
    4. gibt die Dateigrößen aus

- [ ] **Step 1: Failing test** `SnapshotScaleTests`:
  - `Parse(null)` == 1, `Parse("")` == 1, `Parse("abc")` == 1
  - `Parse("2")` == 2, `Parse("1.5")` == 1.5 (auch unter `CultureScope("de-DE")`)
  - `Parse("10")` == 4, `Parse("0.5")` == 1
- [ ] **Step 2:** `dotnet test tests/ShotMark.App.Tests --filter SnapshotScale` → FAIL (Typ fehlt).
- [ ] **Step 3:** `SnapshotScale` umsetzen und in die drei Renderaufrufe einbauen.
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS. Danach `pwsh -File scripts/site-images.ps1` ausführen: Es entstehen 6 Dateien je Sprache. `hero.png` ist 2608 px breit (1304 × 2).
- [ ] **Step 5:** `hero.png` beider Sprachen ansehen: scharf, richtige Sprache.
- [ ] **Step 6:** Commit `feat(site): 2x UI snapshots and site image script`.

### Task 2: Prüfskript für die Seite (der Test)

**Files:**
- Create: `scripts/check-site.ps1`

**Interfaces:**
- Produces: `pwsh -File scripts/check-site.ps1 [-Only en|de]`. Bei Erfolg Exit-Code 0 und Ausgabe `Site OK (<n> checks)`, sonst Exit-Code 1 und je Fehler eine Zeile `FAIL <page>: <reason>`. Es arbeitet mit Regex bzw. einfachem HTML-Parsing, ohne externe Module.
- Prüfungen je Seite (`site/index.html` = en, `site/de/index.html` = de):
  1. `<html lang="en">` bzw. `lang="de"`, dazu `<meta name="viewport">`, `<title>`, `<meta name="description">`, `<link rel="canonical">`
  2. `hreflang` für `en`, `de` und `x-default`
  3. `og:title`, `og:description`, `og:image`
  4. Der Download-Link hat exakt die Setup-URL aus Global Constraints.
  5. Jedes `<img>` hat ein nicht leeres `alt` sowie `width` und `height`.
  6. Kein `src`/`href` beginnt mit `/` (auch nicht `//`). Jede lokale Referenz existiert als Datei relativ zur Seite.
  7. Nur `<a href="https://…">` darf extern sein, und nur auf `github.com` oder `docs.github.com`. `<link>`, `<script>` und `<img>` laden nie von außen. Ausnahme: `link[rel=canonical]`, `link[rel=alternate]` und `meta[property=og:image]` müssen absolut sein und mit `https://pascalsz.github.io/shotmark/` beginnen.
  8. Die en-Seite referenziert keine Datei unter `assets/de/`, die de-Seite keine unter `assets/en/`.
  9. „Snagit“ steht nicht in `<title>`, `<h1>`, `<h2>` oder `<h3>`. Der Markenhinweis aus Global Constraints steht wörtlich in der jeweiligen Sprache darin.
  10. Ohne `-Only` gilt zusätzlich: Die Menge der `id`-Attribute an `<section>` ist in beiden Seiten gleich.
  11. In `site/assets/style.css` stehen Regeln mit `opacity: 0` oder `visibility: hidden` für Einblend-Effekte nur in Selektoren, die mit `.js ` beginnen.

- [ ] **Step 1:** Skript schreiben.
- [ ] **Step 2:** `pwsh -File scripts/check-site.ps1` → Exit 1 mit `FAIL en: site/index.html fehlt` (die Seite existiert noch nicht).
- [ ] **Step 3:** Selbsttest mit einer Wegwerfseite im Scratch-Ordner (Parameter `-Root <Ordner>`): Eine Seite mit root-relativem Bild und Google-Fonts-Link ergibt genau diese zwei FAIL-Zeilen. Den Parameter `-Root` dafür vorsehen, Standard ist `site`.
- [ ] **Step 4:** Commit `test(site): site check script`.

### Task 3: Englische Seite und Gestaltung

**Files:**
- Create: `site/index.html`, `site/assets/style.css`, `site/assets/site.js` (optional, nur Einblenden und Kopieren von Kleinkram), `site/favicon.ico` (Kopie von `src/ShotMark.App/Assets/ShotMark.ico`)
- Create: `.claude/launch.json` mit Eintrag `site`: `python -m http.server 8765 --directory site`, Port 8765

**Interfaces:**
- Consumes: Bilder aus Task 1, `check-site.ps1` aus Task 2.
- Produces:
  - die Abschnitt-IDs `capture`, `tools`, `more`, `multi-pc`, `why`, `download`, die Task 4 übernimmt
  - die Klassen der Markierungs-Bausteine in `style.css`, die Task 4 ebenfalls nutzt: `.mark-box` (Rahmen um Inline-Text), `.mark-arrow` (SVG), `.mark-check`/`.mark-cross` (SVG), `.mark-blur` (verpixelter Text)

- [ ] **Step 1:** Den Skill `impeccable` laden und dessen Vorgaben für Gestaltung und Kontext befolgen.
- [ ] **Step 2:** `site/index.html` mit allen acht Abschnitten aus der Spec bauen, Kopfzeile mit Navigation und Sprachumschalter (`de/`), dazu `style.css`.
  - Einstieg: „Show what matters.“ mit `.mark-box` um „matters“ und einem `.mark-arrow`.
  - Vorteile mit `.mark-check`.
  - Bei „Tools“ demonstriert `.mark-blur` ein Wort wie „password“.
- [ ] **Step 3:** `pwsh -File scripts/check-site.ps1 -Only en` → `Site OK`.
- [ ] **Step 4:** Mit `preview_start` (Name `site`) öffnen und Screenshots bei 1440 px und 375 px machen. Prüfen:
  - kein horizontales Scrollen
  - Kontrast
  - keine abgeschnittenen Texte
  - Markierungen sitzen
  - Gefundene Fehler beheben, danach Step 3 und 4 wiederholen.
- [ ] **Step 5:** Commit `feat(site): English landing page`.

### Task 4: Deutsche Seite

**Files:**
- Create: `site/de/index.html`

**Interfaces:**
- Consumes: Struktur, IDs und Klassen aus Task 3. Pfade sind relativ zu `site/de/`, also `../assets/style.css`, `../assets/de/hero.png` und `../favicon.ico`.

- [ ] **Step 1:** Deutsche Seite mit identischer Struktur und denselben IDs bauen. Den Text natürlich auf Deutsch schreiben, nicht wörtlich übersetzen. Einstieg: „Zeig, was wichtig ist.“ mit `.mark-box` um „wichtig“.
- [ ] **Step 2:** `pwsh -File scripts/check-site.ps1` (ohne `-Only`) → `Site OK`.
- [ ] **Step 3:** Screenshots bei 1440 und 375 px. Deutsche Wörter sind länger: Überschriften und Knöpfe dürfen nicht umbrechen, wo es hässlich wird. Gefundenes beheben.
- [ ] **Step 4:** Commit `feat(site): German landing page`.

### Task 5: 404, robots, sitemap, Vorschaubilder

**Files:**
- Create: `site/404.html` (zweisprachig, Links auf beide Startseiten, absolute Links auf `https://pascalsz.github.io/shotmark/…`, weil GitHub Pages 404 unter beliebigen Pfaden ausliefert)
- Create: `site/robots.txt` (alles erlaubt, `Sitemap: https://pascalsz.github.io/shotmark/sitemap.xml`)
- Create: `site/sitemap.xml` (beide URLs mit `xhtml:link`-Alternates)
- Create: `site/assets/en/og.png`, `site/assets/de/og.png` (1200×630)
- Modify: `scripts/site-images.ps1` (OG-Bild: dunkle Fläche `#1B1B1D`, Titelzeile, verkleinertes `hero.png`, mit `System.Drawing` in `pwsh` gezeichnet)
- Modify: `scripts/check-site.ps1`

**Interfaces:**
- `og:image` beider Seiten zeigt absolut auf `https://pascalsz.github.io/shotmark/assets/<lang>/og.png`, weil soziale Netzwerke relative URLs ignorieren. Das deckt die Ausnahme in Prüfung 7 ab. Bis Task 5 darf `og:image` auf `hero.png` zeigen.

- [ ] **Step 1:** `check-site.ps1` erweitern: `404.html`, `robots.txt` und `sitemap.xml` existieren, die og-Bilder existieren lokal und sind 1200×630 groß (Größe per `System.Drawing.Image` lesen). Ausführen → FAIL.
- [ ] **Step 2:** Dateien anlegen und das OG-Bild erzeugen.
- [ ] **Step 3:** `check-site.ps1` → `Site OK`. Beide OG-Bilder ansehen.
- [ ] **Step 4:** Commit `feat(site): 404, sitemap, social preview images`.

### Task 6: Veröffentlichung per GitHub Actions und README

**Files:**
- Create: `.github/workflows/pages.yml`
- Modify: `README.md` (zweite Zeile: Link auf die Projektseite, EN/DE), `.gitignore` falls nötig

**Interfaces:**
- `pages.yml`:
  - Trigger: `push` auf `main` mit `paths: ['site/**', 'scripts/check-site.ps1', '.github/workflows/pages.yml']`, außerdem `workflow_dispatch`
  - Rechte: `contents: read`, `pages: write`, `id-token: write`
  - Läuft mit `concurrency: pages`
  - Job `deploy` auf `ubuntu-latest`:
    1. checkout
    2. `pwsh -File scripts/check-site.ps1` (pwsh ist auf den Ubuntu-Runnern vorhanden; System.Drawing gibt es dort nicht, deshalb in `check-site.ps1` die Größenprüfung der OG-Bilder ohne System.Drawing über die PNG-Kopfbytes umsetzen)
    3. configure-pages
    4. upload-pages-artifact mit `path: site`
    5. deploy-pages mit `environment: github-pages`
  - Alle Actions mit den SHAs aus Global Constraints

- [ ] **Step 1:** Prüfen, ob `check-site.ps1` System.Drawing nutzt, und auf Kopfbytes umstellen (IHDR: Breite und Höhe als Big-Endian an Offset 16/20). Lokal → `Site OK`.
- [ ] **Step 2:** `pages.yml` schreiben. YAML lokal mit `pwsh -c "Get-Content .github/workflows/pages.yml | ConvertFrom-Yaml"` prüfen, falls das Modul fehlt per `python -c "import yaml; yaml.safe_load(open(...))"`.
- [ ] **Step 3:** Den Link in der README ergänzen.
- [ ] **Step 4:** Commit `ci(site): deploy project site to GitHub Pages`.

### Task 7: Audit und Feinschliff

**Files:**
- Modify: `site/**` nach Befund

- [ ] **Step 1:** `/impeccable audit` auf `site/` ausführen und die Befunde abarbeiten: Kontrast, Fokus, Hierarchie, Abstände, Bewegung.
- [ ] **Step 2:** Prüfung nach Web-Design-Richtlinien:
  - Semantik (Landmarks, eine `h1`, Überschriftenfolge)
  - Tastaturbedienung mit sichtbarem Fokus
  - `prefers-reduced-motion`
  - Seite ohne JS: im Browser JS abschalten oder die Klasse `js` entfernen; alles bleibt sichtbar
  - Bildgrößen: `hero.png` unter 600 KB, sonst stärker komprimieren
  - Lighthouse über das Chrome-DevTools-MCP (`lighthouse_audit`): Accessibility ≥ 95, Best Practices ≥ 95, SEO ≥ 95
- [ ] **Step 3:** Abschließend beide Seiten bei 1440 und 375 px als Screenshots, und `check-site.ps1` → `Site OK`.
- [ ] **Step 4:** Commit `fix(site): audit findings`.
