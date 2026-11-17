@echo off
setlocal
rem Installiert bzw. aktualisiert ShotMark fuer den angemeldeten Benutzer (keine Adminrechte noetig).
rem Systemwerkzeuge mit vollem Pfad, damit z. B. Git-Unix-Werkzeuge im PATH nicht stoeren.
set "SRC=%~dp0ShotMark.exe"
set "DEST=%LOCALAPPDATA%\Programs\ShotMark"
set "SYS=%SystemRoot%\System32\"

if not exist "%SRC%" (
  echo ShotMark.exe wurde neben diesem Skript nicht gefunden.
  pause
  exit /b 1
)

rem Laufende Version geordnet beenden (sie sichert offene Aenderungen) und bis zu 20 s darauf warten
if exist "%DEST%\ShotMark.exe" "%DEST%\ShotMark.exe" --exit
for /L %%i in (1,1,20) do (
  "%SYS%tasklist.exe" /FI "IMAGENAME eq ShotMark.exe" 2>nul | "%SYS%find.exe" /I "ShotMark.exe" >nul || goto :beendet
  "%SYS%timeout.exe" /t 1 /nobreak >nul
)
"%SYS%taskkill.exe" /IM ShotMark.exe /F >nul 2>&1
"%SYS%timeout.exe" /t 1 /nobreak >nul
:beendet

if not exist "%DEST%" mkdir "%DEST%"
copy /Y "%SRC%" "%DEST%\ShotMark.exe" >nul
if errorlevel 1 (
  echo Kopieren fehlgeschlagen. Laeuft ShotMark noch?
  pause
  exit /b 1
)

start "" "%DEST%\ShotMark.exe"
echo.
echo ShotMark ist installiert und laeuft jetzt im Infobereich der Taskleiste (unten rechts).
echo Beim ersten Start fragt ShotMark nach dem Namen dieses PCs.
echo Screenshot: Druck-Taste druecken und Bereich aufziehen.
"%SYS%timeout.exe" /t 8 >nul
