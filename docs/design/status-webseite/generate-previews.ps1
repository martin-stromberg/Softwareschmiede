<#
.SYNOPSIS
  Erzeugt aus den kanonischen Light-Mockups (*.html) die Dark-Varianten
  (*.dark.html) und die Composite-Wrapper (*.composite.html) fuer die
  Light+Dark-Screenshots — JS-frei, zwei iframes nebeneinander.

.DESCRIPTION
  Aufruf:  .\generate-previews.ps1              → nur Dateien erzeugen
           .\generate-previews.ps1 -Screenshots → + Headless-Chrome-PNGs nach
                                                  .\screenshots\ rendern

  Kanonisch sind die Light-Dateien und theme.css/site.css; *.dark.html und
  *.composite.html sind Build-Artefakte und werden bei jedem Lauf neu erzeugt.
  Alle generierten Dateien werden UTF-8 ohne BOM geschrieben (wie die
  kanonischen Dateien). Dieses Skript selbst ist UTF-8 mit BOM gespeichert,
  damit Windows PowerShell 5.1 die Nicht-ASCII-Zeichen korrekt einliest.

  Die Iframe-Hoehe je Seite steht in $Pages.Height (Desktop) bzw.
  $Pages.MobileHeight (nur die zwei Mobil-Nachweis-Seiten). Werte nach
  Inhaltsaenderungen anpassen, bis die PNGs ohne Abschnitt und ohne
  uebermaessigen Leerraum unten rendern.
#>
[CmdletBinding()]
param(
    [switch]$Screenshots,
    [string]$ChromeExe = "C:\Program Files\Google\Chrome\Application\chrome.exe"
)

$ErrorActionPreference = 'Stop'
$dir = $PSScriptRoot
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# Kanonische Mockup-Dateien mit Iframe-Hoehen (px) fuer Desktop-Composite
# und — fuer die zwei Mobil-Nachweise — zusaetzlich MobileHeight.
$Pages = @(
    @{ Name = 'status';                    Height = 2120; MobileHeight = 5060 },
    @{ Name = 'status-leer';               Height = 810 },
    @{ Name = 'login';                     Height = 525 },
    @{ Name = 'register';                  Height = 605 },
    @{ Name = 'profil';                    Height = 900 },
    @{ Name = 'admin-benutzer';            Height = 970; MobileHeight = 1450 },
    @{ Name = 'admin-benutzer-loeschen';   Height = 470 },
    @{ Name = 'admin-tokens';              Height = 840 },
    @{ Name = 'admin-tokens-leer';         Height = 610 },
    @{ Name = 'admin-token-widerrufen';    Height = 470 },
    @{ Name = 'admin-einstellungen';       Height = 1330 },
    @{ Name = 'admin-update-installation'; Height = 420 },
    @{ Name = 'zugriff-verweigert';        Height = 395 },
    @{ Name = 'formular-abgelaufen';       Height = 395 }
)

function New-Composite([string]$Name, [string]$Suffix, [int]$Height) {
    # Titel bewusst ASCII ("-"), damit kein Nicht-ASCII-Zeichen in die
    # Ausgabe gelangt (PS 5.1 / cp1252-Falle).
    $composite = @"
<!doctype html><html lang="de"><head><meta charset="utf-8"><title>$Name - Light + Dark</title>
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
.wrap { display: flex; }
iframe { width: 50%; border: 0; display: block; height: ${Height}px; }
iframe + iframe { border-left: 4px solid dimgray; }
</style></head>
<body><div class="wrap"><iframe src="$Name.html" title="Light"></iframe><iframe src="$Name.dark.html" title="Dark"></iframe></div></body></html>
"@
    [System.IO.File]::WriteAllText((Join-Path $dir "$Name$Suffix.composite.html"), $composite, $utf8NoBom)
}

foreach ($p in $Pages) {
    $name = $p.Name
    $src = Join-Path $dir "$name.html"
    if (-not (Test-Path $src)) { throw "Kanonische Datei fehlt: $src" }

    # Dark-Variante: identischer Inhalt, nur data-theme getauscht.
    $html = Get-Content $src -Raw -Encoding utf8
    if ($html -notmatch 'data-theme="light"') { throw "$name.html ohne data-theme=`"light`"" }
    $dark = $html -replace 'data-theme="light"', 'data-theme="dark"'
    [System.IO.File]::WriteAllText((Join-Path $dir "$name.dark.html"), $dark, $utf8NoBom)

    New-Composite -Name $name -Suffix '' -Height $p.Height
    if ($p.MobileHeight) {
        New-Composite -Name $name -Suffix '.mobil' -Height $p.MobileHeight
    }
    Write-Host "OK $name (dark + composite$(if ($p.MobileHeight) { ' + mobil' }))"
}

if ($Screenshots) {
    if (-not (Test-Path $ChromeExe)) { throw "Chrome nicht gefunden: $ChromeExe" }
    $shotDir = Join-Path $dir 'screenshots'
    New-Item -ItemType Directory -Force -Path $shotDir | Out-Null

    foreach ($p in $Pages) {
        $name = $p.Name
        $target = Join-Path $shotDir "$name.png"
        $source = "file:///$($dir -replace '\\','/')/$name.composite.html"
        & $ChromeExe --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
            --screenshot="$target" --window-size="1920,$($p.Height)" $source | Out-Null
        Write-Host "PNG $name.png"
    }

    foreach ($p in ($Pages | Where-Object { $_.MobileHeight })) {
        $name = $p.Name
        $target = Join-Path $shotDir "$name-mobil.png"
        $source = "file:///$($dir -replace '\\','/')/$name.mobil.composite.html"
        & $ChromeExe --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
            --screenshot="$target" --window-size="920,$($p.MobileHeight)" $source | Out-Null
        Write-Host "PNG $name-mobil.png"
    }
}
