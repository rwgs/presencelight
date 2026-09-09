<#
.SYNOPSIS
    Publishes the desktop application and updates the locally installed copy.

.DESCRIPTION
    The application is run from its own folder rather than from the repository, so a source
    change only reaches it after a publish and a file copy. This script does both, keeps the
    existing settings.json so the configured Entra and Hue values survive the update, and
    restarts the application.

    The build is self-contained and matches Build_Desktop_Artifact.yml, so the installed copy
    does not depend on the per-user .NET runtime location that DOTNET_ROOT points at.

.PARAMETER InstallPath
    Folder the application is installed in. Defaults to PresenceLight in the user profile.

.PARAMETER RuntimeIdentifier
    Target runtime for the published build. Defaults to win-x64.

.PARAMETER NoStart
    Update the files without starting the application afterwards.

.EXAMPLE
    .\Build\scripts\install-local-build.ps1
#>
[CmdletBinding()]
param(
    [string]$InstallPath = (Join-Path $env:USERPROFILE 'PresenceLight'),
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$project = Join-Path $repoRoot 'src\DesktopClient\PresenceLight\PresenceLight.csproj'
$publishPath = Join-Path $repoRoot "publish\$RuntimeIdentifier"

# The SDK is installed for the current user only and is deliberately not on the machine PATH.
$dotnetRoot = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet'
if (Test-Path (Join-Path $dotnetRoot 'dotnet.exe')) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$env:PATH"
}

Write-Host "Publishing $RuntimeIdentifier build to $publishPath"
dotnet publish $project -c Release -r $RuntimeIdentifier --self-contained true -p:ChannelName=Standalone -o $publishPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# The executable holds its own files open, so the running instance has to stop before the copy.
$running = Get-Process PresenceLight -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($InstallPath, [StringComparison]::OrdinalIgnoreCase) }
if ($running) {
    Write-Host "Stopping the running application before updating it"
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 30 -ErrorAction SilentlyContinue
}

if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath | Out-Null
}

# settings.json holds the configured Entra and light settings, so an existing one is kept
# rather than replaced by the empty defaults the publish produces.
$installedSettings = Join-Path $InstallPath 'settings.json'
$keepSettings = Test-Path $installedSettings
$savedSettings = $null
if ($keepSettings) {
    $savedSettings = Join-Path ([System.IO.Path]::GetTempPath()) "presencelight-settings-$PID.json"
    Copy-Item -Path $installedSettings -Destination $savedSettings -Force
}

Write-Host "Updating $InstallPath"
try {
    Copy-Item -Path (Join-Path $publishPath '*') -Destination $InstallPath -Recurse -Force
}
finally {
    if ($keepSettings) {
        Copy-Item -Path $savedSettings -Destination $installedSettings -Force
        Remove-Item -Path $savedSettings -Force
    }
}

if (-not $NoStart) {
    Write-Host "Starting PresenceLight from $InstallPath"
    Start-Process -FilePath (Join-Path $InstallPath 'PresenceLight.exe') -WorkingDirectory $InstallPath
}

Write-Host "Done. Settings file $(if ($keepSettings) { 'kept' } else { 'created from the published defaults' })."
