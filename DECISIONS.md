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
