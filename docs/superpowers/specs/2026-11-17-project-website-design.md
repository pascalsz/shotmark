# ShotMark-Projektseite – Design

Stand: 17.11.2026

## Warum

ShotMark ist Open Source (github.com/pascalsz/shotmark) und wird über GitHub Releases verteilt.
Interessierte sollen in einer Minute verstehen, was ShotMark ist, und es mit einem Klick herunterladen.
Die Seite gibt es auf Deutsch und Englisch, passend zur zweisprachigen App.

## Ziele

- Eine Startseite pro Sprache: Englisch unter `/`, Deutsch unter `/de/`. Beide sind inhaltlich gleich.
  Der Sprachumschalter in der Kopfzeile verlinkt jeweils dieselbe Stelle in der anderen Sprache.
  Es gibt keine automatische Umleitung nach Browsersprache. `<link rel="alternate" hreflang>` gibt es für beide Seiten plus `x-default` auf `/`.
- **Kernbotschaft:** kostenlos, schnell, ohne Konto, ohne Abo, ohne Telemetrie, quelloffen (MIT), aktualisiert sich selbst.
- **Besonderheit:** Mehrere PCs arbeiten über einen gemeinsamen Ordner zusammen (Dropbox, OneDrive oder Netzlaufwerk). Der Verlauf
  zeigt, was wann auf welchem PC entstanden ist.
- Ein Download-Knopf, der immer die neueste Version lädt:
  `https://github.com/pascalsz/shotmark/releases/latest/download/ShotMark.App-win-Setup.exe`.
  Dazu Links auf Portable-Zip (`…/latest/download/ShotMark.App-win-Portable.zip`) und alle Releases.
- Echte Bilder der App in der jeweiligen Sprache, gerendert über `UiSnapshots` (Ordner `de\` und `en\`).
- Veröffentlichung über **GitHub Pages**, automatisch bei Änderungen an der Seite auf `main`.

## Nicht-Ziele

- Kein Impressum: private, unentgeltliche Projektseite (Entscheidung Pascal).
- Keine Cookies, keine Statistik und keine Tracking-Pixel. Keine externen Schriften, Skripte oder CDNs. Alles kommt von der eigenen Seite.
- Kein Seitengenerator und kein npm. Es gibt keine Build-Kette, die gepflegt werden muss.
- Keine Doku-Seiten, kein Blog und keine Changelog-Seite. Dafür gibt es die README und die GitHub Releases.
- Keine Mac-Version erwähnen außer höchstens als „später geplant“. Kein Versprechen mit Datum.

## Anti-Ziele (wäre ein Fehlschlag, auch wenn es „funktioniert“)

- **Markenrechtlich riskant:**
  - Snagit-Logo, Snagit-Screenshots oder Snagit-Optik übernehmen
  - Wörter wie „Klon“ oder „Nachbau“
  - Vergleiche, die wir nicht belegen können („schneller als Snagit“)
  - „Snagit“ im Titel, in der URL oder in Überschriften
  - Erlaubt ist eine sachliche Einordnung („kostenlose Alternative zu Snagit für den Alltag“) samt Markenhinweis in der Fußzeile.
- Die englische Seite zeigt deutsche App-Bilder oder umgekehrt.
- Der Download-Knopf zeigt auf eine feste Version und veraltet damit.
- Eine Seite, die nach Vorlage aussieht (generische Feature-Kacheln mit Icons, Verlaufshintergründe), statt nach ShotMark.
- Schlecht lesbar oder nicht bedienbar: zu wenig Kontrast (WCAG AA), auf dem Handy horizontales Scrollen, Bedienung nur mit der Maus.

## Rahmenbedingungen

- **Hosting:** GitHub Pages aus dem öffentlichen Repo, Quelle ist eine GitHub Action (nicht der Branch-Modus).
  Die Adresse ist `https://pascalsz.github.io/shotmark/`. Alle Links sind relativ oder berücksichtigen den Unterpfad `/shotmark/`.
- **Dateien** liegen im Repo unter `site/`. Die Action `pages.yml` läuft bei Push auf `main` mit Änderungen unter `site/**`, außerdem manuell.
  Die Actions sind auf Commit-SHAs festgelegt, wie in `ci.yml`.
- Die App läuft unter Windows 10 und 11, 64-Bit. Sie braucht keine Adminrechte und kein installiertes .NET.
- **SmartScreen:** Das Setup ist (noch) unsigniert. Die Seite erklärt die Warnung („Weitere Informationen“ → „Trotzdem ausführen“) ruhig und ehrlich.
- **Bilder** kommen aus `SHOTMARK_SNAPSHOT_DIR` (siehe README, Abschnitt Entwicklung) und werden als optimierte Dateien unter
  `site/assets/de/` bzw. `site/assets/en/` abgelegt. Als Quelle dient der Branch mit der Übersetzung (`feat/localization`).
- **Hinweis:** Das aktuelle Release 1.1.0 ist noch rein deutsch. Die englische Seite stimmt erst nach dem nächsten Release mit
  Übersetzung. Die Seite geht deshalb sinnvollerweise erst nach Merge von PR #1 und #2 und einem neuen Release online.
- Der globale Arbeitsauftrag verlangt: Seite im Browser bei Desktop- und Handybreite prüfen und Fehler selbst beheben. Vor dem Abschluss laufen
  `/impeccable audit` und eine Prüfung nach Web-Design-Richtlinien.

## Ansätze

| | Ansatz | Vorteile | Nachteile |
|---|---|---|---|
| **A (gewählt)** | Handgeschriebene statische Seite (HTML/CSS, wenig JS) auf GitHub Pages | Kein Build, keine Abhängigkeiten, nichts veraltet, keine Angriffsfläche, kein eigener Server | Gemeinsame Teile (Kopf, Fuß) existieren zweimal |
| B | Seitengenerator (Astro/Hugo) mit i18n | Vorlagen ohne Doppelung | Build-Kette und Abhängigkeiten pflegen, für eine Seite zu viel |
| C | GitHub-Pages-Standardthema / README | Am schnellsten | Kaum gestaltbar, zweisprachig umständlich, wirkt wie Doku |

Pascal hat **A** gewählt: wenig Wartung und keine Angriffsfläche, die Dockerbox bleibt unbeteiligt.

## Entscheidungen zum Wie

### Gestaltung: „C dunkel“

- Die Farben der App bilden die Grundlage: Fläche `#1B1B1D`, Panels `#232326`/`#2A2A2E`, Linien `#3A3A40`, Text `#ECECEE`/`#A4A4AC`.
  Rot `#FC4242` ist die Markierungsfarbe, Grün `#3DBE5C` die des Hakens. Blau ist die Akzentfarbe der App.
- **Eigenständiges Merkmal:** Die Seite benutzt ShotMarks eigene Werkzeuge als Gestaltungsmittel. Rote Rahmen markieren Schlüsselwörter, rote Pfeile zeigen
  auf das Wichtige, grüne Haken stehen bei Vorteilen, und der Verpixel-Effekt kann z. B. „Passwörter“ unkenntlich machen. Die Formen
  entsprechen denen aus der App (gleiche Strichstärke und Rundung, leichter Schatten), sie sind als SVG/CSS umgesetzt.
- **Kontrast:** Weiße Schrift auf `#2D8CFF` (3,3 : 1) oder `#FC4242` (3,5 : 1) reicht für Knopftext nicht. Knöpfe nutzen einen dunkleren
  Ton derselben Farbe (z. B. Blau um `#1F6FE0`) oder dunkle Schrift, sodass alle Texte WCAG AA erfüllen.
- **Schrift:** Systemschriften (`Segoe UI Variable`, `Segoe UI`, `system-ui`, …), passend zur Windows-App. Es werden keine Webfonts geladen.

### Abschnitte (alle acht, Reihenfolge wie hier)

1. **Einstieg:**
   - Überschrift sinngemäß „Zeig, was wichtig ist.“ / „Show what matters.“, ein Wort darin trägt einen roten Rahmen, ein Pfeil zeigt hin.
   - Darunter ein Satz zum Nutzen, der Download-Knopf und „ohne Konto · ohne Abo · Open Source“.
   - Darunter das Editor-Bild (`01-editor`).
2. **In drei Schritten:** Druck-Taste, Bereich aufziehen, markieren. Danach ist das Bild schon gespeichert und in der Zwischenablage.
   Das Bild dazu ist das Overlay (`12-overlay-drag`).
3. **Die Werkzeuge:** Rahmen, Pfeil, Haken/Kreuz, Text, Unkenntlich machen, Zuschneiden. Dazu gehören Tastenkürzel und Rückgängig.
4. **Kann mehr als ein Bild:**
   - Scroll-Aufnahme für lange Seiten (`13-overlay-scroll` bzw. `14-scroll-panel`)
   - Tooltips mit aufnehmen
   - Verlauf
   - alles bleibt später weiter bearbeitbar
5. **Mehrere PCs, ein Ordner:** Dropbox, OneDrive oder Netzlaufwerk. Jeder PC legt seine Aufnahmen ab, der Verlauf zeigt PC und Uhrzeit, ein Filter je PC
   (`05-editor-filter-laptop`). Gleichzeitiges Bearbeiten überschreibt nichts (`07-editor-conflict`, optional).
6. **Warum ShotMark?** Die Einordnung als kostenlose Alternative zu Snagit für den Alltag:
   - kein Abo, kein Konto, keine Telemetrie
   - quelloffen
   - aktualisiert sich selbst
   - startet schnell und wartet im Infobereich
   Nur belegbare Aussagen, keine Gegenüberstellung von Funktionen.
7. **Herunterladen:**
   - großer Knopf (neueste Version)
   - Voraussetzungen: Windows 10/11, 64-Bit, keine Adminrechte
   - der SmartScreen-Hinweis
   - Links zu Portable-Zip, allen Releases und zum Quellcode
   - ein Satz zu Updates: kommen von selbst und stören nicht
8. **Fußzeile:**
   - MIT-Lizenz, GitHub-Link, Sprachumschalter
   - Markenhinweis „Snagit ist eine Marke der TechSmith Corporation. ShotMark steht in keiner Verbindung zu TechSmith.“
   - kurzer **Datenschutzhinweis**: Die Seite setzt keine Cookies, hat keine Statistik und lädt nichts von Dritten. Gehostet wird sie bei GitHub Pages, dabei
     verarbeitet GitHub technisch nötige Daten wie die IP-Adresse (Link auf GitHubs Datenschutzerklärung). Die App selbst sendet nur die
     Update-Abfrage an GitHub.

### Technik

- **Struktur:**
  - `site/index.html` (en), `site/de/index.html` (de)
  - `site/assets/style.css` (gemeinsam)
  - `site/assets/{en,de}/*.webp|png` (Bilder)
  - `site/favicon.ico`, erzeugt aus `src/ShotMark.App/Assets/ShotMark.ico`
  - `site/404.html`, zweisprachig kurz
  - `site/robots.txt`
  - `site/sitemap.xml` mit beiden Sprachen
- **Meta:**
  - `<title>` und `description` je Sprache, `lang`-Attribut, Canonical-Link
  - Open-Graph- und Twitter-Card-Tags mit einem Vorschaubild je Sprache (1200×630, aus dem Editor-Bild gestaltet)
- **Bilder:**
  - feste `width`/`height` gegen Layout-Sprünge
  - `loading="lazy"` unterhalb des Einstiegs
  - aussagekräftiges `alt` je Sprache
  - verlustarm komprimiert (WebP, mit PNG als Rückfall nur falls nötig)
- **JavaScript:** höchstens minimal, z. B. ein dezentes Einblenden der Markierungen beim Scrollen. Ohne JS bleibt alles lesbar und bedienbar.
  `prefers-reduced-motion` wird respektiert.
- **Responsive:** Die Seite funktioniert ab 360 px Breite ohne horizontales Scrollen. Die Bilder skalieren mit, die Markierungs-Deko darf auf
  kleinen Breiten vereinfacht werden.
- **Veröffentlichung:** `.github/workflows/pages.yml` mit `actions/configure-pages`, `actions/upload-pages-artifact` (Pfad `site`) und
  `actions/deploy-pages`. Die Berechtigungen sind `pages: write` und `id-token: write`. Pages muss einmalig in den Repo-Einstellungen auf „GitHub Actions“
  gestellt werden. Das ist ein Schritt für Pascal oder per `gh api`, mit Rückfrage.
- **README:** Link auf die Projektseite oben.

### Ablauf und Abgrenzung *(Entscheidungen Claude, nach Gutachter-Hinweis)*

- **Repo:** Gearbeitet und veröffentlicht wird im öffentlichen Repo `pascalsz/shotmark` (Remote `public`). Das private
  Repo bekommt nichts davon, dort gibt es keine Pages.
- **Basis:** `feat/website` baut auf `feat/localization` auf (enthält auch `feat/auto-update`). Nur dort gibt es `ci.yml`
  (Vorbild für die SHA-Festlegung) und die Snapshots je Sprache. Der PR der Seite hängt damit an PR #1 und #2. Das passt, weil die
  Seite ohnehin erst nach diesen beiden online gehen soll.
- **Ende dieses Vorhabens ist ein offener PR.** Diese Schritte entscheidet Pascal, sie gehören nicht zum Plan:
  - PR #1 und #2 mergen
  - ein neues Release taggen (z. B. `v1.2.0` mit Übersetzung)
  - danach den Seiten-PR mergen
  - Pages einmalig auf „GitHub Actions“ stellen
  `pages.yml` darf deshalb wie geplant bei Push auf `main` (`site/**`) und manuell laufen. Vor dem Merge des Seiten-PRs passiert nichts.
- **Schärfere Bilder:** `UiSnapshots` bekommt einen optionalen Faktor über die Umgebungsvariable `SHOTMARK_SNAPSHOT_SCALE`
  (Standard 1, für die Seite 2). Ohne den Faktor wären die Bilder auf hochauflösenden Bildschirmen und Handys unscharf. Die Änderung
  betrifft nur Testcode und bleibt bei fehlender Variable rückwärtskompatibel.

## Dem Umsetzer überlassen

- Genaue Texte in beiden Sprachen, im Rahmen der Abschnitte. Der Ton ist kurz, freundlich und sachlich, auf Deutsch per „du“ wie in der App.
- Genaues Layout je Abschnitt und Abstände. Dabei die SVG-Formen der Markierungen so treu wie möglich an der App ausrichten
  (`src/ShotMark.App/Rendering/AnnotationRenderer.cs`, `src/ShotMark.Core/Geometry/ShapeBuilder.cs`).
- Welche Snapshots genau wohin kommen und wie sie zugeschnitten werden.
- Ob ein kleines Skript im Repo (`scripts/site-images.ps1`) die Snapshots erzeugt und konvertiert. Empfohlen, damit sich die Bilder bei
  App-Änderungen leicht erneuern lassen.
