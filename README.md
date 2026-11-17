# ShotMark

Schlankes Screenshot- und Markierungswerkzeug für Windows: **Druck-Taste → Bereich aufziehen → markieren → fertig gespeichert.**
Gedacht als leichtgewichtige Alternative zu großen Screenshot-Suiten. Liegt der Speicherordner in einem synchronisierten
Ordner wie Dropbox, arbeiten beliebig viele PCs zusammen: Im Verlauf siehst du, welche Screenshots wann auf welchem PC
entstanden sind, und kannst jeden davon später weiter bearbeiten.

ShotMark ist kostenlos und quelloffen (MIT-Lizenz), braucht kein Konto, sammelt keine Daten und hält sich selbst aktuell.

## Installation

1. **`ShotMark.App-win-Setup.exe`** aus dem [neuesten Release](https://github.com/pascalsz/shotmark/releases/latest) herunterladen und starten.
   ShotMark wird für den angemeldeten Benutzer nach `%LOCALAPPDATA%\ShotMark.App\` installiert. Adminrechte und ein installiertes .NET sind nicht nötig.
2. Beim ersten Start fragt ShotMark nach dem **Namen dieses PCs** (z. B. „Büro“ oder „Laptop“).
3. ShotMark wartet im Infobereich der Taskleiste und startet ab jetzt mit Windows.

> **Windows-SmartScreen-Warnung?** Das Setup ist (noch) nicht digital signiert. Windows zeigt deshalb beim ersten Start
> „Der Computer wurde durch Windows geschützt“. Über *Weitere Informationen* → *Trotzdem ausführen* geht es weiter.

**Updates kommen von selbst.** ShotMark sucht kurz nach dem Start und danach alle paar Stunden nach einer neuen Version
und lädt sie im Hintergrund herunter. Installiert wird sie, wenn es niemanden stört: nach einigen Minuten ohne Maus- und
Tastatureingabe, ohne offenen Editor und ohne laufende Aufnahme. Sonst beim Beenden oder beim nächsten Start.
Offene Änderungen werden vorher gesichert.

**Deinstallieren:** Windows-Einstellungen → *Apps* → *ShotMark*. Screenshots und Einstellungen bleiben erhalten.

**Von einer älteren Version mit `Installieren.cmd`:** einfach das Setup ausführen. Die alte Kopie in
`%LOCALAPPDATA%\Programs\ShotMark\` wird beim ersten Start geordnet beendet und entfernt.

> **Druck-Taste belegt?** ShotMark meldet das beim Start. Zwei typische Ursachen:
> - **Snagit läuft:** Snagit beenden bzw. dort das Kürzel ändern.
> - **Windows 11 öffnet mit Druck das Snipping Tool:** Auf die ShotMark-Meldung klicken (öffnet die passende Windows-Einstellung)
>   und *„Druck-Taste zum Öffnen der Bildschirmaufnahme verwenden“* ausschalten.
>
> Alternativ in ShotMark unter *Einstellungen* ein anderes Kürzel wählen.

## Bedienung

| Aktion | So geht's |
|---|---|
| Screenshot | **Druck** (änderbar), Bereich mit der Maus aufziehen und loslassen – fertig. Esc oder Rechtsklick bricht ab. Lupe und Pixelmaß helfen beim genauen Ansetzen. |
| Tooltips mit aufnehmen | Einfach mit der Maus auf das Element zeigen, bis der Tooltip erscheint, und das Kürzel drücken. ShotMark hält den Bildschirm schon beim ersten Tastendruck fest (z. B. beim Strg von Strg+Umschalt+P), bevor das Programm den Tooltip wegen der Taste schließt. Maus dabei nicht bewegen. |
| Lange Seite (Scroll-Aufnahme) | Eigener Modus: **Strg+Umschalt+L** (änderbar, abschaltbar) oder Tray-Symbol → *Scroll-Aufnahme*, dann Bereich aufziehen – siehe unten. |
| Danach | Der Editor öffnet sich, das Bild ist schon gespeichert und in der Zwischenablage. |
| Editor öffnen | Tray-Symbol → *Editor öffnen* |
| Aufnahme per Maus | Doppelklick aufs Tray-Symbol oder *Neue Aufnahme* bzw. *Scroll-Aufnahme* im Menü |

### Scroll-Aufnahme (lange Webseiten, Dokumente, Listen)

1. Scroll-Aufnahme starten: **Strg+Umschalt+L** oder Tray-Symbol → *Scroll-Aufnahme*. Das normale Aufnahme-Kürzel
   fragt nie nach einer Scroll-Aufnahme.
2. Bereich über den Inhalt aufziehen, der gescrollt werden soll (z. B. den Seiteninhalt im Browser), und loslassen.
   ShotMark setzt den Mauszeiger an den rechten Rand des Bereichs, scrollt Schritt
   für Schritt nach unten und setzt die Bilder zusammen. Ein orangefarbener Rahmen zeigt den Bereich, das Panel daneben die bisherige Länge.
3. Am Seitenende hört ShotMark von selbst auf. Früher beenden: **Stopp** im Panel, **Esc** oder eines deiner Aufnahme-Kürzel.
   Was in dem Moment sichtbar ist, kommt noch mit ins Bild.

Gut zu wissen:

- **Selbst scrollen geht auch.** Sobald du Maus oder Mausrad bewegst, scrollt ShotMark nicht mehr selbst, sondern
  setzt dein Scrollen zusammen (langsam scrollen, dann Stopp). Reagiert ein Programm nicht auf automatisches Scrollen,
  schaltet ShotMark ebenfalls darauf um.
- **Feste Kopf- und Fußzeilen** von Webseiten erscheinen im Ergebnis nur einmal, auch wenn sie beim Scrollen ihr Aussehen ändern. Ein mit markierter Scrollbalken am rechten Rand wird weggeschnitten.
- **Schwebende Elemente** (Chat-Knopf, „nach oben“) stören das Zusammensetzen nicht. Sie können im Ergebnis aber mehrfach auftauchen.
- **Höchstlänge** 16.000 px bzw. 40 Megapixel – darüber können Grafikkarten das Bild nicht mehr sicher anzeigen.
- Für eine Scroll-Aufnahme muss der Bereich mindestens 100 px hoch sein. Sonst bleibt die Auswahl offen und du ziehst neu auf.
- Kürzel ändern oder abschalten: *Einstellungen* → *Tastenkürzel* → *Scroll-Aufnahme*. Im Tray-Menü bleibt die Scroll-Aufnahme immer.
- Seiten, die beim Scrollen nachladen oder sich umbauen (endlose Feeds, Animationen), können die Aufnahme vorzeitig
  beenden. Das Ergebnis bis dahin bleibt erhalten, und der Editor zeigt einen Hinweis.
- Bilder, die an derselben Stelle nachladen, werden übernommen. Passt ein Schritt einmal nicht, scrollt ShotMark zurück und macht kleinere Schritte.
- Sehr gleichförmige Inhalte (lange leere Flächen, sich exakt wiederholende Muster) können als „Seitenende“ oder „keine Bewegung“ missverstanden werden.
- Im Editor werden lange Aufnahmen auf die Fensterbreite eingepasst.

### Werkzeuge

| Werkzeug | Taste | Hinweis |
|---|---|---|
| Auswahl | V | Markierung anklicken, ziehen, an Griffen skalieren; Entf löscht; Pfeiltasten schieben (Umschalt = 10 px) |
| Rechteck | R | roter Rahmen; Umschalt = Quadrat |
| Pfeil | A | vom Anfang zur Spitze ziehen; Umschalt = 45°-Schritte |
| Haken ✓ | H | Klick setzt, Ziehen bestimmt die Größe („gut“) |
| Kreuz ✗ | X | wie Haken („schlecht“) |
| Text | T | klicken und schreiben; Enter übernimmt, Umschalt+Enter = neue Zeile; Doppelklick bearbeitet |
| Unkenntlich | U | Bereich verpixeln (Passwörter, Namen …) |
| Zuschneiden | C | Rahmen ziehen/anpassen, Enter übernimmt; jederzeit rückgängig zu machen („Zuschnitt entfernen“) |

Weitere Kürzel: Strg+Z / Strg+Y (Rückgängig/Wiederholen), Strg+C (fertiges Bild kopieren), Strg+S (sofort speichern),
Strg+N (neue Aufnahme), Strg+0 (Einpassen), Strg+1 (100 %), Strg+Mausrad (Zoom).

Farbe und Stärke stehen rechts im Panel. Ist eine Markierung ausgewählt, ändern sie genau diese.

## Wo liegen die Dateien?

Standard: `<Dropbox>\Screenshots\`. Den Dropbox-Ordner ermittelt jeder PC selbst, er darf also überall unterschiedlich liegen.
Ohne Dropbox speichert ShotMark nach `Bilder\ShotMark\`. Unter *Einstellungen* → *Speicherordner* lässt sich jeder andere
Ordner wählen, etwa ein OneDrive- oder Netzwerkordner.

```
Screenshots\
  2026-10-19_15-30-12_Buero.png        ← fertiges Bild mit Markierungen (überall sichtbar)
  2026-10-19_15-31-40_Laptop.png
  .shotmark\                           ← versteckt
    2026-10-19_15-30-12_Buero.shotmark ← Projekt: Original + Markierungen (für späteres Bearbeiten)
    pcs\Buero.json                     ← Anzeigename je PC für den Verlauf
```

- Gespeichert wird automatisch: direkt nach der Aufnahme, 1 Sekunde nach jeder Änderung und beim Schließen.
- Das Schreiben ist atomar (erst Temp-Datei, dann Austausch), Dropbox synchronisiert also nie halbe Dateien.
- PNGs ohne Projektdatei (z. B. fremde Bilder) lassen sich öffnen. Ihre Markierungen sind dann eingebrannt.

## Mehrere PCs

- Der PC-Name steht im Dateinamen, deshalb können sich zwei PCs nie gegenseitig überschreiben.
- Der **Verlauf** unten im Editor zeigt alle Aufnahmen aller PCs mit Tagestrennern, Uhrzeit und PC-Name. Mit den Filtern oben lässt er sich auf einzelne PCs einschränken.
  Neue Aufnahmen anderer PCs erscheinen, sobald Dropbox sie synchronisiert hat. Ein blauer Punkt markiert „auf diesem PC aufgenommen“.
- Wird dieselbe Aufnahme auf zwei PCs gleichzeitig bearbeitet, überschreibt ShotMark nichts. Es meldet
  „wurde auf … geändert“ und bietet **Neu laden** oder **Als Kopie speichern** an. Wer stattdessen einfach weiterarbeitet,
  bekommt seine Version beim nächsten Wechsel automatisch als Kopie gespeichert.
- Ist der Speicherordner nicht erreichbar, sichert ShotMark Aufnahmen lokal in `%LOCALAPPDATA%\ShotMark\Nicht gespeichert\`.
- Projektdateien einer neueren ShotMark-Version werden von älteren Versionen nie überschrieben.
- Einstellungen (Kürzel, PC-Name, Autostart, Sprache) gelten pro PC und liegen in `%APPDATA%\ShotMark\settings.json`.
- **Sprache:** ShotMark folgt der Windows-Sprache (Deutsch/Englisch), änderbar unter *Einstellungen* → *Sprache*. Deutsche und englische PCs können denselben Ordner teilen, Dateinamen und Projektdateien sind unabhängig von der Sprache.
- Fehlerprotokoll: `%LOCALAPPDATA%\ShotMark\log.txt`.

## Befehlszeile

| Aufruf | Wirkung |
|---|---|
| `ShotMark.exe` | starten bzw. Editor der laufenden Instanz zeigen |
| `ShotMark.exe --capture` | Aufnahme in der laufenden Instanz starten (z. B. für eine Verknüpfung) |
| `ShotMark.exe --exit` | laufende Instanz geordnet beenden |
| `ShotMark.exe --profile <Ordner>` | eigenes Einstellungsprofil, z. B. um einen zweiten PC zu simulieren |

## Entwicklung

Voraussetzung: .NET 10 SDK.

```
dotnet build ShotMark.slnx
dotnet test ShotMark.slnx
```

- `src/ShotMark.Core`: Modell, Geometrie, Dateiformat, Speichern, Verlauf, Einstellungen. Ohne UI und plattformneutral (Grundlage für eine spätere Mac-Version).
- `src/ShotMark.App`: WPF-Oberfläche, Aufnahme, Tray, Tastenkürzel.
- `src/ShotMark.App.Strings`: Texte der Oberfläche (`Strings.resx` Englisch, `Strings.de.resx` Deutsch). Texte aus `ShotMark.Core` liegen in `src/ShotMark.Core/Localization/`. Neue Sprache = weitere `*.<sprache>.resx`.
- `tests/`: xUnit-Tests. `UiSnapshots` rendert alle Fenster offscreen als PNG nach `SHOTMARK_SNAPSHOT_DIR\de\` und `…\en\`, wenn die Variable gesetzt ist.
- Lokal paketieren: `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Version 1.2.3` legt Setup, Portable-Zip und Update-Pakete nach `Releases\` ([Velopack](https://velopack.io)).
- Veröffentlichen: Tag pushen (`git tag v1.2.3`, `git push origin v1.2.3`). Die GitHub Action `release.yml` testet, packt und erstellt das Release, die installierten Versionen aktualisieren sich daraus.
- Update-Feed zum Testen umbiegen: Umgebungsvariable `SHOTMARK_UPDATE_FEED` auf einen Ordner mit `Releases\`-Inhalt setzen.

## Datenschutz

ShotMark arbeitet lokal. Es gibt keine Telemetrie und kein Konto. Aufnahmen landen nur in dem Ordner, den du selbst festlegst.
Die einzige Netzwerkverbindung ist die Update-Prüfung: Die installierte Version ruft die Release-Dateien von
`github.com/pascalsz/shotmark` ab. Dabei werden keine Daten über dich oder deine Aufnahmen übertragen.

## Lizenz

[MIT](LICENSE). ShotMark ist ein eigenständiges Projekt und enthält keinen Code und keine Grafiken aus Snagit.
Es steht in keiner Verbindung zu TechSmith. „Snagit“ ist eine Marke der TechSmith Corporation.
