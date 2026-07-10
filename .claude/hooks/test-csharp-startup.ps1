[CmdletBinding()]
param()

# Am Git-Repo-Root verankern, damit der Build-Lock (siehe unten) exakt
# denselben Pfad trifft wie dotnet_lock.py (PreToolUse-Hook vor "dotnet test").
try {
    $repoRoot = (git rev-parse --show-toplevel 2>$null)
    if ($repoRoot) { Set-Location $repoRoot }
} catch {}

$WorkDir    = $PWD
$TimeoutSec = 30

# ---- Lock: verhindert, dass dieser Stop-Hook (Build + Start-Smoke-Test)
# gleichzeitig mit einem "dotnet test"-Lauf (build_before_test.py) in
# dieselben obj/bin-Ordner schreibt. Sonst koennen z.B. WPF-Projekte ein
# unvollstaendiges runtimeconfig.json bekommen ("Desktop Runtime nicht
# gefunden" bei E2E-Tests, nur manchmal, je nach Timing).
$LockDir      = Join-Path $WorkDir '.claude\.locks\dotnet-build.lock'
$LockTimeout  = 120
$LockStaleSec = 300

function Enter-DotnetBuildLock {
    $parent = Split-Path $LockDir -Parent
    if (-not (Test-Path $parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }

    $deadline = [DateTime]::UtcNow.AddSeconds($LockTimeout)
    while ($true) {
        try {
            New-Item -ItemType Directory -Path $LockDir -ErrorAction Stop | Out-Null
            Set-Content -Path (Join-Path $LockDir 'owner.txt') -Value "pid=$PID time=$(Get-Date -Format o)"
            return $true
        } catch {
            if (Test-Path $LockDir) {
                $age = ((Get-Date) - (Get-Item $LockDir).CreationTime).TotalSeconds
                if ($age -gt $LockStaleSec) {
                    Remove-Item $LockDir -Recurse -Force -ErrorAction SilentlyContinue
                    continue
                }
            }
            if ([DateTime]::UtcNow -ge $deadline) {
                Write-Host "[dotnet-lock] Timeout beim Warten auf den Build-Lock - fahre ohne Lock fort." -ForegroundColor Yellow
                return $false
            }
            Start-Sleep -Milliseconds 500
        }
    }
}

function Exit-DotnetBuildLock {
    Remove-Item $LockDir -Recurse -Force -ErrorAction SilentlyContinue
}

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

$gotLock = Enter-DotnetBuildLock
try {
    $results = $projects | ForEach-Object { Test-Startup -Project $_ }
} finally {
    if ($gotLock) { Exit-DotnetBuildLock }
}

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
