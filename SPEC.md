# PresenceLight specification

Status: Requirements documented on 2026-09-08. Restoration and live validation have not started.

## Problem and users

The user needs a dependable physical indication of Microsoft Teams availability using their existing Philips Hue Bridge and lights. The existing desktop app needs diagnosis and restoration; the exact failure has not yet been reproduced.

Both accounts are Microsoft 365 business accounts and Teams may run on separate computers. The user does not use Home Assistant. Reliability matters more than language choice or preserving every existing feature. Prefer standard-user operation, per-user installation and eventual automatic updates.

## Required behaviour

### Initial milestone: one account

- Sign into one actual business account and display its current presence and connection health.
- Discover the local Hue Bridge or accept its IP address, pair through the bridge button, select a colour-capable light/group and allow manual colour verification.
- Drive the selected light directly from presence, including available, busy, DND and in-call transitions.
- Keep the tray/settings UI responsive while signing in, polling and controlling lights.
- Preserve configuration and protected authentication across restarts. Support sign-out, silent renewal and explicit reauthentication when required.
- Missing optional profile data must not prevent monitoring.
- Recover from temporary internet/bridge failures and Windows sleep/resume. After recovery, reconcile the light even if presence did not change while its update failed.
- Distinguish stale or failed reads from available status and explain failures to the user.
- Run the delivered desktop application without elevation. Record any prerequisite or organisation policy that limits installation.

### Later milestones

- Provide per-user installation, optional startup at sign-in and automatic updates with settings preservation and a documented recovery path.
- Monitor both business accounts independently and combine them into one light state, including when Teams runs on separate devices.
- Display account identity, freshness and health, and explain the combined state. One account's sign-in or failure must not block the other.
- Allow direct integrations for other lights later. Home Assistant remains optional.

### Combined-status policy for the multi-account phase

| Condition | Required or proposed behaviour |
| --- | --- |
| Either account is busy | Required: red |
| Both required accounts are positively available | Required: green |
| A required account is missing, stale or inaccessible | Required: never assume it is available |
| Fresh in-call, presenting or DND status | Proposed: treat as busy/red, including when another account is unknown |
| No fresh busy status and any required source is unknown | Proposed: configurable fallback colour |
| Away, offline, out of office, AvailableIdle or BusyIdle | Mapping unresolved; default proposal does not count these as confirmed available |

A lost connection must not silently remove an account from the set required for green. The user's word "idle" means free to be interrupted for planning purposes; precise activity/availability mappings, meeting handling, stale-busy retention and any delay before clearing red need confirmation.

## User experience

Use the existing Windows tray app and settings workflow: configure/sign in, pair/select a light, test a colour, then enable presence monitoring. Loading, unavailable and reauthentication states must be visible. Show health in text as well as colour. Multi-account management is a later UI change, not an initial prerequisite.

## Architecture and data flow

The accepted design is one central monitor reading Microsoft Graph and controlling Hue locally. [DECISIONS.md](DECISIONS.md) records why.

```text
Teams account(s), possibly on separate computers
    -> Microsoft Graph -> PresenceLight -> local Hue Bridge -> selected light/group
```

The monitor owns authentication, presence freshness and intended light state. The bridge is the local light-control boundary. The monitor needs internet access, bridge connectivity and an awake host. If the host stops or loses power, a light may retain its last colour: an in-process timeout cannot change it while the process is stopped.

A remote-agent design is conditional future work only. Agents would report source identity and freshness through authenticated connections to one coordinator; they would not independently write the shared light.

## Security and privacy

- Use delegated public-client sign-in with the least permissions needed, including Presence.Read; never distribute a desktop client secret.
- Protect tokens for the current Windows user and keep bridge credentials out of source control and diagnostics.
- Treat each organisation as a separate access-policy boundary. Business accounts are supported by Graph, but tenant consent and Conditional Access must be tested for each account.
- Standard-user Windows operation does not guarantee the absence of Microsoft 365 administrator consent.
- Log enough to diagnose failures without exposing tokens or unnecessary account information.
- Validate package signing/trust and release hosting before enabling automatic updates.

## Performance and compatibility

The current desktop project targets net10.0-windows10.0.19041 and uses WPF plus a Blazor WebView. This is a source declaration, not a verified supported-OS matrix; older README requirements must be reconciled during baseline work.

Measure Graph-to-light delay, retry behaviour and missed transitions on the actual setup. No numerical latency or freshness budget has been agreed. Set those acceptance thresholds before closing the recovery/working-day trial tasks. Polling must respect throttling, avoid overlapping loops and remain cancellable.

## Non-goals

- Multi-account support, remote agents and automatic update delivery in the initial repair milestone.
- Replacing the desktop app with pyTeamsStatus or requiring Home Assistant.
- Restoring the web/container authentication flow as part of desktop repair.
- Rewriting the entire application or making speculative integrations.
- Writing presence back to Teams.

## Acceptance criteria

| Outcome | Required evidence |
| --- | --- |
| Reproducible desktop build | Recorded tooling, build command and successful output |
| One real account works | Live sign-in, presence retrieval, restart/token renewal and sign-out checks |
| Hue works directly | Physical pairing, selection, manual colour and presence-transition checks |
| Recovery is dependable | Focused regression tests where appropriate plus live internet, bridge, authentication and sleep/resume checks |
| Unknown does not become available | Automated state/failure checks and visible manual confirmation |
| Usable delivery | Standard-user run, setup instructions and a normal working-day trial with latency/recovery results |
| Later installation/updates work | Clean-machine install, startup, upgrade, settings preservation and failed-update recovery checks |
| Later multi-account logic works | Both account orderings, conflicting states, one/both failures, restart and expiry tests; separate-device live trial |

## Unresolved questions

| Input | How to resolve it |
| --- | --- |
| Current failure, Windows version and installation method | User symptoms plus local build/runtime diagnosis |
| Whether accounts belong to different organisations and permit Graph access | User account context and separate delegated sign-in tests |
| Central host and awake/network availability | User chooses host; verify internet and bridge reachability |
| Light/group, brightness, fallback, working hours, startup/shutdown policy | User preferences and physical bridge test |
| Meaning of idle, meeting handling, acceptable delay and freshness timeout | Agree mappings and measurable thresholds with the user before relevant acceptance checks |
| Distribution identity, signing and hosting | Evaluate after the initial working build |

Collect inputs for the active phase only. External references: [Graph presence](https://learn.microsoft.com/en-us/graph/api/presence-get?view=graph-rest-1.0), [Presence.Read](https://learn.microsoft.com/en-us/graph/permissions-reference#presenceread), [tenant consent](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/configure-user-consent), [Hue setup](https://developers.meethue.com/develop/get-started-2/). Recheck requirements during implementation.
