using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using PresenceLight.Core;

namespace PresenceLight.Services
{
    /// <summary>
    /// Creates the Entra ID application registration by running the repository's setup script.
    /// </summary>
    /// <remarks>
    /// The script is used rather than in-process Graph calls because creating a registration needs
    /// a Graph token, and acquiring one needs a client identifier that does not exist until the
    /// registration is created. The script resolves that by signing the operator in interactively
    /// through Microsoft's own command line client.
    /// </remarks>
    public class EntraSetupService : IEntraSetupService
    {
        private static readonly Regex ClientIdPattern = new(@"^Application \(client\) ID\s*:\s*(?<value>\S+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex TenantIdPattern = new(@"^Directory \(tenant\) ID\s*:\s*(?<value>\S+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

        private readonly ILogger<EntraSetupService> _logger;

        public EntraSetupService(ILogger<EntraSetupService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public bool IsSupported => FindPowerShell() != null && FindScript() != null;

        /// <inheritdoc />
        public string UnsupportedReason
        {
            get
            {
                if (FindPowerShell() == null)
                {
                    return "PowerShell 7 (pwsh) was not found. Install it, or run the setup command in a terminal yourself.";
                }

                if (FindScript() == null)
                {
                    return $"{EntraSetupCommand.ScriptFileName} was not found next to the application. Run the setup command from the repository instead.";
                }

                return string.Empty;
            }
        }

        /// <inheritdoc />
        public string DescribeCommand(EntraSetupRequest request) => EntraSetupCommand.Build(request);

        /// <inheritdoc />
        public async Task<EntraSetupResult> CreateRegistrationAsync(EntraSetupRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            string? powerShell = FindPowerShell();
            string? script = FindScript();

            if (powerShell == null || script == null)
            {
                return new EntraSetupResult { Succeeded = false, Message = UnsupportedReason };
            }

            var startInfo = new ProcessStartInfo(powerShell)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(script) ?? AppContext.BaseDirectory
            };

            foreach (string argument in BuildArguments(script, request))
            {
                startInfo.ArgumentList.Add(argument);
            }

            var output = new StringBuilder();

            try
            {
                using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

                process.OutputDataReceived += (_, e) => AppendLine(output, e.Data);
                process.ErrorDataReceived += (_, e) => AppendLine(output, e.Data);

                _logger.LogInformation("Starting the Entra registration script");

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                try
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Waiting stops on cancellation but the script keeps running, so end it here
                    // rather than leaving a sign-in prompt behind with nothing listening to it.
                    TryKill(process);
                    throw;
                }

                string captured = output.ToString();

                if (process.ExitCode != 0)
                {
                    _logger.LogWarning("The Entra registration script exited with code {ExitCode}", process.ExitCode);
                    return new EntraSetupResult
                    {
                        Succeeded = false,
                        Message = "Setup did not complete. The output below explains why.",
                        Output = captured
                    };
                }

                string? clientId = Match(ClientIdPattern, captured);
                string? tenantId = Match(TenantIdPattern, captured);

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(tenantId))
                {
                    _logger.LogWarning("The Entra registration script succeeded but no identifiers were found in its output");
                    return new EntraSetupResult
                    {
                        Succeeded = false,
                        Message = "Setup finished but did not report the identifiers. Check the output and enter them by hand.",
                        Output = captured
                    };
                }

                // The identifiers are not secrets, but the captured output also names the signed-in
                // account, so only the client identifier is written to the log.
                _logger.LogInformation("Entra registration ready for client {ClientId}", clientId);

                return new EntraSetupResult
                {
                    Succeeded = true,
                    ClientId = clientId,
                    TenantId = tenantId,
                    Message = "Registration created. The identifiers below have been filled in and saved.",
                    Output = captured
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("The Entra registration script was canceled");
                return new EntraSetupResult
                {
                    Succeeded = false,
                    Message = "Setup was canceled.",
                    Output = output.ToString()
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error running the Entra registration script");
                return new EntraSetupResult
                {
                    Succeeded = false,
                    Message = $"Setup could not be started: {e.Message}",
                    Output = output.ToString()
                };
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // The process may have exited between the check and the request; nothing to do.
            }
        }

        private static void AppendLine(StringBuilder builder, string? line)
        {
            if (line != null)
            {
                lock (builder)
                {
                    builder.AppendLine(line);
                }
            }
        }

        private static string? Match(Regex pattern, string text)
        {
            Match match = pattern.Match(text);
            return match.Success ? match.Groups["value"].Value : null;
        }

        private static List<string> BuildArguments(string script, EntraSetupRequest request)
        {
            // -NonInteractive is deliberately not passed: the first run may install the Microsoft
            // Graph modules, and sign-in opens a browser. Both are driven by the script itself.
            var arguments = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", script
            };

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                arguments.Add("-DisplayName");
                arguments.Add(request.DisplayName);
            }

            if (request.MultiTenant)
            {
                arguments.Add("-Audience");
                arguments.Add("MultiTenant");
            }

            if (request.SkipAdminConsent)
            {
                arguments.Add("-SkipAdminConsent");
            }

            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                arguments.Add("-TenantId");
                arguments.Add(request.TenantId);
            }

            if (!string.IsNullOrWhiteSpace(request.SettingsPath))
            {
                arguments.Add("-SettingsPath");
                arguments.Add(request.SettingsPath);
            }

            return arguments;
        }

        private static string? FindPowerShell()
        {
            foreach (string candidate in EnumeratePowerShellCandidates())
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumeratePowerShellCandidates()
        {
            string? path = Environment.GetEnvironmentVariable("PATH");

            if (!string.IsNullOrEmpty(path))
            {
                foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string candidate;

                    try
                    {
                        candidate = Path.Combine(directory.Trim(), "pwsh.exe");
                    }
                    catch (ArgumentException)
                    {
                        // A malformed PATH entry should not prevent the remaining ones being searched.
                        continue;
                    }

                    yield return candidate;
                }
            }

            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerShell", "7", "pwsh.exe");
        }

        private static string? FindScript()
        {
            // Beside the executable, where the project copies it for a published build.
            string local = Path.Combine(AppContext.BaseDirectory, EntraSetupCommand.ScriptFileName);

            if (File.Exists(local))
            {
                return local;
            }

            // Otherwise walk up to the repository root, which is the layout during development.
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Build", "scripts", EntraSetupCommand.ScriptFileName);

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
