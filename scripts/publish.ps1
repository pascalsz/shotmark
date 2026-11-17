# Baut ShotMark.exe als eigenstaendige Einzeldatei und legt sie mit Installieren.cmd
# in <Dropbox>\Software\ShotMark ab, damit jeder PC sie von dort installieren kann.
#   powershell -ExecutionPolicy Bypass -File scripts\publish.ps1            (bauen + in Dropbox kopieren)
#   powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -NoDropbox (nur bauen nach .\publish)
param([switch]$NoDropbox)
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$out = Join-Path $repo 'publish'
$project = Join-Path $repo 'src\ShotMark.App\ShotMark.App.csproj'

# Unkomprimiert: Datei groesser (~170 MB), aber nur ~60 statt ~140 MB Arbeitsspeicher im Dauerbetrieb
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false -p:DebugType=none -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }

Copy-Item (Join-Path $repo 'deploy\Installieren.cmd') $out -Force
$exe = Join-Path $out 'ShotMark.exe'
'{0:N1} MB  {1}' -f ((Get-Item $exe).Length / 1MB), $exe

if ($NoDropbox) { return }

# Dropbox-Ordner genauso ermitteln wie die App (info.json, erst privat, dann Business)
$dropbox = $null
foreach ($info in @("$env:LOCALAPPDATA\Dropbox\info.json", "$env:APPDATA\Dropbox\info.json")) {
    if (-not (Test-Path $info)) { continue }
    $json = Get-Content $info -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($account in 'personal', 'business') {
        $path = $json.$account.path
        if ($path -and (Test-Path $path)) { $dropbox = $path; break }
    }
    if ($dropbox) { break }
}
if (-not $dropbox) {
    Write-Warning 'Dropbox wurde nicht gefunden - ShotMark liegt nur im Ordner publish.'
    return
}

$target = Join-Path $dropbox 'Software\ShotMark'
New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item $exe, (Join-Path $out 'Installieren.cmd') $target -Force
"Kopiert nach $target"
