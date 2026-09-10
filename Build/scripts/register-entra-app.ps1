<#
.SYNOPSIS
    Creates the Entra ID application registration that PresenceLight needs for delegated sign-in.

.DESCRIPTION
    PresenceLight is a public client: it holds no client secret and needs only an application
    (client) identifier and a tenant identifier. This script signs the operator in to Microsoft
    Graph, creates or updates the registration, optionally grants tenant-wide admin consent for
    the delegated permissions, and writes the resulting identifiers into settings.json.

    The script is idempotent. Running it again reuses the existing registration with the same
    display name and refreshes its configuration instead of creating a duplicate.

    Sign-in is interactive by design. Nothing here stores or transmits credentials, and the
    identifiers the script prints are not secrets.

.PARAMETER DisplayName
    Name of the registration as it appears in the Entra admin center.

.PARAMETER TenantId
    Tenant to sign in to. Omit to use the tenant of the account chosen at sign-in.

.PARAMETER Audience
    SingleTenant restricts sign-in to this organisation, which is the documented default.
    MultiTenant allows accounts from other organisations and records TenantId as "common".

.PARAMETER DelegatedPermissions
    Delegated Microsoft Graph permissions to request. Presence.Read reads the signed-in user's
    presence; User.Read reads the profile used for the display name and photo.

.PARAMETER SettingsPath
    settings.json to update. Defaults to the repository root, which is the working directory the
    desktop application uses when started from there.

.PARAMETER SkipAdminConsent
    Create the registration without granting tenant-wide consent. Each user is then prompted to
    consent at first sign-in, which works because both default permissions are user-consentable.

.PARAMETER UseDeviceCode
    Sign in with a device code instead of opening a browser directly. Use this where no browser can
    be opened for the script. Browser sign-in already falls back to this automatically on failure.

.EXAMPLE
    .\Build\scripts\register-entra-app.ps1

.EXAMPLE
    .\Build\scripts\register-entra-app.ps1 -DisplayName 'PresenceLight (lab)' -SkipAdminConsent
#>
[CmdletBinding()]
param(
    [string]$DisplayName = 'PresenceLight',
    [string]$TenantId,
    [ValidateSet('SingleTenant', 'MultiTenant')]
    [string]$Audience = 'SingleTenant',
    [string[]]$DelegatedPermissions = @('Presence.Read', 'User.Read'),
    [string]$SettingsPath,
    [switch]$SkipAdminConsent,
    [switch]$UseDeviceCode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Errors are terminating, so report what failed and where before exiting. Without this the caller
# sees only a non-zero exit code, which is not enough to tell a permissions problem from a bug.
trap
{
    Write-Host ''
    Write-Host 'Setup failed.'
    Write-Host "Reason  : $($_.Exception.Message)"
    Write-Host "Type    : $($_.Exception.GetType().FullName)"
    Write-Host "Command : $($_.InvocationInfo.MyCommand)"
    Write-Host "Line $($_.InvocationInfo.ScriptLineNumber): $($_.InvocationInfo.Line.Trim())"

    if ($_.ErrorDetails -and $_.ErrorDetails.Message)
    {
        Write-Host "Details : $($_.ErrorDetails.Message)"
    }

    exit 1
}

$graphAppId = '00000003-0000-0000-c000-000000000000'
$instance = 'https://login.microsoftonline.com/'

# MSAL's loopback listener uses http://localhost, which is the value written to settings.json.
# The nativeclient URI is registered as well because upstream issue 978 records sign-in failing
# with AADSTS900971 ("No reply address provided") until both were present on the registration.
$redirectUri = 'http://localhost'
$additionalRedirectUris = @('https://login.microsoftonline.com/common/oauth2/nativeclient')

function Write-Step {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Write-Step 'Checking the Microsoft Graph PowerShell modules'
foreach ($module in @('Microsoft.Graph.Authentication', 'Microsoft.Graph.Applications')) {
    if (-not (Get-Module -ListAvailable -Name $module)) {
        Write-Host "Installing $module for the current user. No administrator rights are required."
        Install-Module -Name $module -Scope CurrentUser -Repository PSGallery -Force -AllowClobber
    }
    Import-Module -Name $module
}

$scopes = @('Application.ReadWrite.All')
if (-not $SkipAdminConsent) {
    $scopes += 'DelegatedPermissionGrant.ReadWrite.All'
}

Write-Step 'Signing in to Microsoft Graph'

# Brokered sign-in through the Windows Account Manager needs a parent window handle, which does not
# exist when this script runs from a hidden or output-redirected process such as the Settings page.
# Without this it fails with "A window handle must be configured".
if (Get-Command Set-MgGraphOption -ErrorAction SilentlyContinue) {
    try
    {
        Set-MgGraphOption -DisableLoginByWAM $true | Out-Null
    }
    catch
    {
        Write-Host "Could not disable brokered sign-in: $($_.Exception.Message)"
    }
}

$connectArgs = @{ Scopes = $scopes; NoWelcome = $true }
if ($TenantId) { $connectArgs['TenantId'] = $TenantId }

if ($UseDeviceCode)
{
    Write-Host 'Open the address shown below and enter the code to sign in.'
    Connect-MgGraph @connectArgs -UseDeviceAuthentication
}
else
{
    Write-Host 'A browser window will open. Sign in with an account that may register applications.'
    Write-Host 'The consent prompt names "Microsoft Graph Command Line Tools"; approve it as an administrator.'

    try
    {
        Connect-MgGraph @connectArgs
    }
    catch
    {
        # A browser cannot always be opened, so fall back rather than failing the whole run.
        Write-Host "Browser sign-in did not work: $($_.Exception.Message)"
        Write-Host 'Falling back to device code sign-in.'
        Connect-MgGraph @connectArgs -UseDeviceAuthentication
    }
}

$context = Get-MgContext
if (-not $context) {
    throw 'Sign-in did not complete, so no registration was created.'
}
$signedInTenant = $context.TenantId
Write-Host "Signed in to tenant $signedInTenant as $($context.Account)."

Write-Step 'Resolving the requested Microsoft Graph permissions'
$graphServicePrincipal = Get-MgServicePrincipal -Filter "appId eq '$graphAppId'" -All | Select-Object -First 1
if (-not $graphServicePrincipal) {
    throw 'The Microsoft Graph service principal was not found in this tenant.'
}

$resourceAccess = foreach ($permission in $DelegatedPermissions) {
    $scope = $graphServicePrincipal.Oauth2PermissionScopes | Where-Object { $_.Value -eq $permission } | Select-Object -First 1
    if (-not $scope) {
        throw "Microsoft Graph does not expose a delegated permission named '$permission'."
    }
    Write-Host "  $permission ($($scope.Id))"
    @{ Id = $scope.Id; Type = 'Scope' }
}

$signInAudience = if ($Audience -eq 'MultiTenant') { 'AzureADMultipleOrgs' } else { 'AzureADMyOrg' }
$applicationParameters = @{
    DisplayName            = $DisplayName
    SignInAudience         = $signInAudience
    IsFallbackPublicClient = $true
    PublicClient           = @{ RedirectUris = @($redirectUri) + $additionalRedirectUris }
    RequiredResourceAccess = @(@{ ResourceAppId = $graphAppId; ResourceAccess = @($resourceAccess) })
}

Write-Step "Creating or updating the '$DisplayName' registration"
$filterName = $DisplayName.Replace("'", "''")
$application = Get-MgApplication -Filter "displayName eq '$filterName'" -All | Select-Object -First 1
if ($application) {
    Write-Host 'An existing registration was found; updating it instead of creating a duplicate.'
    Update-MgApplication -ApplicationId $application.Id @applicationParameters
    $application = Get-MgApplication -ApplicationId $application.Id
}
else {
    $application = New-MgApplication @applicationParameters
    Write-Host 'Registration created.'
}

$servicePrincipal = Get-MgServicePrincipal -Filter "appId eq '$($application.AppId)'" -All | Select-Object -First 1
if (-not $servicePrincipal) {
    $servicePrincipal = New-MgServicePrincipal -AppId $application.AppId
    Write-Host 'Service principal created in this tenant.'
}

if ($SkipAdminConsent) {
    Write-Step 'Skipping tenant-wide consent'
    Write-Host 'Each user will be asked to consent at first sign-in.'
}
else {
    Write-Step 'Granting tenant-wide admin consent'
    $scopeString = (($DelegatedPermissions | Sort-Object -Unique) -join ' ')

    # The oauth2PermissionGrant cmdlets live in Microsoft.Graph.Identity.SignIns, which this script
    # does not install. These requests go directly over the connection already established rather
    # than downloading another module for three calls.
    try {
        $filter = "clientId eq '$($servicePrincipal.Id)' and consentType eq 'AllPrincipals' and resourceId eq '$($graphServicePrincipal.Id)'"
        $query = 'https://graph.microsoft.com/v1.0/oauth2PermissionGrants?$filter=' + [uri]::EscapeDataString($filter)
        $response = Invoke-MgGraphRequest -Method GET -Uri $query

        $existingGrant = $null
        if ($response -is [System.Collections.IDictionary] -and $response.Contains('value')) {
            $existingGrant = @($response['value']) | Select-Object -First 1
        }

        if ($existingGrant) {
            $grantId = $existingGrant['id']
            Invoke-MgGraphRequest -Method PATCH -Uri "https://graph.microsoft.com/v1.0/oauth2PermissionGrants/$grantId" -Body @{ scope = $scopeString } | Out-Null
            Write-Host "Updated the existing consent grant to: $scopeString"
        }
        else {
            $body = @{
                clientId    = $servicePrincipal.Id
                consentType = 'AllPrincipals'
                resourceId  = $graphServicePrincipal.Id
                scope       = $scopeString
            }

            Invoke-MgGraphRequest -Method POST -Uri 'https://graph.microsoft.com/v1.0/oauth2PermissionGrants' -Body $body | Out-Null
            Write-Host "Granted: $scopeString"
        }
    }
    catch {
        # The registration already exists and works without tenant-wide consent, so report this and
        # carry on rather than discarding a usable result.
        Write-Host "Tenant-wide consent was not granted: $($_.Exception.Message)"
        Write-Host 'This is not fatal. Each user is asked to consent at their own first sign-in instead.'
    }
}

if (-not $SettingsPath) {
    $repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
    $SettingsPath = Join-Path $repositoryRoot 'settings.json'
}

Write-Step "Writing the identifiers to $SettingsPath"
$settings = if (Test-Path $SettingsPath) {
    Get-Content -Path $SettingsPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
}
else {
    @{}
}

if (-not $settings.ContainsKey('AADSettings') -or $null -eq $settings['AADSettings']) {
    $settings['AADSettings'] = @{}
}

$aadSettings = $settings['AADSettings']
$aadSettings['ClientId'] = $application.AppId
$aadSettings['TenantId'] = if ($Audience -eq 'MultiTenant') { 'common' } else { $signedInTenant }
$aadSettings['Instance'] = $instance
$aadSettings['RedirectUri'] = $redirectUri
if (-not $aadSettings.ContainsKey('Scopes') -or -not $aadSettings['Scopes']) {
    $aadSettings['Scopes'] = @('https://graph.microsoft.com/.default')
}
$settings['AADSettings'] = $aadSettings

$settings | ConvertTo-Json -Depth 32 | Set-Content -Path $SettingsPath -Encoding utf8

# Signing out can warn about clearing its own token cache. Every useful step has already completed
# by this point, so surfacing that warning would only look like a failure.
try { Disconnect-MgGraph -WarningAction SilentlyContinue -ErrorAction SilentlyContinue 3>$null | Out-Null } catch { }

Write-Step 'Done'
Write-Host "Application (client) ID : $($application.AppId)"
Write-Host "Directory (tenant) ID   : $($aadSettings['TenantId'])"
Write-Host "Settings file           : $SettingsPath"
Write-Host ''
Write-Host 'Neither identifier is a secret and no client secret was created.'
Write-Host 'Start PresenceLight from the directory holding that settings file, then choose Sign In on Teams Status.'
