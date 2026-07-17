#Requires -Version 7.0
<#
.SYNOPSIS
    Baut die Solution einmal komplett und führt danach jeden einzelnen Test aus allen Testprojekten
    separat als eigenen "dotnet test"-Aufruf aus.

.DESCRIPTION
    Ablauf:
      1. Vollständiger "dotnet build" der Solution (bricht bei Fehlern sofort ab, es wird kein
         einziger Test ausgeführt, wenn der Build nicht sauber durchläuft).
      2. Alle Testprojekte werden ermittelt (jedes .csproj mit Referenz auf Microsoft.NET.Test.Sdk).
      3. Pro Projekt werden alle Tests via "dotnet test --list-tests" ermittelt.
      4. Jeder stabile Test-Filter wird einzeln über "dotnet test --filter FullyQualifiedName=..."
         ausgeführt (mit --no-build, damit zwischen den einzelnen Tests kein Rebuild passiert).
         xUnit-Theory-Datenzeilen aus "--list-tests" sind Displaynamen und werden methodenbasiert
         gruppiert, weil einzelne Datenzeilen nicht stabil per FullyQualifiedName adressierbar sind.
      5. Für jeden Test wird das Ergebnis (TRX-Logger) ausgewertet: Bestanden / Fehlgeschlagen /
         Ausführungsfehler (Testhost-Absturz, fehlende Build-Artefakte o.ä. statt einer echten
         Assertion). Bei einem Ausführungsfehler wird die Solution neu gebaut und der betroffene
         Test genau einmal wiederholt.
      6. Alle Ergebnisse werden in einer Markdown-Zusammenfassung sowie den rohen TRX-Dateien unter
         test-results/<Zeitstempel>/ abgelegt.

    Hinweis zur Laufzeit: Jeder Test startet einen eigenen Testhost-Prozess. Das ist bei vielen
    Tests spürbar langsamer als ein einzelner "dotnet test"-Lauf über die ganze Suite, isoliert
    dafür aber jeden Test vollständig (keine gegenseitige Beeinflussung durch Zustand oder
    ThreadPool-Druck aus anderen Tests).

.PARAMETER Configuration
    Build-Konfiguration (Standard: Debug).

.PARAMETER Filter
    Optionaler Teilstring-Filter (Groß-/Kleinschreibung wird ignoriert) auf den vollqualifizierten
    Testnamen. Nur passende Tests werden ausgeführt. Leer = alle Tests.

.PARAMETER TestSet
    Testgruppe, die ausgeführt wird: Regular (Category!=OsInterface), OsInterface (Category=OsInterface)
    oder All (kein Kategorie-Filter). Standard: Regular.

.PARAMETER ResultsDirectory
    Zielverzeichnis für die Ergebnisdateien. Standard: test-results/<Zeitstempel> im Repo-Root.

.EXAMPLE
    ./scripts/Run-AllTestsIndividually.ps1

.EXAMPLE
    ./scripts/Run-AllTestsIndividually.ps1 -TestSet OsInterface -Filter "PseudoConsole"
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Debug",
    [string]$Filter = "",
    [ValidateSet("Regular", "OsInterface", "All")]
    [string]$TestSet = "Regular",
    [string]$ResultsDirectory = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    $timestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
    if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
        $ResultsDirectory = Join-Path $repoRoot "test-results/$timestamp"
    }
    New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
    $trxDirectory = Join-Path $ResultsDirectory "trx"
    New-Item -ItemType Directory -Force -Path $trxDirectory | Out-Null
    $summaryPath = Join-Path $ResultsDirectory "summary.md"
    $buildLogPath = Join-Path $ResultsDirectory "build.log"

    # Baut jedes Testprojekt einzeln ueber sein eigenes csproj (nicht die Solution-Datei): Ein
    # "dotnet build" der .slnx hat sich als unzuverlaessig herausgestellt (MSBuild-Scheduling-Race
    # zwischen abhaengigen Projekten fuehrte zu sporadischen MSB3030-Kopierfehlern). Der Build ueber
    # das jeweilige Testprojekt-csproj löst die ProjectReference-Kette dagegen zuverlaessig in der
    # richtigen Reihenfolge auf, da dotnet dabei den Abhaengigkeitsgraphen ausgehend von diesem
    # Projekt aufloest statt Solution-weit zu parallelisieren.
    function Invoke-FullBuild {
        param([System.IO.FileInfo[]]$Projects)
        $ok = $true
        foreach ($project in $Projects) {
            Write-Host "==> Baue $($project.Name) ($Configuration)..." -ForegroundColor Cyan
            $output = & dotnet build $project.FullName -c $Configuration 2>&1
            $output | Out-File -FilePath $buildLogPath -Append -Encoding utf8
            if ($LASTEXITCODE -ne 0) {
                Write-Host "==> BUILD FEHLGESCHLAGEN ($($project.Name), Exit-Code $LASTEXITCODE)." -ForegroundColor Red
                $output | Select-Object -Last 40 | ForEach-Object { Write-Host $_ }
                $ok = $false
            }
        }
        if ($ok) { Write-Host "==> Build erfolgreich." -ForegroundColor Green }
        return $ok
    }

    # 1. Testprojekte finden: jedes csproj mit Referenz auf Microsoft.NET.Test.Sdk
    $testProjects = Get-ChildItem -Path $repoRoot -Filter "*.csproj" -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
        Where-Object { (Get-Content $_.FullName -Raw) -match "Microsoft\.NET\.Test\.Sdk" }

    if (-not $testProjects) {
        throw "Keine Testprojekte gefunden (kein csproj mit Microsoft.NET.Test.Sdk-Referenz)."
    }

    Write-Host "==> Gefundene Testprojekte:" -ForegroundColor Cyan
    $testProjects | ForEach-Object { Write-Host "    $($_.FullName)" }

    # 2. Initialer Build - bricht bei Fehler komplett ab, es wird kein Test ausgefuehrt
    if (-not (Invoke-FullBuild -Projects $testProjects)) {
        throw "Initialer Build fehlgeschlagen. Siehe $buildLogPath. Breche ab, ohne Tests auszufuehren."
    }

    function Get-CategoryFilterExpression {
        switch ($TestSet) {
            "Regular" { return "Category!=OsInterface" }
            "OsInterface" { return "Category=OsInterface" }
            default { return "" }
        }
    }

    function Join-TestFilter {
        param([string]$NameFilter, [string]$CategoryFilter)
        if ([string]::IsNullOrWhiteSpace($CategoryFilter)) {
            return $NameFilter
        }
        return "($NameFilter)&($CategoryFilter)"
    }

    $categoryFilterExpression = Get-CategoryFilterExpression

    function New-TestTarget {
        param(
            [string]$FilterName,
            [string]$DisplayName,
            [string]$ExecutionMode,
            [int]$DiscoveryCount
        )
        return [PSCustomObject]@{
            FilterName     = $FilterName
            DisplayName    = $DisplayName
            ExecutionMode  = $ExecutionMode
            DiscoveryCount = $DiscoveryCount
        }
    }

    function Get-TestTargets {
        param([string]$ProjectPath)
        if ([string]::IsNullOrWhiteSpace($categoryFilterExpression)) {
            $listOutput = & dotnet test $ProjectPath -c $Configuration --no-build --list-tests 2>&1
        }
        else {
            $listOutput = & dotnet test $ProjectPath -c $Configuration --no-build --list-tests --filter $categoryFilterExpression 2>&1
        }
        $targetsByFilterName = [ordered]@{}
        foreach ($line in $listOutput) {
            $trimmed = "$line".Trim()
            if ($trimmed -match '^([A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+)\(.+\)$') {
                $methodName = $matches[1]
                if (-not $targetsByFilterName.Contains($methodName)) {
                    $targetsByFilterName[$methodName] = New-TestTarget `
                        -FilterName $methodName `
                        -DisplayName "$methodName (alle entdeckten Theory-Datenzeilen)" `
                        -ExecutionMode "TheoryMethod" `
                        -DiscoveryCount 0
                }
                $targetsByFilterName[$methodName].DiscoveryCount++
            }
            elseif ($trimmed -match '^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)+$') {
                if (-not $targetsByFilterName.Contains($trimmed)) {
                    $targetsByFilterName[$trimmed] = New-TestTarget `
                        -FilterName $trimmed `
                        -DisplayName $trimmed `
                        -ExecutionMode "Individual" `
                        -DiscoveryCount 1
                }
            }
        }
        return @($targetsByFilterName.Values)
    }

    function Get-TrxResult {
        param([string]$TrxPath)
        if (-not (Test-Path $TrxPath)) { return $null }
        try {
            [xml]$trx = Get-Content $TrxPath -Raw
        }
        catch {
            return $null
        }
        $ns = [System.Xml.XmlNamespaceManager]::new($trx.NameTable)
        $ns.AddNamespace("t", "http://microsoft.com/schemas/VisualStudio/TeamTest/2010")
        $resultNodes = @($trx.SelectNodes("//t:UnitTestResult", $ns))
        if ($resultNodes.Count -eq 0) { return $null }
        $result = $resultNodes | Where-Object { $_.outcome -eq "Failed" } | Select-Object -First 1
        if (-not $result) { $result = $resultNodes[0] }
        $message = $null
        $stack = $null
        $msgNode = $result.SelectSingleNode("t:Output/t:ErrorInfo/t:Message", $ns)
        if ($msgNode) { $message = $msgNode.InnerText }
        $stackNode = $result.SelectSingleNode("t:Output/t:ErrorInfo/t:StackTrace", $ns)
        if ($stackNode) { $stack = $stackNode.InnerText }
        $outcome = if (($resultNodes | Where-Object { $_.outcome -eq "Failed" }).Count -gt 0) {
            "Failed"
        }
        elseif (($resultNodes | Where-Object { $_.outcome -ne "Passed" }).Count -gt 0) {
            ($resultNodes | Where-Object { $_.outcome -ne "Passed" } | Select-Object -First 1).outcome
        }
        else {
            "Passed"
        }
        return [PSCustomObject]@{
            Outcome    = $outcome
            Duration   = if ($resultNodes.Count -eq 1) { $result.duration } else { "$($resultNodes.Count) Ergebnisse" }
            Message    = $message
            StackTrace = $stack
            ResultCount = $resultNodes.Count
        }
    }

    # Textmuster, die auf einen Infrastruktur-/Ausfuehrungsfehler hindeuten statt auf eine echte
    # Testfehlschlag-Assertion (Testhost-Absturz, fehlende Build-Artefakte, Datei-Sperren o.ae.).
    $runtimeErrorMarkers = @(
        "A fatal error was encountered",
        "hostpolicy.dll",
        "wurde nicht gefunden. Bitte zuerst das App-Projekt bauen",
        "Es konnte kein Test Host gestartet werden",
        "Unable to start test host",
        "Testhost-Prozess",
        "wird von einem anderen Prozess verwendet",
        "being used by another process"
    )

    function Test-LooksLikeRuntimeError {
        param([string[]]$Output, [object]$TrxResult)
        if (-not $TrxResult) { return $true }
        $joined = ($Output -join "`n")
        foreach ($marker in $runtimeErrorMarkers) {
            if ($joined -match [regex]::Escape($marker)) { return $true }
        }
        return $false
    }

    function Invoke-SingleTest {
        param([string]$ProjectPath, [object]$TestTarget)

        $safeName = ($TestTarget.FilterName -replace '[^A-Za-z0-9_.]', '_')
        $trxFileName = "$safeName.trx"
        $trxFullPath = Join-Path $trxDirectory $trxFileName
        if (Test-Path $trxFullPath) { Remove-Item $trxFullPath -Force }

        $dotnetFilter = Join-TestFilter -NameFilter "FullyQualifiedName=$($TestTarget.FilterName)" -CategoryFilter $categoryFilterExpression
        $output = & dotnet test $ProjectPath -c $Configuration --no-build `
            --filter $dotnetFilter `
            --logger "trx;LogFileName=$trxFileName" `
            --results-directory $trxDirectory `
            --logger "console;verbosity=quiet" 2>&1

        $trxResult = Get-TrxResult -TrxPath $trxFullPath
        $isRuntimeError = Test-LooksLikeRuntimeError -Output $output -TrxResult $trxResult

        return [PSCustomObject]@{
            Output         = $output
            Trx            = $trxResult
            IsRuntimeError = $isRuntimeError
        }
    }

    # 4. Alle Tests aus allen Projekten einsammeln (optional gefiltert). xUnit-Theory-Datenzeilen
    # werden als entdeckte Tests gezaehlt, aber methodenbasiert ausgefuehrt: Der Displayname einer
    # einzelnen Datenzeile ist nicht stabil als FullyQualifiedName-Filter adressierbar.
    $allTests = [System.Collections.Generic.List[object]]::new()
    foreach ($project in $testProjects) {
        Write-Host "==> Ermittle Tests: $($project.Name)" -ForegroundColor Cyan
        $targets = Get-TestTargets -ProjectPath $project.FullName
        foreach ($target in $targets) {
            if ([string]::IsNullOrWhiteSpace($Filter) -or ($target.DisplayName -like "*$Filter*") -or ($target.FilterName -like "*$Filter*")) {
                $allTests.Add([PSCustomObject]@{ Project = $project; TestTarget = $target })
            }
        }
    }

    if ($allTests.Count -eq 0) {
        throw "Keine Tests gefunden (ggf. Filter '$Filter' zu eng)."
    }

    $testSetLabel = switch ($TestSet) {
        "Regular" { "regulaere Tests (Category!=OsInterface)" }
        "OsInterface" { "OS-Schnittstellen-Tests (Category=OsInterface)" }
        default { "alle Tests" }
    }
    $discoveredTests = ($allTests | ForEach-Object { $_.TestTarget.DiscoveryCount } | Measure-Object -Sum).Sum
    $theoryTargets = @($allTests | Where-Object { $_.TestTarget.ExecutionMode -eq "TheoryMethod" })
    $groupedTheoryRows = ($theoryTargets | ForEach-Object { $_.TestTarget.DiscoveryCount } | Measure-Object -Sum).Sum
    Write-Host "==> $discoveredTests entdeckte Tests werden ueber $($allTests.Count) stabile Filter ausgefuehrt: $testSetLabel." -ForegroundColor Cyan
    if ($theoryTargets.Count -gt 0) {
        Write-Host "==> $groupedTheoryRows Theory-Datenzeilen laufen bewusst gruppiert ueber $($theoryTargets.Count) Methodennamen." -ForegroundColor Cyan
    }

    # 5. Jeden Test einzeln ausfuehren, inkl. Retry-nach-Rebuild bei Ausfuehrungsfehlern
    $results = [System.Collections.Generic.List[object]]::new()
    $index = 0
    foreach ($entry in $allTests) {
        $index++
        Write-Host "[$index/$($allTests.Count)] $($entry.TestTarget.DisplayName)" -NoNewline

        $run = Invoke-SingleTest -ProjectPath $entry.Project.FullName -TestTarget $entry.TestTarget
        $retried = $false

        if ($run.IsRuntimeError) {
            Write-Host " -> Ausfuehrungsfehler, baue neu und wiederhole einmal..." -ForegroundColor Yellow
            $rebuildOk = Invoke-FullBuild -Projects $testProjects
            $retried = $true
            if ($rebuildOk) {
                $run = Invoke-SingleTest -ProjectPath $entry.Project.FullName -TestTarget $entry.TestTarget
            }
        }

        $status = if ($run.Trx) {
            switch ($run.Trx.Outcome) {
                "Passed" { "Bestanden" }
                "Failed" { "Fehlgeschlagen" }
                default { "Unbekannt ($($run.Trx.Outcome))" }
            }
        }
        elseif ($run.IsRuntimeError) {
            "Ausfuehrungsfehler"
        }
        else {
            "Unbekannt"
        }

        $color = switch -Wildcard ($status) {
            "Bestanden" { "Green" }
            "Fehlgeschlagen" { "Red" }
            "Ausfuehrungsfehler" { "DarkRed" }
            default { "Yellow" }
        }
        Write-Host " -> $status" -ForegroundColor $color

        $results.Add([PSCustomObject]@{
            Project    = $entry.Project.Name
            TestSet    = $TestSet
            TestName   = $entry.TestTarget.DisplayName
            FilterName = $entry.TestTarget.FilterName
            ExecutionMode = $entry.TestTarget.ExecutionMode
            DiscoveryCount = $entry.TestTarget.DiscoveryCount
            Status     = $status
            Duration   = if ($run.Trx) { $run.Trx.Duration } else { $null }
            Message    = if ($run.Trx) { $run.Trx.Message } else { $null }
            StackTrace = if ($run.Trx) { $run.Trx.StackTrace } else { $null }
            Retried    = $retried
        })
    }

    # 6. Ergebnisse schreiben
    $passed = ($results | Where-Object { $_.Status -eq "Bestanden" }).Count
    $failed = ($results | Where-Object { $_.Status -eq "Fehlgeschlagen" }).Count
    $execErrors = ($results | Where-Object { $_.Status -eq "Ausfuehrungsfehler" }).Count
    $unknown = ($results | Where-Object { $_.Status -notin @("Bestanden", "Fehlgeschlagen", "Ausfuehrungsfehler") }).Count

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# Testergebnisse ($timestamp)")
    $lines.Add("")
    $lines.Add("Testprojekte: $(($testProjects.Name) -join ', '), Konfiguration: $Configuration")
    $lines.Add("Testgruppe: $testSetLabel")
    if ($categoryFilterExpression) { $lines.Add("Kategorie-Filter: $categoryFilterExpression") }
    if ($Filter) { $lines.Add("Filter: $Filter") }
    $lines.Add("")
    $lines.Add("## Zusammenfassung")
    $lines.Add("")
    $lines.Add("- Entdeckte Tests: $discoveredTests")
    $lines.Add("- Ausgefuehrte Filter: $($results.Count)")
    if ($theoryTargets.Count -gt 0) {
        $lines.Add("- Gruppierte Theory-Datenzeilen: $groupedTheoryRows ueber $($theoryTargets.Count) Methodennamen")
        $lines.Add("- Hinweis: Einzelne xUnit-Theory-Datenzeilen aus ``dotnet test --list-tests`` sind Displaynamen und werden methodenbasiert ausgefuehrt, damit sie nicht stillschweigend ausgelassen werden.")
    }
    $lines.Add("- Bestanden: $passed")
    if ($TestSet -eq "OsInterface") {
        $lines.Add("- OS-Schnittstellen-Fehler: $failed")
    }
    else {
        $lines.Add("- Regulaere Fehler: $failed")
    }
    $lines.Add("- Infrastruktur-/Ausfuehrungsfehler: $execErrors")
    if ($unknown -gt 0) { $lines.Add("- Unbekannt: $unknown") }
    $lines.Add("")

    if ($failed -gt 0 -or $execErrors -gt 0) {
        $lines.Add("## Fehlgeschlagene Tests")
        $lines.Add("")
        foreach ($r in ($results | Where-Object { $_.Status -ne "Bestanden" })) {
            $lines.Add("### $($r.Project) - $($r.TestName)")
            $lines.Add("")
            $retriedNote = if ($r.Retried) { " (nach Wiederholung)" } else { "" }
            $lines.Add("- Status: $($r.Status)$retriedNote")
            if ($r.Message) {
                $flatMessage = ($r.Message -replace "`r?`n", " ")
                $lines.Add("- Meldung: $flatMessage")
            }
            $lines.Add("")
        }
    }

    $lines.Add("## Alle Tests")
    $lines.Add("")
    $lines.Add("| Projekt | Test | Ausfuehrung | Entdeckte Tests | Status | Dauer |")
    $lines.Add("|---|---|---|---|---|---|")
    foreach ($r in $results) {
        $execution = if ($r.ExecutionMode -eq "TheoryMethod") { "Theory-Methode" } else { "Einzeltest" }
        $lines.Add("| $($r.Project) | $($r.TestName) | $execution | $($r.DiscoveryCount) | $($r.Status) | $($r.Duration) |")
    }

    $lines | Out-File -FilePath $summaryPath -Encoding utf8

    Write-Host ""
    Write-Host "==> Fertig. Bestanden: $passed, Fehlgeschlagen: $failed, Ausfuehrungsfehler: $execErrors" -ForegroundColor Cyan
    Write-Host "==> Ergebnisse: $summaryPath" -ForegroundColor Cyan

    if ($failed -gt 0 -or $execErrors -gt 0) {
        exit 1
    }
    exit 0
}
finally {
    Pop-Location
}
