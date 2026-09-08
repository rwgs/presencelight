using System;
using System.Threading;
using System.Threading.Tasks;

namespace PresenceLight.Core
{
    /// <summary>
    /// Options for creating the Entra ID application registration that PresenceLight signs in with.
    /// </summary>
    public class EntraSetupRequest
    {
        /// <summary>
        /// Gets or sets the name the registration is given in the Entra admin center.
        /// </summary>
        public string DisplayName { get; set; } = "PresenceLight";

        /// <summary>
        /// Gets or sets a value indicating whether accounts from other organisations may sign in.
        /// Required when the accounts to monitor do not all belong to one tenant.
        /// </summary>
        public bool MultiTenant { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether tenant-wide consent is skipped. Both permissions
        /// PresenceLight requests are user-consentable, so skipping consent avoids needing an
        /// administrator at the cost of a consent prompt at each user's first sign-in.
        /// </summary>
        public bool SkipAdminConsent { get; set; }

        /// <summary>
        /// Gets or sets the tenant to sign in to. Null uses the tenant of the account chosen at sign-in.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the settings file the identifiers are written to.
        /// </summary>
        public string? SettingsPath { get; set; }
    }

    /// <summary>
    /// Outcome of an attempt to create the application registration.
    /// </summary>
    public class EntraSetupResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the registration now exists and is configured.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// Gets or sets the application (client) identifier. This is not a secret.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the directory (tenant) identifier, or "common" for a multi-tenant registration.
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets a short outcome message suitable for display.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the captured output, shown to the operator so a failure can be diagnosed.
        /// </summary>
        public string Output { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates the Entra ID application registration PresenceLight signs in with.
    /// </summary>
    /// <remarks>
    /// A registration cannot be created from inside the application alone, because creating one
    /// requires a Microsoft Graph token and obtaining that token requires a client identifier that
    /// does not exist yet. The supported implementation therefore delegates to the repository's
    /// setup script, which signs the operator in interactively.
    /// </remarks>
    public interface IEntraSetupService
    {
        /// <summary>
        /// Gets a value indicating whether this host can create a registration.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Gets the reason creation is unavailable, or an empty string when it is available.
        /// </summary>
        string UnsupportedReason { get; }

        /// <summary>
        /// Creates or updates the registration and writes its identifiers to the settings file.
        /// </summary>
        Task<EntraSetupResult> CreateRegistrationAsync(EntraSetupRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the equivalent command, so the same work can be done manually if creation is unavailable.
        /// </summary>
        string DescribeCommand(EntraSetupRequest request);
    }

    /// <summary>
    /// Builds the sign-in URLs used to onboard a tenant to an existing registration.
    /// </summary>
    public static class EntraSetupLinks
    {
        /// <summary>
        /// Builds the administrator consent URL for a registration in another tenant. An
        /// administrator of that tenant opens it once to grant consent for their organisation;
        /// no second registration is needed.
        /// </summary>
        /// <param name="instance">Authority instance, for example https://login.microsoftonline.com/.</param>
        /// <param name="tenant">Tenant identifier or verified domain to grant consent in.</param>
        /// <param name="clientId">Application (client) identifier of the existing registration.</param>
        /// <param name="redirectUri">Redirect URI registered on the application.</param>
        public static string BuildAdminConsentUrl(string instance, string tenant, string clientId, string redirectUri)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A client identifier is required.", nameof(clientId));
            }

            if (string.IsNullOrWhiteSpace(tenant))
            {
                throw new ArgumentException("A tenant is required.", nameof(tenant));
            }

            string authority = string.IsNullOrWhiteSpace(instance)
                ? "https://login.microsoftonline.com/"
                : instance.Trim();

            if (!authority.EndsWith("/", StringComparison.Ordinal))
            {
                authority += "/";
            }

            string url = $"{authority}{Uri.EscapeDataString(tenant.Trim())}/adminconsent?client_id={Uri.EscapeDataString(clientId.Trim())}";

            if (!string.IsNullOrWhiteSpace(redirectUri))
            {
                url += $"&redirect_uri={Uri.EscapeDataString(redirectUri.Trim())}";
            }

            return url;
        }
    }

    /// <summary>
    /// Used by hosts that cannot create a registration, such as the web application.
    /// </summary>
    public class UnsupportedEntraSetupService : IEntraSetupService
    {
        /// <inheritdoc />
        public bool IsSupported => false;

        /// <inheritdoc />
        public string UnsupportedReason =>
            "Creating an application registration is only available in the desktop application. Run the setup script manually instead.";

        /// <inheritdoc />
        public Task<EntraSetupResult> CreateRegistrationAsync(EntraSetupRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EntraSetupResult
            {
                Succeeded = false,
                Message = UnsupportedReason
            });
        }

        /// <inheritdoc />
        public string DescribeCommand(EntraSetupRequest request) => EntraSetupCommand.Build(request);
    }

    /// <summary>
    /// Builds the setup script command line shared by every host.
    /// </summary>
    public static class EntraSetupCommand
    {
        /// <summary>
        /// Name of the script that creates the registration.
        /// </summary>
        public const string ScriptFileName = "register-entra-app.ps1";

        /// <summary>
        /// Builds the command an operator can run themselves for the supplied options.
        /// </summary>
        public static string Build(EntraSetupRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string command = $".\\Build\\scripts\\{ScriptFileName}";

            if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName != "PresenceLight")
            {
                command += $" -DisplayName '{request.DisplayName}'";
            }

            if (request.MultiTenant)
            {
                command += " -Audience MultiTenant";
            }

            if (request.SkipAdminConsent)
            {
                command += " -SkipAdminConsent";
            }

            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                command += $" -TenantId {request.TenantId}";
            }

            if (!string.IsNullOrWhiteSpace(request.SettingsPath))
            {
                command += $" -SettingsPath '{request.SettingsPath}'";
            }

            return command;
        }
    }
}
