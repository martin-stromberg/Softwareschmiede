[CmdletBinding()]
param()

$WorkDir    = $PWD
$TimeoutSec = 30

function Get-RunnableProjects {
    param([string]$Dir)

    Get-ChildItem -Path $Dir -Filter '*.csproj' -Recurse | Where-Object {
        $xml = Get-Content $_.FullName -Raw

        $isTest = $xml -match '<IsTestProject>\s*true\s*</IsTestProject>' -or
                  $xml -match 'Microsoft\.NET\.Test\.Sdk'

        $isRunnable = $xml -match 'Sdk="Microsoft\.NET\.Sdk\.Web"' -or
                      $xml -match '<OutputType>\s*(Exe|WinExe)\s*</OutputType>'

        $isRunnable -and -not $isTest
    }
}

function Stop-ProcessTree {
    param([System.Diagnostics.Process]$Process)
    if ($null -eq $Process -or $Process.HasExited) { return }
    taskkill /F /T /PID $Process.Id 2>$null | Out-Null
}

function Test-Startup {
    param([System.IO.FileInfo]$Project)

    $name  = $Project.BaseName
    $csproj = $Project.FullName
    $dir   = $Project.DirectoryName

    # --- Port-Check: App läuft bereits (z.B. in Visual Studio) ---
    $appPort = $null
    $portMatch = Select-String -Path $csproj -Pattern 'applicationUrl.*:(\d{4,5})' -ErrorAction SilentlyContinue
    if (-not $portMatch) {
        $launchSettings = Join-Path $dir 'Properties\launchSettings.json'
        if (Test-Path $launchSettings) {
            $portMatch = Select-String -Path $launchSettings -Pattern ':(\d{4,5})' -ErrorAction SilentlyContinue
        }
    }
    if ($portMatch) {
        $appPort = [int]($portMatch.Matches[0].Groups[1].Value)
    }
    if ($appPort -and (Test-NetConnection -ComputerName localhost -Port $appPort -InformationLevel Quiet -ErrorAction SilentlyContinue -WarningAction SilentlyContinue)) {
        return [PSCustomObject]@{ Name = $name; Success = $true; Reason = "Läuft bereits auf Port $appPort (übersprungen)"; Detail = '' }
    }

    # --- Build ---
    $buildOut = & dotnet build $csproj --no-incremental -v quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        return [PSCustomObject]@{
            Name    = $name
            Success = $false
            Reason  = 'Build failed'
            Detail  = ($buildOut -split "`n" | Select-Object -Last 15) -join "`n"
        }
    }

    # --- Run ---
    $outFile = [IO.Path]::GetTempFileName()
    $errFile = [IO.Path]::GetTempFileName()
    $proc    = $null

    try {
        $proc = Start-Process dotnet `
            -ArgumentList "run --project `"$csproj`" --no-build" `
            -WorkingDirectory $dir `
            -RedirectStandardOutput $outFile `
            -RedirectStandardError  $errFile `
            -PassThru -WindowStyle Hidden

        $webSignals  = 'Application started', 'Now listening on:', 'Content root path:'
        $deadline    = [DateTime]::UtcNow.AddSeconds($TimeoutSec)
        $lastExamine = ''

        while ([DateTime]::UtcNow -lt $deadline) {
            Start-Sleep -Milliseconds 400

            if ($proc.HasExited) {
                $stdout = Get-Content $outFile -Raw -ErrorAction SilentlyContinue
                $stderr = Get-Content $errFile -Raw -ErrorAction SilentlyContinue

                if ($proc.ExitCode -eq 0) {
                    return [PSCustomObject]@{ Name = $name; Success = $true; Reason = 'Exited cleanly (code 0)'; Detail = '' }
                }

                $detail = (@($stderr, $stdout) | Where-Object { $_ } |
                    ForEach-Object { ($_ -split "`n" | Select-Object -Last 15) -join "`n" }) -join "`n"

                return [PSCustomObject]@{
                    Name    = $name
                    Success = $false
                    Reason  = "Crashed (exit $($proc.ExitCode))"
                    Detail  = $detail
                }
            }

            $stdout = Get-Content $outFile -Raw -ErrorAction SilentlyContinue
            if ($stdout -and ($stdout -ne $lastExamine)) {
                $lastExamine = $stdout
                foreach ($sig in $webSignals) {
                    if ($stdout -match [regex]::Escape($sig)) {
                        return [PSCustomObject]@{ Name = $name; Success = $true; Reason = "Web-App gestartet ('$sig' erkannt)"; Detail = '' }
                    }
                }
            }
        }

        # Timeout ohne Absturz = läuft stabil
        if (-not $proc.HasExited) {
            return [PSCustomObject]@{ Name = $name; Success = $true; Reason = "Läuft nach ${TimeoutSec}s stabil (kein Absturz)"; Detail = '' }
        }

        return [PSCustomObject]@{ Name = $name; Success = $false; Reason = 'Unbekannter Zustand'; Detail = '' }
    }
    finally {
        Stop-ProcessTree -Process $proc
        Remove-Item $outFile, $errFile -ErrorAction SilentlyContinue
    }
}

# ---- Main ----

# Stop-Hook sendet JSON via stdin – lesen und ignorieren
$null = $input

$projects = Get-RunnableProjects -Dir $WorkDir
if (-not $projects) {
    Write-Host 'Keine ausführbaren C#-Projekte gefunden – Test übersprungen.'
    exit 0
}

$names = ($projects | ForEach-Object { $_.BaseName }) -join ', '
Write-Host ""
Write-Host "=== C# Starttest === ($names)"
Write-Host ""

$results = $projects | ForEach-Object { Test-Startup -Project $_ }

foreach ($r in $results) {
    if ($r.Success) {
        Write-Host "[OK]   $($r.Name): $($r.Reason)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $($r.Name): $($r.Reason)" -ForegroundColor Red
        if ($r.Detail) {
            Write-Host ($r.Detail.Trim()) -ForegroundColor DarkGray
        }
    }
}

$failed = @($results | Where-Object { -not $_.Success })
Write-Host ""

if ($failed.Count -gt 0) {
    Write-Host "$($failed.Count) Projekt(e) konnten nicht gestartet werden!" -ForegroundColor Red
    exit 1
}

Write-Host "Alle Projekte erfolgreich gestartet." -ForegroundColor Green
exit 0
