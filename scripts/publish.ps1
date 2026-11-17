# Baut ShotMark und packt es mit Velopack nach .\Releases:
#   ShotMark.App-win-Setup.exe     Installer (pro Benutzer, ohne Adminrechte)
#   ShotMark.App-win-Portable.zip  ohne Installation lauffähig
#   *.nupkg, releases.win.json     Update-Pakete und Feed für die automatische Aktualisierung
#
#   powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -Version 1.1.0
#
# Für Delta-Updates vorher die letzte veröffentlichte Version nach .\Releases holen:
#   dotnet vpk download github --repoUrl https://github.com/pascalsz/shotmark
# Veröffentlicht wird über GitHub Actions (.github/workflows/release.yml), sobald ein Tag v1.1.0 gepusht wird.
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'publish'
$releases = Join-Path $repo 'Releases'
$project = Join-Path $repo 'src\ShotMark.App\ShotMark.App.csproj'
$icon = Join-Path $repo 'src\ShotMark.App\Assets\ShotMark.ico'

if (Test-Path $out) { Remove-Item $out -Recurse -Force }

# Mehrere Dateien statt Single-File: kleinere Delta-Updates und kein Entpacken beim Start
dotnet publish $project -c Release -r win-x64 --self-contained true -p:Version=$Version -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }

Push-Location $repo
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore ist fehlgeschlagen.' }
    # Paket-Id ShotMark.App (nicht ShotMark): siehe src\ShotMark.App\Updates\Installation.cs
    dotnet vpk pack --packId ShotMark.App --packVersion $Version --packDir $out --mainExe ShotMark.exe `
        --packTitle ShotMark --packAuthors 'Pascal Szorath' --icon $icon --shortcuts StartMenuRoot --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack ist fehlgeschlagen.' }
}
finally {
    Pop-Location
}

Get-ChildItem $releases -File | Where-Object Name -like "*$Version*" | ForEach-Object { '{0,8:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name }
Get-ChildItem $releases -File -Filter '*Setup.exe' | ForEach-Object { '{0,8:N1} MB  {1}' -f ($_.Length / 1MB), $_.Name }
