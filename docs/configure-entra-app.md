
## Configure an Entra ID Application

PresenceLight is a public client. It needs an application (client) identifier and a tenant
identifier, and it never uses a client secret. Choose either the scripted or the manual route
below; both produce the same registration.

### From the application (recommended)

Start PresenceLight, open **Settings**, and use **Create app registration** under *Application
registration*. A browser opens for sign-in, and the identifiers are filled in and saved for you.

Two options sit above the button. *Allow accounts in other organisations* makes the registration
multi-tenant, which is needed when the accounts to monitor do not all belong to one tenant.
*Skip tenant-wide admin consent* creates the registration without granting consent for the whole
organisation, so each user consents at their first sign-in instead.

Once a multi-tenant registration exists, an additional tenant is onboarded from the same panel:
enter the tenant under *Add another tenant* and choose **Open consent page**. An administrator of
that tenant approves it once. No second registration is needed.

The button requires PowerShell 7 (`pwsh`), because it runs the script described below. If PowerShell
7 is missing the panel says so and shows the command to run instead.

### Scripted

Run the following from the repository root in PowerShell 7:

```powershell
.\Build\scripts\register-entra-app.ps1
```

The script installs the two Microsoft Graph PowerShell modules it needs for the current user, opens
a browser for sign-in, creates the registration as a public client with the `http://localhost`
redirect URI and delegated `Presence.Read` and `User.Read`, grants tenant-wide admin consent, and
writes the identifiers into `settings.json` in the repository root.

The sign-in prompt asks for approval of **Microsoft Graph Command Line Tools**, which is Microsoft's
own client for administering a tenant from the command line. Approving it is the administrator step;
everything after it is automated. Creating the registration requires an account permitted to
register applications, and granting tenant-wide consent requires an administrator role. Add
`-SkipAdminConsent` to create the registration without tenant-wide consent, in which case each user
consents at first sign-in; both default permissions are user-consentable, so that works without an
administrator.

Running the script again updates the existing registration rather than creating a duplicate. Use
`-Audience MultiTenant` if accounts from other organisations must sign in, and `-SettingsPath` to
write somewhere other than the repository root. Neither identifier the script prints is a secret.

### Manual (portal)

1. Sign in to the [Microsoft Entra admin center](https://entra.microsoft.com/) using either a work or school account or a personal Microsoft account.
1. If your account gives you access to more than one tenant, select your account in the top right corner, and set your portal session to the desired Azure AD tenant
   (using **Switch Directory**).
1. In the left-hand navigation pane, select the **Entra ID** service, and then select **App registrations**.

#### Register the client app (WpfApp)

1. Navigate to the Microsoft identity platform for developers [App registrations](https://go.microsoft.com/fwlink/?linkid=2083908) page.
1. Select **New registration**.
   - In the **Name** section, enter a meaningful application name that will be displayed to users of the app, for example `Presence Light`.
   - In the **Supported account types** section, select **Accounts in this organizational directory only (YOUR_TENANT_NAME only - Single tenant)**.
   - In the **Redirect URI (optional)** section, select **Public client/native (mobile & desktop)** and enter http://localhost for the value.
    - Select **Register** to create the application.
1. On the app **Overview** page, find the **Application (client) ID** value and record it for later.
1. On the app **Overview** page, find the **Directory (tenant) ID** value and record it for later.<br>![Ids](../static/id.png)
1. In the list of pages select **API permissions**
   - Select **Add a permission**
   - Ensure that the **Microsoft APIs** tab is selected
   - In the **Commonly used Microsoft APIs** section, click on **Microsoft Graph**
   - Select **Delegated permissions**.
   - Ensure that the right permissions are checked: **Presence.Read, User.Read**. Use the search box if necessary. See the screenshot below.
   - Select **Add permissions**
   - You can consent for your entire organization by selecting **Grant admin consent for YOUR_TENANT_NAME**
     - In the **Grant admin consent confirmation** section select **Yes**

   ![Api Permissions](..//static/api-perms.png)

#### Configuring PresenceLight (Desktop)

1. Start `PresenceLight`.
1. Select **Settings**
  1. Enter your **Directory (tenant) ID** or `common` if you elected to support Multitenant account types.
  1. Enter your **Application (client) ID**
  1. Select **SAVE SETTINGS**
1. Select **Team Status**
  1. Select **SIGN IN**
  1. Complete authentication in your browser.