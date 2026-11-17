# ShotMark auf Englisch und Deutsch – Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jeder nutzersichtbare Text von ShotMark gibt es auf Englisch und Deutsch. Die Sprache folgt Windows oder der Einstellung.

**Architecture:** .resx-Ressourcen mit beim Build erzeugter, öffentlicher, typisierter Klasse. Es gibt je einen Satz in
`ShotMark.Core` (`CoreStrings`) und in `ShotMark.App` (`Strings`), mit Englisch als neutraler Sprache und Deutsch in
`*.de.resx`. `App.OnStartup` setzt beim Start einmal UI- und Formatkultur. Core meldet Fehler typisiert, die App übersetzt sie.

**Tech Stack:** .NET 10, WPF, xUnit, MSBuild `EmbeddedResource GenerateSource` (keine Visual-Studio-Designer-Dateien).

**Spec:** `docs/superpowers/specs/2026-11-17-app-localization-design.md`

## Global Constraints

- Neutrale Sprache **Englisch**: `[assembly: NeutralResourcesLanguage("en")]` in beiden Projekten, Deutsch in `*.de.resx`.
- Ressourcen-Generierung ohne Designer-Datei (beim Build geprüft):
  `<EmbeddedResource Update="Localization\X.resx" GenerateSource="true" PublicClass="true" StronglyTypedLanguage="CSharp" StronglyTypedNamespace="…" StronglyTypedClassName="X" />`.
  Wichtig: **ohne `StronglyTypedLanguage` bricht der Build ab** (MSB3572).
- `<SatelliteResourceLanguages>en;de</SatelliteResourceLanguages>` in `ShotMark.App.csproj` (keine fremden Framework-Satelliten im Paket).
- **Unverändert bleiben:** Dateinamensschema (`CaptureName`, invariant), `.shotmark`, `pcs\*.json`, `document.json`,
  `<Dropbox>\Screenshots`, `SpecialFolder.MyPictures\ShotMark`, `%LOCALAPPDATA%\ShotMark\Nicht gespeichert`, Suffix `.defekt-…`,
  Protokolltexte (`Log.Info/Error`), interne Ausnahmetexte.
- Bestehende deutsche Texte wandern **wortgleich** nach `*.de.resx`. Englisch neu und natürlich formuliert.
- Tastennamen DE: Strg, Umschalt, Alt, Win, Druck, Leertaste, Einfg, Entf, Pos1, Ende, Bild auf, Bild ab, Pause, Rollen, „Num n“.
  EN: Ctrl, Shift, Alt, Win, PrtScn, Space, Ins, Del, Home, End, PgUp, PgDn, Pause, ScrLk, „Num n“.
- Platzhalter-PC: „unbekannt“ ↔ „unknown“. Vom Nutzer gewählte PC-Namen werden nie übersetzt.
- Fehlermeldung = übersetzter ShotMark-Satz. Rohtext nur als eigene Zeile `Details: {0}` (DE und EN identisch „Details:“).
- Sprachwechsel erst nach Neustart. ShotMark startet sich selbst mit denselben Argumenten neu.
- Tests setzen die Kultur ausdrücklich, CI läuft auf englischem Windows.

## Review Focus

1. **Deutschsprachiges Windows mit Regionalvariante (de-AT, de-CH):** Oberfläche Deutsch, Formate der Region bleiben. Test in Task 1.
2. **Windows in einer dritten Sprache (z. B. fr-FR):** Oberfläche Englisch **und** Formate en-US. Keine französischen Wochentage in englischen Sätzen. Test in Task 1 und Task 2.
3. **Alte oder kaputte `settings.json`** ohne `language` oder mit unbekanntem Wert („fr“, „“): gilt als automatisch, kein Absturz. Test in Task 1.
4. **Gemeinsamer Ordner von deutschem und englischem PC:** Dateinamen und Projektdateien sind unabhängig von der Kultur. Test in Task 2: `CaptureName.BaseName` unter `de-DE`, `en-US` und `ar-SA` identisch.
5. **Abgeschnittene Texte** in der anderen Sprache (Werkzeugleiste, schmaler Editor 06-editor-narrow, Einstellungen). Sichtprüfung beider Snapshot-Sätze in Task 8.

---

### Task 1: Sprachwahl in Core und Einstellung

**Files:**
- Create: `src/ShotMark.Core/Localization/UiLanguage.cs`
- Modify: `src/ShotMark.Core/Settings/AppSettings.cs` (neues Feld)
- Create: `tests/ShotMark.Core.Tests/Localization/UiLanguageTests.cs`
- Create: `tests/ShotMark.Core.Tests/CultureScope.cs`, `tests/ShotMark.App.Tests/CultureScope.cs`
- Test: `tests/ShotMark.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `public static class UiLanguage { public const string German = "de"; public const string English = "en"; public static (CultureInfo Ui, CultureInfo Format) Resolve(string? setting, CultureInfo windowsUi, CultureInfo windowsFormat); }`
  - `AppSettings.Language : string?` (`null` = automatisch, JSON-Name `language`)
  - `sealed class CultureScope(string name) : IDisposable`: setzt `CultureInfo.CurrentCulture` und `CurrentUICulture`, `Dispose` stellt sie zurück. Wird in beiden Testprojekten genutzt.

- [ ] **Step 1: Failing tests** `UiLanguageTests`:
  - `Resolve(null, de-AT, de-AT)` → Ui `de`, Format `de-AT`
  - `Resolve(null, en-GB, en-GB)` → Ui `en`, Format `en-GB`
  - `Resolve(null, fr-FR, fr-FR)` → Ui `en`, Format `en-US`
  - `Resolve("de", en-US, en-US)` → Ui `de`, Format `de-DE`
  - `Resolve("en", de-DE, de-DE)` → Ui `en`, Format `en-US`
  - `Resolve("fr", de-DE, de-DE)` und `Resolve("", …)` verhalten sich wie `null`
  - `Resolve(null, en-US, de-DE)` (englische Oberfläche, deutsches Regionalformat) → Ui `en`, Format `en-US`. Das Format folgt nur, wenn dessen Sprache zur gewählten passt.
  - `SettingsStoreTests`: `settings.json` ohne `language` → `Language == null`. Round-Trip mit `"de"`.
- [ ] **Step 2:** `dotnet test tests/ShotMark.Core.Tests --filter "UiLanguage|SettingsStore"` → FAIL (Typ fehlt).
- [ ] **Step 3: Implement** `Resolve`. Ermittle die Sprache aus `setting`, wenn sie `de` oder `en` ist. Sonst ist es `de`, wenn `windowsUi.TwoLetterISOLanguageName == "de"`, ansonsten `en`. Ui = neutrale Kultur der Sprache. Format = `windowsFormat`, wenn dessen Zwei-Buchstaben-Sprache passt, sonst `de-DE` bzw. `en-US`. `AppSettings.Language { get; init; }` hinzufügen.
- [ ] **Step 4:** Tests → PASS.
- [ ] **Step 5:** Commit `feat(core): resolve UI language from Windows or setting`.

### Task 2: Core-Ressourcen (Verlauf, Scroll-Hinweise)

**Files:**
- Create: `src/ShotMark.Core/Localization/CoreStrings.resx`, `CoreStrings.de.resx`
- Modify: `src/ShotMark.Core/ShotMark.Core.csproj` (EmbeddedResource, siehe Global Constraints; Namespace `ShotMark.Core.Localization`), `AssemblyInfo` bzw. Attribut `NeutralResourcesLanguage("en")`
- Modify: `src/ShotMark.Core/History/HistoryIndex.cs` (`DayLabel`, `ShortDayNames` entfällt)
- Modify: `src/ShotMark.Core/Scrolling/ScrollCaptureController.cs` (`HintManual`, `HintNoReaction`)
- Create: `tests/ShotMark.Core.Tests/Localization/ResourceCompletenessTests.cs`
- Test: `tests/ShotMark.Core.Tests/History/HistoryIndexTests.cs`, `Storage/CaptureNameTests.cs`, `Scrolling/ScrollCaptureControllerTests.cs`

**Interfaces:**
- Produces: `ShotMark.Core.Localization.CoreStrings` (öffentlich) mit mindestens `Today`, `Yesterday`, `DayLabelThisYear` (DE `ddd dd.MM.`, EN `ddd MMM d`), `DayLabelOtherYear` (DE `ddd dd.MM.yyyy`, EN `ddd MMM d, yyyy`), `ScrollHintManual`, `ScrollHintNoReaction`.
- `ScrollCaptureController.HintManual` und `HintNoReaction` werden `public static string … => CoreStrings.…` (statt `const`). `ScrollProgress.Hint` bleibt `string?`.
- `HistoryIndex.DayLabel` formatiert mit `CultureInfo.CurrentCulture`, Texte aus `CoreStrings` (`CurrentUICulture`).

- [ ] **Step 1: Failing tests**
  - `HistoryIndexTests.DayLabel` läuft unter `new CultureScope("de-DE")` mit den bestehenden Erwartungen („Heute“, „Gestern“, „Di 13.10.“, „Mo 12.10.“, „Mi 31.12.2025“).
  - Neuer Test `DayLabel_english` unter `en-US`: „Today“, „Yesterday“, „Tue Oct 13“, „Wed Dec 31, 2025“.
  - `WithDaySeparators_inserts_header_per_day` unter `de-DE`.
  - `CaptureNameTests`: `BaseName` für denselben Zeitstempel ist unter `de-DE`, `en-US` und `ar-SA` identisch `2026-10-19_15-30-12_Buero` (Review Focus 4).
  - `ResourceCompletenessTests.German_translates_every_core_text`: Jeder Schlüssel aus `CoreStrings.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true)` hat in `GetResourceSet(new CultureInfo("de"), true, false)` einen nicht leeren Wert. Kein Wert in beiden Sätzen ist leer.
- [ ] **Step 2:** `dotnet test tests/ShotMark.Core.Tests` → FAIL.
- [ ] **Step 3: Implement.** Ressourcen anlegen und die deutschen Texte wortgleich übernehmen. `DayLabel` und die Hints umstellen, `ScrollCaptureControllerTests` unverändert lassen (vergleicht mit der Eigenschaft).
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS (App kompiliert weiter, weil `ScrollProgress.Hint` ein String bleibt).
- [ ] **Step 5:** Commit `feat(core): English and German texts for history and scroll hints`.

### Task 3: App-Ressourcen und Sprache beim Start

**Files:**
- Create: `src/ShotMark.App/Localization/Strings.resx`, `Strings.de.resx`
- Create: `src/ShotMark.App/Localization/Language.cs`
- Modify: `src/ShotMark.App/ShotMark.App.csproj` (EmbeddedResource mit Namespace `ShotMark.App.Localization`, `SatelliteResourceLanguages`), `src/ShotMark.App/AssemblyInfo.cs` (`NeutralResourcesLanguage("en")`)
- Modify: `src/ShotMark.App/App.xaml.cs` (`OnStartup`: Sprache setzen, bevor `AppHost` entsteht)
- Create: `tests/ShotMark.App.Tests/ResourceCompletenessTests.cs`

**Interfaces:**
- Consumes: `UiLanguage.Resolve`, `AppSettings.Language` (Task 1).
- Produces:
  - `ShotMark.App.Localization.Strings` (öffentlich, für `{x:Static}` in XAML). Am Ende von Task 3 enthält die Datei nur `UpdatedNotice` („ShotMark was updated to version {0}.“ / „ShotMark wurde auf Version {0} aktualisiert.“) und `ErrorDetails` („Details: {0}“). Weitere Schlüssel kommen in Task 4–7 dazu.
  - `internal static class Language { public static void Apply(string? setting); }` setzt `CultureInfo.DefaultThreadCurrentUICulture`/`DefaultThreadCurrentCulture` sowie die Kultur des aktuellen Threads aus `UiLanguage.Resolve(setting, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture)`. Zusätzlich `FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(format.IetfLanguageTag)))`, damit WPF-Bindungen (Datumsformat) dieselbe Kultur nutzen. Einmal pro Prozess aufrufen.
- `App.OnStartup` liest die Einstellungen schon vor `new AppHost(…)` (über `new SettingsStore(profile).Load().Language`) und ruft `Language.Apply` vor dem Erstellen jedes Fensters auf. Der Hinweis nach dem Update nutzt `Strings.UpdatedNotice`.

- [ ] **Step 1: Failing test** `ResourceCompletenessTests.German_translates_every_app_text` (wie in Task 2, für `Strings.ResourceManager`).
- [ ] **Step 2:** `dotnet test tests/ShotMark.App.Tests` → FAIL (Typ fehlt).
- [ ] **Step 3: Implement** Ressourcen, `Language.Apply` und den Startaufruf. Prüfen, dass `{x:Static loc:Strings.UpdatedNotice}` in einer XAML-Datei kompiliert (WPF-Markup-Pass mit erzeugter Klasse). Ist der Test erfolgreich, wieder entfernen. Scheitert er, die Generierung über `StronglyTypedFileName` nach `Localization\Strings.g.cs` im Projektordner umlenken und diese Datei in `.gitignore` aufnehmen.
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS. `dotnet publish … -o publish` enthält `de\ShotMark.resources.dll` und `de\ShotMark.Core.resources.dll`, aber keine Ordner `fr\`, `ja\` usw.
- [ ] **Step 5:** Commit `feat(app): resource infrastructure and language selection at startup`.

### Task 4: Typisierte Fehler in Core, übersetzte Meldungen in der App

**Files:**
- Modify: `src/ShotMark.Core/Storage/CaptureStore.cs` (`SaveOutcome.Failed`, `LoadedCapture`, `DiskState.Unavailable`, `NewerVersionMessage` entfällt)
- Create: `src/ShotMark.App/Localization/ErrorText.cs`
- Modify: `src/ShotMark.App/AppHost.cs` (Zeilen um 146–150, 238–256, 286–296), `src/ShotMark.App/Editor/EditorViewModel.cs` (Warnungstext aus `LoadWarning`), `src/ShotMark.App/Editor/OpenCapture.cs` falls nötig, `src/ShotMark.App/App.xaml.cs` (Fehlerdialog)
- Test: `tests/ShotMark.Core.Tests/Storage/CaptureStoreTests.cs`, Create `tests/ShotMark.App.Tests/ErrorTextTests.cs`

**Interfaces:**
- Produces (Core):
  - `public enum StorageError { FolderUnavailable, WriteFailed, NewerVersion }`
  - `public sealed record Failed(StorageError Error, string? Detail) : SaveOutcome;`. `Unavailable` → `FolderUnavailable` mit `e.Message` als Detail, `Newer` → `NewerVersion` ohne Detail, die Ausnahme im Save-Catch → `WriteFailed` mit `e.Message`.
  - `public enum LoadWarning { NewerVersion, ProjectUnreadable }`, `LoadedCapture(CapturePaths Paths, CaptureDocument? Document, byte[] ImagePng, LoadWarning? Warning)`
- Produces (App): `internal static class ErrorText`
  - `string Describe(LoadWarning warning)`
  - `string SaveFailed(SaveOutcome.Failed failed, string folder)`: Ordner nicht erreichbar, nicht speicherbar oder neuere Version
  - `string WithDetails(string message, string? detail)`: hängt `"\n" + Strings.ErrorDetails` an, wenn `detail` nicht leer ist
  - Dazu die Texte für „Aufnahme fehlgeschlagen“, „konnte nicht geöffnet werden“ und den unerwarteten Fehler.
  - Neue Schlüssel mit den heutigen deutschen Sätzen ohne den eingebetteten Rohtext, z. B. `SaveFolderUnavailableRescued` = „Der Speicherordner „{0}“ ist nicht erreichbar. Die Aufnahme wurde lokal gesichert: {1}“.

- [ ] **Step 1: Failing tests**
  - `CaptureStoreTests`: Statt `Assert.Contains("neueren", …)` jetzt `Assert.Equal(StorageError.NewerVersion, failed.Error)` bzw. `Assert.Equal(LoadWarning.NewerVersion, loaded.Warning)`. Beschädigte Projektdatei → `LoadWarning.ProjectUnreadable`.
  - `ErrorTextTests` (unter `de-DE` und `en-US`):
    - `Describe(LoadWarning.NewerVersion)` unter `de-DE` enthält „neueren ShotMark-Version“, unter `en-US` „newer version of ShotMark“.
    - `WithDetails("X", "Zugriff verweigert")` == `"X\nDetails: Zugriff verweigert"`.
    - `WithDetails("X", null)` == `"X"`.
- [ ] **Step 2:** `dotnet test ShotMark.slnx` → FAIL.
- [ ] **Step 3: Implement.** Core umstellen. In der App jede Stelle, die heute `e.Message` oder `failed.Message` in einen sichtbaren Text einbettet, über `ErrorText` laufen lassen. Die Rohtexte gehen nur noch als Detail weiter, die `Log.Error`-Aufrufe bleiben unverändert.
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS. `git grep -n "e.Message\|failed.Message" src/ShotMark.App` findet nur noch `Log`-Aufrufe und `ErrorText.WithDetails(…)`.
- [ ] **Step 5:** Commit `feat: typed storage errors, translated error messages`.

### Task 5: Editor übersetzen (Fenster, ViewModel, Verlauf)

**Files:**
- Modify: `src/ShotMark.App/Editor/EditorWindow.xaml` (54 Texte), `EditorWindow.xaml.cs`, `EditorViewModel.cs`, `src/ShotMark.App/History/HistoryStripViewModel.cs`, `src/ShotMark.App/Editor/AnnotationCanvas.cs` (sichtbare Texte, falls vorhanden)
- Modify: `Strings.resx`, `Strings.de.resx`
- Test: `tests/ShotMark.App.Tests/EditorViewModelTests.cs`, Create `tests/ShotMark.App.Tests/HistoryTextTests.cs`

**Interfaces:**
- Consumes: `Strings` (Task 3).
- XAML: `xmlns:loc="clr-namespace:ShotMark.App.Localization"`, `Text="{x:Static loc:Strings.Key}"`. Tooltips mit Tastenkürzel (z. B. „Auswählen und verschieben (V)“) bleiben als ein Text pro Sprache, die Buchstaben-Kürzel sind in beiden Sprachen gleich.
- `HistoryStripViewModel`: Das Feld `German` entfällt. Zeitstempel in Tooltip und Barrierefreiheitstext nutzen `CultureInfo.CurrentCulture` und eine Formatzeichenkette aus den Ressourcen. Für DE gelten die heutigen Formate (`ddd dd.MM.yyyy, HH:mm:ss` bzw. `dd.MM.yyyy HH:mm`). Zähltexte („1 Aufnahme“, „{0} Aufnahmen“, „{0} von {1} PCs“), „Alle PCs“ und „unbekannt“ kommen aus den Ressourcen.

- [ ] **Step 1: Failing tests** `HistoryTextTests`: unter `de-DE` lautet die Zusammenfassung für 1 Aufnahme „1 Aufnahme“, für 3 Aufnahmen von 2 PCs „3 Aufnahmen von 2 PCs“. Unter `en-US` „1 capture“ und „3 captures from 2 PCs“. Ist die zuständige Methode privat, über `InternalsVisibleTo` (bereits vorhanden) eine `internal static`-Hilfsmethode testen. Bestehende `EditorViewModelTests` laufen unter `de-DE`. Prüfen sie Statustexte, gelten die deutschen Erwartungen unverändert.
- [ ] **Step 2:** `dotnet test tests/ShotMark.App.Tests` → FAIL.
- [ ] **Step 3: Implement:** alle sichtbaren Texte der genannten Dateien in die Ressourcen. Der Statustext „Gespeichert ✓ {0:t}“ nutzt die Formatkultur.
- [ ] **Step 4:** Tests → PASS. `git grep -n -P '"[^"]*[äöüÄÖÜß][^"]*"' src/ShotMark.App/Editor src/ShotMark.App/History` findet nur noch Kommentare und `Log`-Texte.
- [ ] **Step 5:** Commit `feat(app): translate editor and history`.

### Task 6: Einstellungen, Erststart und Tastennamen übersetzen

**Files:**
- Modify: `src/ShotMark.App/Settings/SettingsWindow.xaml` (21 Texte), `SettingsWindow.xaml.cs`, `FirstRunWindow.xaml` (6), `FirstRunWindow.xaml.cs`, `HotkeyText.cs`
- Create: `tests/ShotMark.App.Tests/HotkeyTextTests.cs`

**Interfaces:**
- `HotkeyText.Format(HotkeySetting)` bleibt in der Signatur gleich und liefert die Namen der aktuellen UI-Sprache (Werte siehe Global Constraints).

- [ ] **Step 1: Failing tests** `HotkeyTextTests`: `Format(new HotkeySetting(0x4C, true, true, false, false))` ergibt unter `de-DE` „Strg+Umschalt+L“ und unter `en-US` „Ctrl+Shift+L“. `Format(HotkeySetting.PrintScreen)` ergibt „Druck“ bzw. „PrtScn“.
- [ ] **Step 2:** Tests → FAIL.
- [ ] **Step 3: Implement**, die Texte kommen aus den Ressourcen.
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS.
- [ ] **Step 5:** Commit `feat(app): translate settings, first run and key names`.

### Task 7: Tray, Aufnahme-Overlays, Meldungen und Sprachauswahl mit Neustart

**Files:**
- Modify: `src/ShotMark.App/Tray/TrayIcon.cs`, `AppHost.cs` (übrige Texte), `App.xaml.cs`, `Capture/OverlayWindow.cs`, `Capture/ScrollFrameWindow.cs`, `Native/HotkeyManager.cs`, `Native/HotkeyPreCapture.cs` (nur sichtbare Texte), `Rendering/*` (nur sichtbare Texte)
- Modify: `Settings/SettingsWindow.xaml(.cs)` (Sprachauswahl)
- Create: `tests/ShotMark.App.Tests/RestartArgumentsTests.cs`

**Interfaces:**
- Sprachauswahl in den Einstellungen unter „Verhalten“: ein neuer Abschnitt „Sprache“/„Language“ mit drei RadioButtons im vorhandenen Stil `Chip`: „Automatisch (Windows-Sprache)“/„Automatic (Windows language)“, „Deutsch“, „English“. Die Sprachnamen stehen in beiden Oberflächen in der jeweils eigenen Sprache. Das Ergebnis landet in `Result.Language`.
- `AppHost` bekommt `public event Action? RestartRequested;`. Nach *OK* mit geänderter Sprache: speichern, dann `RestartRequested` auslösen.
- `App` reagiert auf `RestartRequested` so:
  1. `await host.ShutdownAsync()`
  2. `Process.Start(Environment.ProcessPath, RestartArguments.Build(originalArgs, Environment.ProcessId))`
  3. `Shutdown()`
- `internal static class RestartArguments { public static string[] Build(string[] args, int pid); public static int? WaitFor(string[] args); }`
  - `Build` hängt `--wait-for <pid>` an, ersetzt ein vorhandenes und behält alles andere (z. B. `--profile X`).
  - `App.OnStartup` wartet, wenn `WaitFor` einen Wert liefert, vor dem Mutex bis zu 30 s auf das Ende dieses Prozesses (`Process.GetProcessById`, fehlender Prozess = schon beendet).

- [ ] **Step 1: Failing tests** `RestartArgumentsTests`:
  - `Build(["--profile","C:\\p"], 42)` == `["--profile","C:\\p","--wait-for","42"]`
  - `Build(["--wait-for","7"], 42)` == `["--wait-for","42"]`
  - `WaitFor(["--wait-for","42"])` == 42
  - `WaitFor(["--wait-for","x"])` == null
  - `WaitFor([])` == null
- [ ] **Step 2:** Tests → FAIL.
- [ ] **Step 3: Implement** die Übersetzung der genannten Dateien, die Sprachauswahl und den Neustart.
- [ ] **Step 4:** `dotnet test ShotMark.slnx` → PASS. `git grep -n -P '"[^"]*[äöüÄÖÜß][^"]*"' src/ShotMark.App -- ':!*.resx'` findet nur noch Kommentare, `Log`-Texte und den Rettungsordner „Nicht gespeichert“.
- [ ] **Step 5: Manuell:** Die App mit `--profile <Testordner>` starten, in den Einstellungen „English“ wählen und *OK* drücken. ShotMark startet von selbst neu, Tray-Menü und Editor sind auf Englisch. Danach wieder „Automatisch“ wählen.
- [ ] **Step 6:** Commit `feat(app): translate tray, capture overlays and messages; language setting`.

### Task 8: Snapshots je Sprache für die Webseite

**Files:**
- Modify: `tests/ShotMark.App.Tests/UiSnapshots.cs`

**Interfaces:**
- `UiSnapshots` rendert unter `SHOTMARK_SNAPSHOT_DIR\de\` und `…\en\` jeweils alle bisherigen Bilder (01–11 …). Für jede Sprache: `CultureScope`, eigener Aufnahmeordner und eigene PC-Liste.
- Demo-Inhalte je Sprache:

  | | DE | EN |
  |---|---|---|
  | PC-Namen | „Büro“, „Laptop“ | „Office“, „Laptop“ |
  | Textmarkierung | „Hier klicken“ | „Click here“ |
  | gerenderte Beispielseite | „Kundenportal“, „Dokumente anfragen“ und Kontonummer `DE12 …` | z. B. „Customer portal“, „Request documents“ und eine Account number im US-Stil |
  | Kürzel | „Druck“ | „PrtScn“ |
  | unbekannter PC | „unbekannt“ | „unknown“ |

  Die Uhrzeiten werden in der jeweiligen Kultur formatiert.

- [ ] **Step 1:** `SHOTMARK_SNAPSHOT_DIR=<Scratch>\snaps dotnet test tests/ShotMark.App.Tests --filter UiSnapshots` erzeugt beide Ordner mit gleich vielen PNGs.
- [ ] **Step 2: Sichtprüfung** aller PNGs beider Sprachen (Review Focus 5):
  - keine abgeschnittenen oder überlappenden Texte, besonders in `06-editor-narrow`, `09-settings` und der Werkzeugleiste
  - keine Reste der anderen Sprache
  Gefundene Layoutprobleme in XAML beheben, dabei Breiten lieber an den Inhalt anpassen als Texte zu kürzen.
- [ ] **Step 3:** `dotnet test ShotMark.slnx` → PASS.
- [ ] **Step 4:** In der README unter *Entwicklung* den Satz zu `UiSnapshots` um die Unterordner `de\` und `en\` ergänzen, außerdem einen Satz zur Sprache: „ShotMark folgt der Windows-Sprache (Deutsch/Englisch), änderbar unter Einstellungen → Sprache.“
- [ ] **Step 5:** Commit `test(app): render UI snapshots per language with localized demo content`.
