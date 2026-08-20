param(
    [Parameter(Mandatory = $false)]
    [string]$Branch = 'main'
)

$ErrorActionPreference = 'Continue'

$output = & npx semantic-release --dry-run --no-ci --branches $Branch 2>&1 | Out-String

Write-Host "semantic-release output:"
Write-Host $output

$version = $null
$changed = $false

if ($output -match 'The next release version is ([0-9]+\.[0-9]+\.[0-9]+(?:[-+][^\s]+)?)') {
    $version = $Matches[1]
    $changed = $true
    Write-Host "Next release version: $version"
}
else {
    Write-Host "No new release version determined."
}

if ($env:GITHUB_OUTPUT) {
    "version=$version" | Add-Content -Path $env:GITHUB_OUTPUT -Encoding utf8
    "changed=$($changed.ToString().ToLowerInvariant())" | Add-Content -Path $env:GITHUB_OUTPUT -Encoding utf8
}
