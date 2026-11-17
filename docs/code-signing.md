# Code-Signatur über die SignPath Foundation

Ziel: Das Setup trägt eine gültige Signatur der **SignPath Foundation**. Windows zeigt dann statt
„Unbekannter Herausgeber“ einen Herausgeber, und die SmartScreen-Warnung verschwindet, sobald das
Zertifikat genug Reputation hat (bei einem Zertifikat, das viele Projekte nutzen, meist nach kurzer Zeit).

Kostenlos für Open-Source-Projekte: [signpath.org](https://signpath.org/). Die Foundation ist der
Zertifikatsinhaber; ShotMark bekommt das Recht, damit zu signieren, und muss dafür ihre
Bedingungen einhalten: [signpath.org/terms](https://signpath.org/terms).

## Was im Repository schon vorbereitet ist

| Bedingung der Foundation | Wo erfüllt |
|---|---|
| OSI-Lizenz, öffentliches Repository | MIT, `github.com/pascalsz/shotmark` |
| Automatischer Build auf GitHub-Runnern, nur eigene Dateien signieren | `.github/workflows/release.yml`, Liste der eigenen Dateien dort und in `signpath/app-binaries.xml` |
| Jede Signatur wird von Hand freigegeben | Signing Policy mit Approval (siehe unten) |
| Code signing policy auf der Projektseite, verlinkt von Startseite und Release | `site/code-signing/`, Link im Download-Bereich, im Fuß und in den Release-Notizen (`scripts/publish.ps1`) |
| Hinweis „Free code signing provided by SignPath.io, certificate by SignPath Foundation“ | Policy-Seite, Prüfung in `scripts/check-site.ps1` |
| Rollen im Team (Committer, Reviewer, Approver) | Policy-Seite, Abschnitt „Team roles“ |
| Datenschutz: Netzwerkzugriffe erklärt, beim Installieren/ersten Start gezeigt, abschaltbar | Policy-Seite, Erststart-Fenster, Einstellung „Automatisch nach Updates suchen“ |
| Deinstallation möglich und beschrieben | Windows-Einstellungen → Apps, beschrieben in README und auf der Policy-Seite |
| Gleicher Produktname und gleiche Version in allen signierten Dateien | `Directory.Build.props` (`Product` = ShotMark, Version aus dem Tag) |

Solange die Repository-Variable `SIGNPATH_ORGANIZATION_ID` fehlt, überspringt der Release-Workflow
alle Signaturschritte und veröffentlicht wie bisher unsigniert.

## Checkliste für dich

### 1. Bewerben

- [ ] Bei GitHub und später bei SignPath **Zwei-Faktor-Anmeldung** aktivieren (Pflicht für alle mit Rechten am Projekt).
- [ ] Antrag stellen: [signpath.org/apply](https://signpath.org/apply). Angaben:
  - Projekt: ShotMark, Repository `https://github.com/pascalsz/shotmark`
  - Website: `https://pascalsz.github.io/shotmark/`
  - Code signing policy: `https://pascalsz.github.io/shotmark/code-signing/`
  - Lizenz: MIT
  - Build: GitHub Actions, `.github/workflows/release.yml`, ausgelöst durch einen Versions-Tag
  - Zu signieren: eigene .NET-Dateien (`ShotMark.exe`, `ShotMark*.dll`, deutsche Ressourcen-DLLs) und das Velopack-Setup `ShotMark.App-win-Setup.exe`
- [ ] Auf die Antwort warten. Die Foundation prüft das Projekt; Rückfragen kommen per Mail.

### 2. In SignPath einrichten (nach der Zusage)

- [ ] **GitHub App** von SignPath installieren und ihr Zugriff auf `pascalsz/shotmark` geben.
- [ ] Unter *Trusted Build Systems* das vordefinierte **GitHub.com** hinzufügen und mit dem Projekt verknüpfen.
- [ ] Projekt mit dem Slug `shotmark` anlegen (oder den Slug aus der Zusage übernehmen, siehe Variablen unten).
- [ ] Zwei **Artifact Configurations** anlegen, Inhalt jeweils aus der Datei kopieren:
  - Slug `app-binaries` ← `signpath/app-binaries.xml`
  - Slug `installer` ← `signpath/installer.xml`
- [ ] **Signing Policy** `release-signing` mit dem Zertifikat der Foundation, *Approval required*, dich als Approver.
  Als Submitter einen CI-Benutzer (Interactive user = nein) anlegen und dessen **API-Token** erzeugen.

### 3. In GitHub hinterlegen

Repository → *Settings* → *Secrets and variables* → *Actions*:

- [ ] Secret `SIGNPATH_API_TOKEN` = API-Token des CI-Benutzers
- [ ] Variable `SIGNPATH_ORGANIZATION_ID` = Organization-ID aus SignPath (schaltet die Signatur ein)
- [ ] Nur falls abweichend: Variable `SIGNPATH_PROJECT_SLUG` (Standard `shotmark`), `SIGNPATH_SIGNING_POLICY_SLUG` (Standard `release-signing`)

### 4. Erstes signiertes Release

- [ ] Auf der Policy-Seite (`site/code-signing/index.html` und `site/de/code-signing/index.html`) den Absatz
  „Signing is being set up …“ durch „Releases from version X.Y.Z on are signed.“ ersetzen, im README die
  SmartScreen-Hinweise anpassen.
- [ ] Tag pushen (`git tag v1.3.0`, `git push origin v1.3.0`).
- [ ] Der Workflow stellt **zwei** Signaturanfragen nacheinander: erst die Programmdateien, nach dem Packen das Setup.
  Beide in SignPath freigeben (Mail kommt). Der Workflow wartet je Anfrage bis zu einer Stunde.
- [ ] Prüfen: Setup herunterladen, Rechtsklick → *Eigenschaften* → *Digitale Signaturen* zeigt „SignPath Foundation“.

## Wie der Workflow signiert

1. `publish.ps1 -Step Build` baut nach `publish\`.
2. Die sechs eigenen Dateien werden nach `signing\app\` kopiert und als Artefakt hochgeladen. Kommt eine
   eigene Datei dazu oder fällt eine weg, bricht dieser Schritt ab: dann Workflow und `signpath/app-binaries.xml` anpassen.
3. SignPath signiert (nach Freigabe), der Workflow prüft jede Signatur und kopiert die Dateien zurück nach `publish\`.
4. `publish.ps1 -Step Pack` packt mit Velopack. Update-Pakete und Portable-Zip enthalten so schon die signierten Dateien.
5. Das Setup wird als zweites Artefakt signiert und in `Releases\` ersetzt.
6. `vpk upload github` veröffentlicht das Release.

Noch nicht ausprobiert, weil es erst mit dem Zertifikat geht: dass das nachträglich signierte
Velopack-Setup sauber installiert. Velopack signiert sein Setup selbst auch erst nach dem Zusammenbauen,
das sollte also passen. Beim ersten signierten Release trotzdem einmal frisch installieren und testen.
