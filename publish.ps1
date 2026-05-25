Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        return [System.IO.Path]::GetFullPath($PSScriptRoot)
    }

    return [System.IO.Path]::GetFullPath((Get-Location).Path)
}

function Get-PublishProfileData {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PublishProfilePath
    )

    if (-not (Test-Path -LiteralPath $PublishProfilePath -PathType Leaf)) {
        throw "Publish profile not found: $PublishProfilePath"
    }

    [xml]$publishProfileXml = Get-Content -LiteralPath $PublishProfilePath -Raw
    $propertyGroup = $publishProfileXml.Project.PropertyGroup

    if ($null -eq $propertyGroup) {
        throw "Invalid publish profile: missing PropertyGroup in $PublishProfilePath"
    }

    $deployIisAppPath = [string]$propertyGroup.DeployIisAppPath
    if ([string]::IsNullOrWhiteSpace($deployIisAppPath)) {
        throw "Invalid publish profile: DeployIisAppPath is empty in $PublishProfilePath"
    }

    return [pscustomobject]@{
        DeployIisAppPath = $deployIisAppPath.Trim()
        ProfileName = [System.IO.Path]::GetFileNameWithoutExtension($PublishProfilePath)
        ProfilePath = $PublishProfilePath
    }
}

function Test-IisApplicationExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DeployIisAppPath
    )

    Import-Module WebAdministration -ErrorAction Stop

    $segments = $DeployIisAppPath.Split('/', [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Length -lt 1) {
        throw "DeployIisAppPath has an invalid format: $DeployIisAppPath"
    }

    $siteName = $segments[0]
    $iisPath = "IIS:\Sites\$siteName"

    if ($segments.Length -gt 1) {
        $applicationPath = [string]::Join('/', $segments[1..($segments.Length - 1)])
        $iisPath = "$iisPath\$applicationPath"
    }

    return Test-Path -LiteralPath $iisPath
}

function Test-IisInstalled {
    $feature = Get-WindowsOptionalFeature -Online -FeatureName 'IIS-WebServerRole' -ErrorAction SilentlyContinue
    return ($null -ne $feature -and $feature.State -eq 'Enabled')
}

function Show-IisInstallHelp {
    Write-Host 'IIS ist auf diesem System nicht installiert.' -ForegroundColor Yellow
    Write-Host 'IIS kann ueber die Windows-Features aktiviert werden:' -ForegroundColor Yellow
    Write-Host '  Option A – PowerShell (als Administrator):'
    Write-Host '    Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole, IIS-WebServer, IIS-ManagementConsole -All'
    Write-Host '  Option B – Windows-Einstellungen:'
    Write-Host '    Einstellungen -> System -> Optionale Features -> Weitere Windows-Features'
    Write-Host '    Haken setzen bei: Internetinformationsdienste'
    Write-Host 'Nach der Installation dieses Skript erneut ausfuehren.'
}

function Show-IisMissingHelp {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DeployIisAppPath
    )

    Write-Host "Die IIS-Anwendung '$DeployIisAppPath' wurde nicht gefunden." -ForegroundColor Yellow
    Write-Host "Kurzanleitung zum Anlegen:" -ForegroundColor Yellow
    Write-Host "1. IIS-Manager oeffnen (inetmgr)."
    Write-Host "2. Unter 'Sites' die Website aus dem ersten Segment sicherstellen (z. B. 'Dashboard')."
    Write-Host "3. Auf der Website Rechtsklick -> Add Application..."
    Write-Host "4. Alias auf den Rest des Pfads setzen (z. B. 'Softwareschmiede')."
    Write-Host "5. Physikalischen Pfad auf den spaeteren Publish-Ordner setzen und speichern."
}

function Invoke-Dotnet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$StepDescription
    )

    Write-Host "==> $StepDescription"
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        $formattedArguments = $Arguments | ForEach-Object { "'$_'" }
        throw "Command failed: dotnet $($formattedArguments -join ' ')"
    }
}

$repoRoot = Get-RepoRoot
$publishProfilePath = Join-Path $repoRoot 'src/Softwareschmiede/Properties/PublishProfiles/Lokaler IIS.pubxml'
$publishData = Get-PublishProfileData -PublishProfilePath $publishProfilePath

if (-not (Test-IisInstalled)) {
    Show-IisInstallHelp
    exit 1
}

if (-not (Test-IisApplicationExists -DeployIisAppPath $publishData.DeployIisAppPath)) {
    Show-IisMissingHelp -DeployIisAppPath $publishData.DeployIisAppPath
    exit 1
}

$buildProjects = @(
    'plugins/Softwareschmiede.Plugin.ClaudeCli/Softwareschmiede.Plugin.ClaudeCli.csproj',
    'plugins/Softwareschmiede.Plugin.GitHub/Softwareschmiede.Plugin.GitHub.csproj',
    'plugins/Softwareschmiede.Plugin.GitHubCopilot/Softwareschmiede.Plugin.GitHubCopilot.csproj',
    'plugins/Softwareschmiede.Plugin.KiSimulator/Softwareschmiede.Plugin.KiSimulator.csproj',
    'plugins/Softwareschmiede.Plugin.LocalDirectory/Softwareschmiede.Plugin.LocalDirectory.csproj',
    'src/Softwareschmiede/Softwareschmiede.csproj'
)

foreach ($projectRelativePath in $buildProjects) {
    $projectPath = Join-Path $repoRoot $projectRelativePath

    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        throw "Project not found: $projectRelativePath"
    }

    Invoke-Dotnet -StepDescription "Build $projectRelativePath" -Arguments @(
        'build',
        $projectPath,
        '--configuration', 'Release',
        '--nologo'
    )
}

$mainProjectPath = Join-Path $repoRoot 'src/Softwareschmiede/Softwareschmiede.csproj'
$publishProfileArgument = "/p:PublishProfile=$($publishData.ProfilePath)"
Invoke-Dotnet -StepDescription "Publish src/Softwareschmiede with profile '$($publishData.ProfileName)'" -Arguments @(
    'publish',
    $mainProjectPath,
    '--configuration', 'Release',
    '--no-build',
    $publishProfileArgument,
    '--nologo'
)

Write-Host 'Publish erfolgreich abgeschlossen.' -ForegroundColor Green
