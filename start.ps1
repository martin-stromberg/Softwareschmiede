Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExitCodeSuccess = 0
$ExitCodeLaunchSettingsMissing = 10
$ExitCodeInvalidLaunchSettings = 11
$ExitCodeInvalidOrUnavailablePort = 12
$ExitCodeWriteFailed = 13
$ExitCodeUnexpectedError = 99

$ProfileName = 'http'
$IgnoredDirectoryNames = @('.git', 'bin', 'obj', 'TestResults', 'node_modules')
$RunId = [Guid]::NewGuid().ToString('N')
$AtomicWriteRetryCount = 3
$AtomicWriteRetryDelayMilliseconds = 100

function Write-Diagnostic {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('INFO', 'WARN', 'ERROR')]
        [string]$Level,

        [Parameter(Mandatory = $true)]
        [int]$Code,

        [Parameter(Mandatory = $true)]
        [string]$Message,

        [string]$RunIdValue = '',

        [string]$ProjectPath = '',

        [string]$LaunchSettingsPath = '',

        [Nullable[int]]$Port = $null
    )

    $timestamp = [DateTimeOffset]::Now.ToString('yyyy-MM-ddTHH:mm:ss.fffK')
    $runPart = if (-not [string]::IsNullOrWhiteSpace($RunIdValue)) { " [RUN:$RunIdValue]" } else { '' }
    $projectPart = if (-not [string]::IsNullOrWhiteSpace($ProjectPath)) { " [PROJECT:$ProjectPath]" } else { '' }
    $filePart = if (-not [string]::IsNullOrWhiteSpace($LaunchSettingsPath)) { " [FILE:$LaunchSettingsPath]" } else { '' }
    $portValue = if ($null -ne $Port) { "$Port" } else { 'n/a' }
    $portPart = " [PORT:$portValue]"
    Write-Host "[$timestamp] [$Level] [CODE:$Code]$runPart$projectPart$filePart$portPart $Message"
}

function Resolve-RepositoryRootPath {
    if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return [System.IO.Path]::GetFullPath((Get-Location).Path)
    }

    return [System.IO.Path]::GetFullPath($PSScriptRoot)
}

function Test-IsJsonObject {
    param([object]$Value)

    return $null -ne $Value -and (
        $Value -is [pscustomobject] -or
        $Value -is [System.Collections.IDictionary]
    )
}

function Get-JsonPropertyValue {
    param(
        [object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary]) {
        if ($InputObject.Contains($PropertyName)) {
            return $InputObject[$PropertyName]
        }

        return $null
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Set-JsonPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName,
        [AllowNull()]
        [object]$Value
    )

    if ($InputObject -is [System.Collections.IDictionary]) {
        $InputObject[$PropertyName] = $Value
        return
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        Add-Member -InputObject $InputObject -MemberType NoteProperty -Name $PropertyName -Value $Value -Force
        return
    }

    $property.Value = $Value
}

function Test-PortIsAvailable {
    param(
        [int]$CandidatePort
    )

    if ($CandidatePort -lt 1 -or $CandidatePort -gt 65535) {
        return $false
    }

    $listener = $null
    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $CandidatePort)
        $listener.Server.ExclusiveAddressUse = $true
        $listener.Start()
        return $true
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $listener) {
            $listener.Stop()
        }
    }
}

function Resolve-HostForHttpProfile {
    param(
        [string]$CurrentApplicationUrl
    )

    function Test-IsVisualStudioDebugCompatibleHost {
        param(
            [string]$HostName
        )

        if ([string]::IsNullOrWhiteSpace($HostName)) {
            return $false
        }

        if ($HostName.Equals('localhost', [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }

        $parsedIpAddress = $null
        if ([System.Net.IPAddress]::TryParse($HostName, [ref]$parsedIpAddress)) {
            return [System.Net.IPAddress]::IsLoopback($parsedIpAddress)
        }

        return $false
    }

    if (-not [string]::IsNullOrWhiteSpace($CurrentApplicationUrl)) {
        $candidate = $CurrentApplicationUrl.Split(';') | Where-Object { $_ -match '^http://' } | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($candidate)) {
            $uri = $null
            if ([System.Uri]::TryCreate($candidate, [System.UriKind]::Absolute, [ref]$uri)) {
                if (Test-IsVisualStudioDebugCompatibleHost -HostName $uri.Host) {
                    return $uri.Host
                }
            }
        }
    }

    return 'localhost'
}

function Resolve-UniqueFreeHttpPort {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.HashSet[int]]$AssignedPorts
    )

    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $listener = $null
        try {
            $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
            $listener.Server.ExclusiveAddressUse = $true
            $listener.Start()
            $candidate = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
        }
        finally {
            if ($null -ne $listener) {
                $listener.Stop()
            }
        }

        if ($AssignedPorts.Add($candidate)) {
            return $candidate
        }
    }

    throw [System.InvalidOperationException]::new('Could not resolve a unique free HTTP port.')
}

function Get-ExitPriority {
    param([int]$Code)
    switch ($Code) {
        13 { return 5 }
        12 { return 4 }
        11 { return 3 }
        10 { return 2 }
        0 { return 1 }
        default { return 0 }
    }
}

function Update-AggregatedExitCode {
    param(
        [int]$CurrentCode,
        [int]$NextCode
    )

    if ((Get-ExitPriority -Code $NextCode) -gt (Get-ExitPriority -Code $CurrentCode)) {
        return $NextCode
    }

    return $CurrentCode
}

function Is-IgnoredPath {
    param([string]$PathValue)

    $segments = $PathValue.Split([char[]]@('\', '/'), [System.StringSplitOptions]::RemoveEmptyEntries)
    foreach ($segment in $segments) {
        if ($IgnoredDirectoryNames -contains $segment) {
            return $true
        }
    }
    return $false
}

function Find-LaunchSettingsTargets {
    param([string]$RepositoryRoot)

    return @(Get-ChildItem -LiteralPath $RepositoryRoot -Filter 'launchSettings.json' -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object {
            $_.FullName -like '*\Properties\launchSettings.json' -and
            -not (Is-IgnoredPath -PathValue $_.FullName)
        } |
        Sort-Object FullName)
}

function Process-LaunchSettingsFile {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$LaunchSettingsFile,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.HashSet[int]]$AssignedPorts
    )

    $projectPath = Split-Path -Parent (Split-Path -Parent $LaunchSettingsFile.FullName)

    try {
        $rawJson = Get-Content -LiteralPath $LaunchSettingsFile.FullName -Raw -Encoding UTF8
    }
    catch {
        return @{
            ExitCode = $ExitCodeLaunchSettingsMissing
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "launchSettings.json could not be read."
            Port = $null
        }
    }

    try {
        $launchSettings = $rawJson | ConvertFrom-Json
    }
    catch {
        return @{
            ExitCode = $ExitCodeInvalidLaunchSettings
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "launchSettings.json is invalid JSON."
            Port = $null
        }
    }

    if (-not (Test-IsJsonObject -Value $launchSettings)) {
        return @{
            ExitCode = $ExitCodeInvalidLaunchSettings
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "launchSettings.json root must be an object."
            Port = $null
        }
    }

    $profiles = Get-JsonPropertyValue -InputObject $launchSettings -PropertyName 'profiles'
    if (-not (Test-IsJsonObject -Value $profiles)) {
        return @{
            ExitCode = $ExitCodeInvalidLaunchSettings
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Property 'profiles' is missing or invalid in launchSettings.json."
            Port = $null
        }
    }

    $httpProfile = Get-JsonPropertyValue -InputObject $profiles -PropertyName $ProfileName
    if ($null -eq $httpProfile) {
        return @{
            ExitCode = $ExitCodeInvalidLaunchSettings
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Profile '$ProfileName' is missing in launchSettings.json."
            Port = $null
        }
    }

    if (-not (Test-IsJsonObject -Value $httpProfile)) {
        return @{
            ExitCode = $ExitCodeInvalidLaunchSettings
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Profile '$ProfileName' in launchSettings.json must be an object."
            Port = $null
        }
    }

    $currentUrlValue = Get-JsonPropertyValue -InputObject $httpProfile -PropertyName 'applicationUrl'
    $currentUrl = if ($null -eq $currentUrlValue) { '' } else { [string]$currentUrlValue }
    $resolvedHost = Resolve-HostForHttpProfile -CurrentApplicationUrl $currentUrl
    $resolvedPort = $null
    try {
        $resolvedPort = Resolve-UniqueFreeHttpPort -AssignedPorts $AssignedPorts
    }
    catch {
        return @{
            ExitCode = $ExitCodeInvalidOrUnavailablePort
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Could not resolve a free HTTP port."
            Port = $null
        }
    }

    if (-not (Test-PortIsAvailable -CandidatePort $resolvedPort)) {
        return @{
            ExitCode = $ExitCodeInvalidOrUnavailablePort
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Resolved port '$resolvedPort' is invalid or not available."
            Port = $resolvedPort
        }
    }

    $newUrl = "http://${resolvedHost}:$resolvedPort"
    Set-JsonPropertyValue -InputObject $httpProfile -PropertyName 'applicationUrl' -Value $newUrl

    $targetDirectory = Split-Path -Parent $LaunchSettingsFile.FullName
    $fileName = Split-Path -Leaf $LaunchSettingsFile.FullName
    $tempPath = Join-Path -Path $targetDirectory -ChildPath "$fileName.tmp"

    $fileAttributes = [System.IO.File]::GetAttributes($LaunchSettingsFile.FullName)
    if (($fileAttributes -band [System.IO.FileAttributes]::ReadOnly) -eq [System.IO.FileAttributes]::ReadOnly) {
        return @{
            ExitCode = $ExitCodeWriteFailed
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "launchSettings.json is read-only and cannot be updated."
            Port = $resolvedPort
        }
    }

    $writeSucceeded = $false
    $jsonContent = $launchSettings | ConvertTo-Json -Depth 100
    for ($attempt = 1; $attempt -le $AtomicWriteRetryCount; $attempt++) {
        try {
            [System.IO.File]::WriteAllText($tempPath, "$jsonContent`r`n", [System.Text.Encoding]::UTF8)
            Move-Item -LiteralPath $tempPath -Destination $LaunchSettingsFile.FullName -Force
            $writeSucceeded = $true
            break
        }
        catch {
            if (Test-Path -LiteralPath $tempPath) {
                Remove-Item -LiteralPath $tempPath -Force -ErrorAction SilentlyContinue
            }

            if ($attempt -lt $AtomicWriteRetryCount) {
                Start-Sleep -Milliseconds $AtomicWriteRetryDelayMilliseconds
            }
        }
    }

    if (-not $writeSucceeded) {
        return @{
            ExitCode = $ExitCodeWriteFailed
            ProjectPath = $projectPath
            LaunchSettingsPath = $LaunchSettingsFile.FullName
            Message = "Could not update launchSettings.json atomically after $AtomicWriteRetryCount attempts."
            Port = $resolvedPort
        }
    }

    return @{
        ExitCode = $ExitCodeSuccess
        ProjectPath = $projectPath
        LaunchSettingsPath = $LaunchSettingsFile.FullName
        Message = "Updated '$ProfileName' profile to '$newUrl'."
        Port = $resolvedPort
    }
}

try {
    $repositoryRoot = Resolve-RepositoryRootPath
    $targets = @(Find-LaunchSettingsTargets -RepositoryRoot $repositoryRoot)
    if ($targets.Count -eq 0) {
        Write-Diagnostic -Level 'ERROR' -Code $ExitCodeLaunchSettingsMissing -RunIdValue $RunId -Message "No launchSettings.json targets found under '$repositoryRoot' (expected '*\\Properties\\launchSettings.json')."
        exit $ExitCodeLaunchSettingsMissing
    }

    $assignedPorts = [System.Collections.Generic.HashSet[int]]::new()
    $finalExitCode = $ExitCodeSuccess
    foreach ($target in $targets) {
        $result = Process-LaunchSettingsFile -LaunchSettingsFile $target -AssignedPorts $assignedPorts
        $level = if ($result.ExitCode -eq $ExitCodeSuccess) { 'INFO' } else { 'ERROR' }
        Write-Diagnostic -Level $level -Code $result.ExitCode -RunIdValue $RunId -ProjectPath $result.ProjectPath -LaunchSettingsPath $result.LaunchSettingsPath -Port $result.Port -Message $result.Message
        $finalExitCode = Update-AggregatedExitCode -CurrentCode $finalExitCode -NextCode $result.ExitCode
    }

    if ($finalExitCode -eq $ExitCodeSuccess) {
        Write-Diagnostic -Level 'INFO' -Code $ExitCodeSuccess -RunIdValue $RunId -Message "Processed $($targets.Count) project(s) successfully."
    }
    else {
        Write-Diagnostic -Level 'ERROR' -Code $finalExitCode -RunIdValue $RunId -Message "Processed $($targets.Count) project(s) with failures."
    }

    exit $finalExitCode
}
catch {
    Write-Diagnostic -Level 'ERROR' -Code $ExitCodeUnexpectedError -RunIdValue $RunId -Message "Unexpected error: $($_.Exception.Message)"
    exit $ExitCodeUnexpectedError
}
