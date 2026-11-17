# ShotMark auf Englisch und Deutsch – Design

Stand: 17.11.2026

## Warum

ShotMark ist jetzt Open Source und bekommt eine zweisprachige Projektseite (Deutsch und Englisch).
Wer über die englische Seite kommt, soll eine englische Oberfläche bekommen. Bisher sind alle Texte
deutsch und stehen direkt im Code bzw. in den XAML-Dateien.

Die Webseite ist ein eigenes, nachfolgendes Vorhaben. Sie zeigt echte Bilder der App, die aus der App
gerendert werden (`UiSnapshots`). Deshalb kommt die Übersetzung zuerst.

## Ziele

- Jeder Text, den ein Nutzer sieht, gibt es auf Englisch und Deutsch: Editor (Werkzeugleiste, Tooltips,
  Statuszeile, Verlauf, Filter), Tray-Menü und -Meldungen, Einstellungen, Erststart-Dialog, Overlay bei der
  Aufnahme (Pixelmaß, Hinweise), Scroll-Aufnahme-Panel, Meldungsfenster (MessageBox), Warnungen aus `ShotMark.Core`
  (z. B. „neuere Version“, „Projektdatei konnte nicht gelesen werden“, Scroll-Hinweise), Tagesüberschriften im Verlauf
  („Heute“, „Gestern“, Wochentag + Datum) und Tastennamen in Kürzeln („Strg+Umschalt+L“ ↔ „Ctrl+Shift+L“).
- **Sprachwahl:** ShotMark folgt der Windows-Anzeigesprache (`CultureInfo.CurrentUICulture` beim Start): Deutsch bei
  jeder `de-*`-Kultur, sonst Englisch. In den Einstellungen lässt sich die Sprache festlegen:
  *Automatisch (Windows-Sprache)* / *Deutsch* / *English*. Die Auswahl gilt pro PC (wie die übrigen Einstellungen).
- Datums- und Zeitangaben folgen der gewählten Sprache (Deutsch: „Mo 17.11.“, „17.11.2026, 14:30“;
  Englisch: Formate der Kultur `en-US` bzw. sinngemäß „Mon Nov 17“, „11/17/2026, 2:30 PM“).
- Fehlt ein Text in einer Übersetzung, fällt ShotMark auf Englisch zurück und zeigt nie einen leeren Text oder einen Schlüsselnamen.
- Weitere Sprachen sollen später nur eine zusätzliche `.resx`-Datei brauchen.

## Nicht-Ziele

- Keine Übersetzung von Protokolleinträgen (`log.txt`) und internen Ausnahmetexten, die nur in Logs landen.
  Sie bleiben, wie sie sind.
- Keine Übersetzung von README, Projektdokumentation und Webseite. Die Webseite ist das nächste Vorhaben.
- Keine Sprachwahl im Velopack-Setup (dessen kleine Oberfläche bleibt, wie Velopack sie liefert).
- Kein Sprachwechsel ohne Neustart.

## Anti-Ziele (wäre ein Fehlschlag, auch wenn es „funktioniert“)

- **Dateinamen, Ordner und Dateiformat ändern sich mit der Sprache.** Screenshots, Projektdateien, Verlauf und
  PC-Registry müssen zwischen deutschen und englischen PCs, die denselben Ordner teilen, unverändert austauschbar
  bleiben. Unverändert bleiben deshalb insbesondere: Dateinamensschema der Aufnahmen, `.shotmark`, `pcs\*.json`,
  `document.json`, Standardordner `<Dropbox>\Screenshots` bzw. `Bilder\ShotMark` (über `SpecialFolder.MyPictures`),
  der Rettungsordner `%LOCALAPPDATA%\ShotMark\Nicht gespeichert` und das Suffix `.defekt-…` für kaputte Projektdateien.
  Bestehende Dateien dürfen nicht „verschwinden“, weil ein Name übersetzt wurde.
- Ein englischer PC zeigt deutsche Reste oder umgekehrt.
- Ein vom Nutzer frei gewählter PC-Name wird übersetzt (er ist Nutzerinhalt). Übersetzt wird nur der Platzhalter für
  einen unbekannten PC („unbekannt“ ↔ „unknown“).
- Die Tests werden abhängig von der Sprache des Rechners, auf dem sie laufen (CI läuft auf englischem Windows).

## Rahmenbedingungen

- WPF, .NET 10, `ShotMark.Core` bleibt UI-frei und plattformneutral (spätere Mac-Version).
- Veröffentlicht wird self-contained als Ordner (kein Single-File) über Velopack. Satelliten-Assemblies
  (`de\ShotMark.resources.dll` usw.) landen dadurch automatisch im Paket.
- Es gibt UI-Snapshot-Tests (`tests/ShotMark.App.Tests/UiSnapshots.cs`), die alle Fenster offscreen als PNG rendern,
  wenn `SHOTMARK_SNAPSHOT_DIR` gesetzt ist.
- Umfang heute grob: ~80 Texte in XAML (`EditorWindow.xaml` 54, `SettingsWindow.xaml` 21, `FirstRunWindow.xaml` 6) und
  ~200 Zeichenketten in C# (Schwerpunkte `EditorWindow.xaml.cs`, `AppHost.cs`, `SettingsWindow.xaml.cs`,
  `EditorViewModel.cs`, `HotkeyText.cs`, `ScrollFrameWindow.cs`, `TrayIcon.cs`, `HistoryStripViewModel.cs`,
  `OverlayWindow.cs`, `NativeMethods.cs` enthält überwiegend P/Invoke-Namen, keine UI-Texte).

## Ansätze

| | Ansatz | Vorteile | Nachteile |
|---|---|---|---|
| **A (gewählt)** | **.resx-Ressourcen** mit generierter, typisierter Klasse; Englisch als neutrale Grundsprache, Deutsch als `*.de.resx` | Standard in .NET; Tippfehler und fehlende Schlüssel fallen beim Build auf; funktioniert in `ShotMark.Core` und später auf dem Mac; Rückfall auf Englisch eingebaut | Sprachwechsel braucht Neustart |
| B | WPF-`ResourceDictionary` pro Sprache mit `DynamicResource` | Sprachwechsel live | Zeichenketten-Schlüssel, Fehler erst zur Laufzeit; nur WPF |
| C | Eigene JSON-Dateien + Übersetzungsdienst | Übersetzer brauchen kein Visual Studio | Eigener Code zum Pflegen; lohnt sich bei zwei Sprachen nicht |

Pascal hat **A** gewählt: Standardweg, Fehler beim Build, Mac-tauglich. Der Neustart beim Sprachwechsel ist bei einer
Einstellung, die man einmal setzt, vertretbar.

## Entscheidungen zum Wie

- **Zwei Ressourcen-Sätze:** `ShotMark.Core` bekommt eigene Ressourcen für seine nutzersichtbaren Texte (Verlaufs-
  überschriften, Warnungen aus `CaptureStore`, Scroll-Hinweise), `ShotMark.App` für alles andere. So bleibt Core
  eigenständig.
- **Neutrale Sprache Englisch** (`Strings.resx`), Deutsch in `Strings.de.resx`. `NeutralResourcesLanguage("en")`
  setzen. Bestehende deutsche Texte wandern 1:1 nach `Strings.de.resx`; der englische Text wird neu geschrieben
  (natürliches Englisch, kein Wort-für-Wort).
- **XAML** bindet statisch an die generierte Klasse (`{x:Static …}`), Code-behind und ViewModels nutzen die typisierten
  Eigenschaften. Formatierte Texte mit Platzhaltern (`„{0}“ ist belegt`) als Formatzeichenketten in den Ressourcen,
  nicht durch Zusammensetzen von Satzteilen.
- **Sprache festlegen beim Start** in `App.OnStartup`, bevor ein Fenster entsteht: Einstellung lesen; bei
  *Automatisch* `de`, wenn die Windows-Anzeigesprache Deutsch ist, sonst `en`. `CultureInfo.DefaultThreadCurrentUICulture`
  und `DefaultThreadCurrentCulture` (für Datumsformate) entsprechend setzen. Der Erststart-Dialog erscheint damit schon
  in der Windows-Sprache.
- **Einstellung:** neues Feld in `AppSettings` (z. B. `Language`: `null` = automatisch, sonst `"de"`/`"en"`).
  Ältere `settings.json` ohne Feld bedeuten „automatisch“.
- **Sprachwechsel in den Einstellungen:** Nach *OK* mit geänderter Sprache startet ShotMark sich neu, mit denselben
  Argumenten (z. B. `--profile`). Offene Änderungen werden vorher gesichert, wie beim Beenden. Der neue Prozess darf
  dabei nicht an der Einzelinstanz-Sperre der alten Instanz scheitern (er muss warten, bis die alte beendet ist).
- **Tastennamen:** `HotkeyText` liefert Tastenbezeichnungen je Sprache (Deutsch wie heute: Strg, Umschalt, Druck,
  Leertaste, Einfg, Entf, Pos1, Ende, Bild auf, Bild ab, Rollen; Englisch: Ctrl, Shift, PrtScn, Space, Ins, Del,
  Home, End, PgUp, PgDn, ScrLk). Gespeichert werden Kürzel weiterhin als Tastencodes, nicht als Text.
- **Tests:** Tests, die Texte prüfen, setzen die Kultur ausdrücklich (z. B. Deutsch für bestehende Erwartungen wie
  „Heute“) und es kommen Tests für Englisch dazu. Ein Test stellt sicher, dass jeder Schlüssel der neutralen Ressource
  auch eine deutsche Übersetzung hat und kein Wert leer ist (je Assembly).
- **Fehlermeldungen mit Ausnahmetext** *(Entscheidung Claude, nach Gutachter-Hinweis)*: Heute landen einige
  `e.Message` direkt in sichtbaren Meldungen (Öffnen aus dem Verlauf, `SaveOutcome.Failed`, Tray-Warnung bei
  fehlgeschlagener Aufnahme, Fehlerdialog). Künftig ist der sichtbare Satz immer ein übersetzter ShotMark-Text.
  - Bekannte Fälle aus `ShotMark.Core` werden typisiert statt als fertiger Text gemeldet: Projektdatei einer
    neueren Version, Projektdatei beschädigt bzw. Eintrag fehlt, Ordner nicht erreichbar, Schreiben fehlgeschlagen.
    `SaveOutcome`, `DiskState` und `LoadedCapture.Warning` tragen dafür eine Fehlerart, die App übersetzt sie.
  - Unbekannte Ausnahmen bekommen einen allgemeinen übersetzten Satz. Der rohe Ausnahmetext, der bei
    Betriebssystemfehlern in der Windows-Sprache kommt, erscheint nur als abgesetzte Zeile („Details: …“ bzw.
    „Details: …“ auf Englisch) und vollständig im Protokoll. Diese eine Detailzeile darf in der Windows-Sprache sein. Das gilt nicht
    als „Rest der anderen Sprache“.
  - Interne Ausnahmetexte in Core (z. B. `"original.png fehlt"`) bleiben unübersetzt, dürfen aber nicht mehr als
    eigenständige Meldung beim Nutzer ankommen.
- **Snapshots für die Webseite** *(Entscheidung Claude, nach Gutachter-Hinweis)*: `UiSnapshots` rendert alle
  Fenster je Sprache in Unterordner (`de\`, `en\`) unterhalb von `SHOTMARK_SNAPSHOT_DIR`. Die Demo-Inhalte sind
  je Sprache eigene: PC-Namen („Büro“/„Laptop“ ↔ „Office“/„Laptop“), die gerenderte Beispielseite im
  Screenshot, die Textmarkierung („Hier klicken“ ↔ „Click here“), Kürzelanzeige („Druck“ ↔ „PrtScn“) und
  Datumsangaben. Jede Sprache bekommt dafür ihren eigenen Aufnahmeordner und ihre eigene PC-Liste. Sonst wären die
  englischen Webseiten-Bilder voller deutscher Inhalte.

## Dem Umsetzer überlassen

- Genaue Schlüsselnamen und die Aufteilung innerhalb der `.resx` (eine Datei je Projekt oder je Bereich).
- Wie der Neustart beim Sprachwechsel das Warten auf die alte Instanz löst (z. B. Argument mit der Prozess-Id der alten Instanz).
- Ob Layouts für längere englische/deutsche Texte angepasst werden müssen. Vorgabe: keine abgeschnittenen Texte in
  beiden Sprachen, geprüft über die Snapshots bei normaler und schmaler Editorbreite.
- Wie die genauen englischen Datumsformate aussehen, solange sie für englische Nutzer natürlich wirken.
