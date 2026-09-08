# Restore single-account desktop operation

Status: Planned, 2026-09-08. This is the current implementation approach, not evidence of completed repair. Work items and validation status live in [TASKS.md](TASKS.md); durable choices are in [DECISIONS.md](DECISIONS.md).

## Problem

The user wants the existing app working again. The exact desktop failure has not been reproduced. The first useful result is one business account reliably driving a Hue light. Multi-account support is absent today and deliberately deferred.

## Constraints discovered

Source inspection, not runtime validation, established:

| Evidence | Implication |
| --- | --- |
| [Desktop project](src/DesktopClient/PresenceLight/PresenceLight.csproj), [desktop props](src/DesktopClient/Directory.Build.props) | .NET 10 Windows target, WPF/Windows Forms, Blazor WebView; verify SDK/runtime and reconcile older documented OS requirements |
| [AuthorizationProvider](src/PresenceLight.Core/GraphServices/AuthorizationProvider.cs), [LoginService](src/PresenceLight.Core/GraphServices/LoginService.cs) | First cached account selected; multi-tenant registration is not simultaneous multi-account support |
| [AppState](src/PresenceLight.Core/Configuration/AppState.cs) | One user and one presence |
| [GraphWrapper](src/PresenceLight.Core/GraphServices/GraphWrapper.cs) | Reads signed-in user's Graph presence; existing retry handling needs failure-specific review |
| [TokenCacheHelper](src/PresenceLight.Core/GraphServices/TokenCacheHelper.cs) | Current-user Windows token protection exists and should be preserved |
| [HueService](src/PresenceLight.Core/Lights/HueServices/HueService.cs) | Direct bridge pairing and light/group operations exist; validate identifiers and actual bridge responses |
| [Package.appinstaller](src/DesktopClient/PresenceLight.Package/Package.appinstaller) | Launch/background update settings exist, but this fork's publishing channel is unverified |

The user confirms two business accounts, an existing Hue Bridge, possible separate Teams devices and no Home Assistant. The root README warns about web/container authentication; this is not a reproduced desktop fault. No automated test project was found in the initial inventory.

## Approach

### 1. Diagnose and build

Collect symptoms and redacted errors, inspect prerequisites, and run the desktop commands in [AGENTS.md](AGENTS.md). Separate compile, launch, sign-in and light failures. Record the exact working command/toolchain when established.

Start with the desktop project and its shared dependencies; only change files implicated by evidence. Preserve unrelated local changes and settings.

### 2. Authenticate one account

Reuse AuthorizationProvider, LoginService, GraphWrapper, TokenCacheHelper and the existing Entra settings UI/configuration. Verify whether the current registration works. If necessary, configure an appropriate public desktop client registration with delegated Presence.Read and only other permissions required by implemented features; do not distribute a client secret.

Prove actual /me/presence access before substantial changes. Exercise selection, protected persistence, silent renewal, sign-out and reauthentication. Review optional photo/profile loading so it cannot prevent presence monitoring. If tenant policy blocks access, record the exact cause and consent path before reconsidering log detection.

### 3. Control Hue directly

Reuse HueService and existing registration/discovery handlers. Accept a bridge IP if discovery fails. Pair, select a colour-capable light/group and prove manual colour changes first. Check identifier conversions and state mapping against the actual bridge, then connect available/busy/DND/in-call updates.

Use local control for this milestone. Remote Hue, other new integrations and Home Assistant are not required.

### 4. Repair recovery and polling

Review [MainWindow.xaml.cs](src/DesktopClient/PresenceLight/MainWindow.xaml.cs), GraphWrapper and the light handlers for bounded retries, throttling, cancellation, overlapping loops and UI blocking. Track successful reads and freshness so failures never silently become available. Agree fallback, startup/shutdown, working-hours and sleep/resume behaviour before implementing ambiguous policy.

Reconcile intended light state after reconnect even if the upstream presence is unchanged. Keep useful diagnostics free of credentials and unnecessary account data. Avoid a broad provider/aggregation redesign until the later phase.

### 5. Deliver and document

Produce a standard-user desktop build, setup instructions and recorded validation. Build affected shared consumers to catch regressions without repairing web/container authentication. Packaging/update publication is a later milestone.

## Trade-offs

Reuse reduces new work but requires diagnosis of existing dependencies and mixed UI/polling ownership. Single-account repair gives a useful earlier result but does not meet the eventual combined-account requirement.

Graph has stable documented account semantics but still depends on tenant policy, internet access and actual observed update latency. Local Hue avoids an extra cloud dependency for light control, but the host must reach the bridge and remain awake. Its crash or power loss can leave the last colour displayed.

No implementation approaches have been tried and abandoned yet. Record evidence here if that changes; promote durable decisions to DECISIONS.md.

## Verification

| Check | Failure it should catch |
| --- | --- |
| Desktop restore/build and affected shared builds | Missing prerequisites, incompatible dependencies, compile regressions |
| Focused automated regressions for repaired behaviour | Reintroduced failure handling, stale-to-available errors, retry/update-loop defects |
| Live first sign-in and /me/presence | Incorrect registration, permissions or tenant access |
| Restart, renewal, sign-out, revoked/expired authentication | Broken persistence and misleading healthy/available state |
| Physical Hue pairing, manual colours and presence changes | Discovery, identifiers, selection and mapping errors |
| Internet/bridge interruption and recovery with unchanged presence | Lost light updates that never reconcile |
| Sleep/resume and responsive settings UI | Stuck or duplicate loops and blocked UI |
| Normal working-day trial under standard user | Missed transitions, unacceptable delay and unexpected elevation |

Automated tests should use controlled Graph/light responses for failure cases where possible. Live account consent, physical lights, host sleep and a working-day trial require the user's actual environment; none has been performed. Numerical latency/freshness thresholds must be agreed before acceptance. Record all results in TASKS.md and do not mark Phase 1 complete while required manual checks remain outstanding.
