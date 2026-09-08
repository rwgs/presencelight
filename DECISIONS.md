# Project decisions

Accepted choices from the restoration discussion, recorded 2026-09-08. Add later reversals as new entries and mark the affected decision superseded. Implementation choices not yet settled remain in [SPEC.md](SPEC.md).

## 2026-09-08 Restore one account before adding multiple accounts

Status: Accepted.

### Decision

Deliver reliable single-account desktop operation first. Schedule multi-account support and installation/update distribution as separate phases; their order can be reconsidered after the first working build.

### Why

Authentication, application state and polling currently assume one account. The user explicitly accepts a smaller first milestone to avoid increasing the repair effort.

### Rejected alternatives

A full multi-account redesign before restoring basic operation would delay useful delivery and mix an unproven repair with new behaviour.

### Consequences

Do not introduce further first-account assumptions, but defer new aggregation abstractions. No planned feature is a released capability.

## 2026-09-08 Use PresenceLight as the starting repository

Status: Accepted.

### Decision

Repair the Windows desktop application and reuse useful UI, configuration, Graph and light code. Keep the sibling pyTeamsStatus repository as a reference; do not merge applications.

### Why

PresenceLight already provides direct Hue/local and remote control, other light integrations, a tray UI and packaging. pyTeamsStatus currently publishes to Home Assistant, which the user does not run. Its newer SlimCore heuristics and stateful log tailing may help a fallback, but its last-block/global-state parsing does not implement any-busy aggregation.

### Rejected alternatives

- pyTeamsStatus as the primary product would require a new direct light layer and desktop/distribution work.
- Running both applications as independent light writers risks one overwriting the other's busy state.
- A wholesale rewrite loses existing useful functionality before the failure is understood.

### Consequences

Inspect and repair existing code first. The web/container authentication warning does not justify expanding desktop scope to web restoration.

## 2026-09-08 Prefer central Graph monitoring and direct local Hue

Status: Accepted, subject to actual tenant-access validation.

### Decision

Use delegated sign-in to Microsoft Graph for each business account, initially one. One monitor controls the existing Hue Bridge locally. Home Assistant is optional future work.

### Why

Both accounts are Microsoft 365 business accounts. Graph presence is associated with accounts, so Teams can run on different computers without requiring local status agents on each.

### Rejected alternatives

- Local Teams logs as the default: undocumented markers can change and per-account attribution needs proof.
- Home Assistant as a mandatory intermediary: the user does not use it.
- Multiple independent writers to one light: available could overwrite busy.

### Consequences

The monitor needs internet access, bridge reachability and an awake host. Stopping the host can leave the last colour on the light. Verify tenant consent and Conditional Access separately from Windows installation privileges. Do not embed a desktop client secret.

If Graph is blocked or unsuitable after testing, first document the cause and consent options. A fallback would use authenticated agents with source identity/freshness and one coordinator; it must prove call/account detection and log-rotation behaviour before adoption. Remote/cloud Hue remains an optional route if local network placement requires it.

## 2026-09-08 Require positive availability before showing green

Status: Accepted for the core rule; detailed mappings unresolved.

### Decision

In the multi-account phase, either busy account makes the shared light red. Both required accounts must be positively available for green; missing or stale information does not count as available.

### Why

The light must not invite interruption while another account is busy or its status cannot be verified.

### Rejected alternatives

Last-event-wins aggregation and treating request failures as available can incorrectly clear a busy indication.

### Consequences

Track per-source freshness and health. Do not silently remove a disconnected account from the required set. Exact idle, meeting, DND/activity, fallback and stale-busy policies remain in [SPEC.md](SPEC.md).

## 2026-09-08 Target standard-user desktop operation

Status: Accepted goal; packaging mechanism not yet selected.

### Decision

First deliver a desktop build that runs without elevation. Later evaluate per-user installation and automatic updates using existing MSIX/App Installer support before introducing another distribution mechanism.

### Why

The user prefers no administrator install and eventual automatic updates. Existing app/package configuration provides a starting point, while the Python service installer requires elevation.

### Rejected alternatives

A Windows service as the initial deployment adds installation and user-session/authentication complexity. Assuming upstream signing/hosting is available leaves distribution unverified.

### Consequences

Validate dependencies, trust and device policies before promising administrator-free installation. Windows user privileges and Microsoft 365 consent are separate constraints. Signing, hosting and update timing remain implementation inputs.

## 2026-09-08 Install the build toolchain for the current user only

Status: Accepted.

### Decision

Install the .NET 10 SDK into `%LOCALAPPDATA%\Microsoft\dotnet` with Microsoft's official `dotnet-install.ps1` script rather than machine-wide. Builds and launches set `PATH` and `DOTNET_ROOT` for the session instead of changing machine environment variables.

### Why

The development machine had no .NET SDK and no Visual Studio, so a toolchain had to be added before anything could be built. A per-user install needs no administrator rights, matches the project's standard-user goal, keeps the machine unchanged for other software and can be removed by deleting one directory.

### Rejected alternatives

- A machine-wide `winget` or installer package requires elevation, writes to `C:\Program Files\dotnet` and alters the shared `PATH` for every user.
- Visual Studio would install far more than the desktop build needs.

### Consequences

The built executable does not start from a plain shell: its apphost looks for a machine-wide runtime and exits with `0x80008083` (`CoreHostLibMissingFailure`). Set `DOTNET_ROOT` before launching, and read that exit code as a missing toolchain rather than an application defect. The commands in [AGENTS.md](AGENTS.md) include the required session setup. This decision covers the development machine only; it does not determine how a finished build is distributed to end users.

## 2026-09-08 Use an application registration in our own tenant

Status: Accepted.

### Decision

Create and own the Entra ID application registration this fork signs in with, rather than depending on the upstream PresenceLight registration. [register-entra-app.ps1](Build/scripts/register-entra-app.ps1) creates it, and the identifiers are supplied through `settings.json` or the Settings page rather than committed to the repository.

### Why

Upstream issue 978, "PresenceLight will not work for non-Microsoft Employees", records that the published application registration lives in Microsoft's own tenant. Its author created it while employed there, and after leaving he can no longer maintain multi-tenant access for external users under Microsoft's tightened policy. The registration may eventually be deleted. This explains why installed releases stopped signing in and is consistent with this fork shipping an empty `ClientId`: the identifier was only ever injected from a secret at publish time. The upstream maintainer's own guidance in that issue is for affected users to build from source and supply their own identifier, which is the route already chosen here.

### Rejected alternatives

- Waiting for upstream to restore a shared registration depends on a policy decision outside this project, and the issue remains open with no committed fix.
- Reusing the upstream client identifier extracted from a released build depends on an application object we do not control and that is expected to stop working.
- Borrowing a Microsoft first-party client identifier such as the Azure CLI's would misuse another publisher's application identity, is unsupported and can break without notice.

### Consequences

Sign-in works only after the registration exists, so a working build is not the same as a working application. The permissions granted are exactly the delegated `Presence.Read` and `User.Read` the implemented features need, both of which a user can consent to without an administrator. The registration is a per-tenant asset that must be recorded and recreated if the tenant changes.

Phase 1 needs one account, so a single-tenant registration is sufficient. The later multi-account phase must revisit this if the two business accounts belong to different tenants: one registration would then need `-Audience MultiTenant` and consent in the second tenant. Because the registration requests `Presence.Read` and is not publisher verified, an ordinary user in that second tenant may be unable to consent for themselves where risk-based step-up consent is enabled; an administrator granting consent avoids this. [publisher-verification.md](docs/publisher-verification.md) records the mechanism and what verification would require.
