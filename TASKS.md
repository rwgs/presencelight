# PresenceLight tasks

Status: T1 complete; the desktop application builds and starts. Sign-in configuration is the next blocker. Phase order lives in [ROADMAP.md](ROADMAP.md), requirements in [SPEC.md](SPEC.md), and the current approach in [PLAN.md](PLAN.md).

## Current phase: single-account restoration

- [ ] T2: Make delegated sign-in and one real presence read work.
  - Scope: existing public-client registration/configuration, intentional account selection, least permissions and actionable access errors.
  - Acceptance: one actual business account signs in and returns /me/presence; optional profile data does not block it.
  - Automated validation: build and focused regressions for any repaired authentication/profile behaviour.
  - Manual validation: real consent/sign-in, presence retrieval and missing-photo case. Watch for the failures reported in upstream issue 978: `AADSTS900971` ("No reply address provided") when redirect URIs are incomplete, and a sign-in loop that returns to the login page after the authorisation-complete page. The Entra fields are editable in the application through [Settings.razor](src/PresenceLight.Razor/Components/Pages/Settings.razor), so a wrong identifier can be corrected without rebuilding.
  - Dependencies: T1 (met). `AADSettings.ClientId` is empty in [appsettings.json](src/DesktopClient/PresenceLight/appsettings.json), so the running application reports "Enter Microsoft Entra / Azure AD configuration in Settings" and never attempts authentication. An application registration and its client identifier are required before anything else in this task can be tested; the shipped scope is `https://graph.microsoft.com/.default` rather than an explicit `Presence.Read`, which resolves to whatever the registration grants. [register-entra-app.ps1](Build/scripts/register-entra-app.ps1) automates the registration, consent and settings update so the only manual step is an interactive administrator sign-in, and the Settings page runs it through [EntraSetupService.cs](src/DesktopClient/PresenceLight/Services/EntraSetupService.cs) so no command line is needed. The script has been parse-checked and its settings merge tested offline, the desktop build succeeds and the panel renders with creation available, but neither the script nor the panel has yet been run against a real tenant, so every Microsoft Graph call in this path is unverified. Organisation policy remains unverified.

- [ ] T3: Verify authentication across restart and reauthentication.
  - Scope: protected cache, silent renewal, sign-out, revoked/expired access and explicit reconnect state.
  - Acceptance: cached restart works, sign-out clears the intended account, required reauthentication is visible, and failures never count as available.
  - Automated validation: focused cache/error-state tests where meaningful; relevant builds.
  - Manual validation: real restart, renewal and reauthentication checks; confirm diagnostics contain no tokens.
  - Dependencies: T2. Note from T1: the standalone build saves `settings.json` to `Directory.GetCurrentDirectory()` ([StandaloneSettingsService.cs:18](src/DesktopClient/PresenceLight/Services/Settings/StandaloneSettingsService.cs#L18)), so saved configuration follows the working directory of whatever started the application rather than a per-user location. The MSAL token cache, by contrast, lives under `%LOCALAPPDATA%\PresenceLight\` ([TokenCacheHelper.cs:19](src/PresenceLight.Core/GraphServices/TokenCacheHelper.cs#L19)). Confirm which behaviour is intended before relying on restart persistence, and revisit it in T8 where a standard user may launch from a directory they cannot write to.

- [ ] T4: Pair the bridge and control the selected light manually.
  - Scope: discovery/IP entry, button pairing, light/group selection and identifier handling.
  - Acceptance: intended colour-capable light/group changes colour directly without Home Assistant.
  - Automated validation: builds and focused identifier/mapping regressions if repaired.
  - Manual validation: actual bridge pairing, enumeration, saved selection and colour test.
  - Dependencies: T1; host bridge reachability and intended light/group.

- [ ] T5: Connect presence transitions to the physical light.
  - Scope: existing mapping and polling, including available, busy, DND and in-call behaviour.
  - Acceptance: physical light follows the agreed single-account mapping; observed latency is recorded.
  - Automated validation: focused state-mapping checks for changed logic.
  - Manual validation: exercise transitions on the actual business account and Hue light.
  - Dependencies: T2 and T4; agree brightness and relevant mappings.

- [ ] T6: Define and implement freshness/failure indication.
  - Scope: last-success tracking, fallback colour/timeout, startup/shutdown and working-hours behaviour.
  - Acceptance: failed/stale reads never silently turn green; UI explains unknown and reauthentication states.
  - Automated validation: controlled fresh, failed and expired-read cases against agreed thresholds.
  - Manual validation: confirm fallback and visible status on the physical setup.
  - Dependencies: agree fallback, freshness timeout and lifecycle policies from SPEC.md.

- [ ] T7: Recover polling and light state after interruptions.
  - Scope: bounded retry/backoff, throttling, cancellation, responsive UI, prevention of overlapping loops and reconciliation after failed writes.
  - Acceptance: reconnect and sleep/resume restore monitoring, including when presence is unchanged; no stuck or duplicate loops.
  - Automated validation: focused transient/throttled responses, failed-write retry and loop-lifecycle checks for repaired code.
  - Manual validation: interrupt/restore internet and bridge access; sleep/resume while using the settings UI.
  - Dependencies: T3, T5 and T6.

- [ ] T8: Deliver a verified standard-user build and setup instructions.
  - Scope: reproducible local build, relevant shared-consumer regression checks, documented setup and a normal working-day trial.
  - Acceptance: Phase 1 exit criteria pass with measured latency/recovery and documented environmental limitations; no elevation at runtime.
  - Automated validation: desktop build, affected shared builds and established regression suite.
  - Manual validation: standard-user launch and working-day trial; record missed transitions and all required scenario results.
  - Dependencies: T1-T7 and agreed latency/freshness acceptance thresholds. Installer/update publication belongs to a later phase.

## Validation status

| Area | Result |
| --- | --- |
| Initial source/repository comparison | Read-only inspection completed; rationale recorded in DECISIONS.md and PLAN.md |
| Desktop restore and build | Passed 2026-09-08 on .NET SDK 10.0.401: 0 errors, 127 warnings, 23.9 s |
| Desktop launch | Passed 2026-09-08: window renders, no startup error; requires `DOTNET_ROOT` for the per-user toolchain |
| Automated application tests | Not run; no test project exists |
| Actual Graph account access and token recovery | Not tested |
| Hue pairing and physical transitions | Not tested |
| Recovery, latency and working-day trial | Not tested |
| Per-user installer and automatic updates | Not tested; later phase |
| Second account and combined rules | Not implemented/validated; later phase |

## Completed

- [x] T1: Establish a reproducible desktop build and failure report. Completed 2026-09-08.
  - Result: the application was not broken. It had never been built on this machine, which had no .NET SDK and no Visual Studio. After installing the SDK for the current user, restore, build and launch all succeeded unmodified; no source change was needed.
  - Environment: Windows 11 Pro 10.0.26200, .NET SDK 10.0.401 with the 10.0.12 NETCore, AspNetCore and WindowsDesktop runtimes, WebView2 runtime 152.0.4191.66 already present, no `global.json`.
  - Build: `dotnet build .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug --no-restore -p:ChannelName=Standalone` returned 0 errors and 127 warnings in 23.9 s. Restore returned 0 errors.
  - Launch: `PresenceLight.exe` starts, the Blazor/WebView2 interface renders and the process stays responsive. The log records settings loading successfully and light mode Graph.
  - Toolchain symptom to recognise: launching without `DOTNET_ROOT` exits immediately with `0x80008083` (`CoreHostLibMissingFailure`) because the apphost looks for a machine-wide runtime. This is environmental, not an application defect, and is recorded in [AGENTS.md](AGENTS.md) and [DECISIONS.md](DECISIONS.md).
  - Findings carried forward: the empty `AADSettings.ClientId` blocks T2; restore reports known vulnerability advisories against `Microsoft.AspNetCore.DataProtection` 10.0.0 (critical), `System.Security.Cryptography.Xml` 10.0.0 and `Microsoft.Kiota.Abstractions` 1.17.1 (high), which need a separate dependency review; the 127 build warnings include nullability, `CA1416` platform-compatibility and one unawaited call at [MainWindow.xaml.cs:171](src/DesktopClient/PresenceLight/MainWindow.xaml.cs#L171) that are relevant to the T7 polling review.

- [x] Document the agreed restoration direction and migrate it into the project documentation.
  - Scope: requirements, phase order, current approach/tasks, durable decisions and agent guidance; remove the superseded plan index and its references.
  - Acceptance: root Markdown templates populated and renamed, README navigation updated, proposed work distinguished from shipped behaviour.
  - Validation: documentation filenames, relative links, remaining placeholder/reference checks and whitespace review.
  - Application tests: not applicable to this documentation-only change; no runtime claims are made.
