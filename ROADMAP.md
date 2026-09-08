# PresenceLight roadmap

Status: Planned, 2026-09-08. No restoration milestone is validated. [SPEC.md](SPEC.md) defines correctness; [TASKS.md](TASKS.md) tracks current work.

## Phase 1: one business account reliably controls Hue

### Outcome

A usable Windows desktop build reads one actual business account and controls the existing Hue Bridge directly.

### Included work

- Diagnose the actual failure, establish a reproducible build and repair the existing desktop implementation.
- Restore delegated sign-in, token persistence/renewal and presence retrieval.
- Pair the bridge, select a light/group, verify manual colours and connect presence updates.
- Handle stale status, reauthentication, network/bridge interruption and sleep/resume.
- Deliver standard-user operation and setup instructions.

### Dependencies and risks

Actual account/tenant access, colour-capable Hue hardware, bridge connectivity and build/runtime prerequisites are unverified. The chosen monitor must stay awake. No desktop failure has yet been reproduced; the README's web authentication warning is not a desktop diagnosis.

### Exit criteria

The Phase 1 acceptance outcomes in [SPEC.md](SPEC.md) pass, including physical light checks, failure visibility, recovery and a normal working-day trial. Record environmental limitations. Multi-account support and automatic updates are not prerequisites.

### Validation

Desktop and affected shared-project builds; focused regressions for repaired behaviour; actual sign-in, Hue, restart, authentication-expiry, connectivity and sleep/resume trials. Record observed latency against agreed thresholds.

## Phase 2: per-user installation and automatic updates

### Outcome

The working app can be installed, started at sign-in and updated on the intended Windows device with a verified recovery path.

### Included work

- Evaluate existing MSIX/App Installer packaging and a standalone option where appropriate.
- Establish this fork's package identity, signing and hosting.
- Validate dependencies, standard-user installation, optional startup, update timing, restart and settings/token preservation.
- Document failed-update recovery and avoid unexpected interruption during busy periods.

### Dependencies and risks

Requires Phase 1's working build. Device policy, package trust and runtime installation may affect administrator requirements. Existing upstream publishing services and credentials cannot be assumed available.

### Exit criteria

Clean-machine installation and an actual upgrade succeed under the intended user permissions; startup and data preservation work; failed-update recovery is demonstrated.

### Validation

Install/update trials on the target environment, signing/trust checks, interrupted/failed update tests and monitoring continuity checks. [App Installer reference](https://learn.microsoft.com/en-us/windows/msix/app-installer/auto-update-and-repair--overview).

## Phase 3: combine both business accounts across devices

### Outcome

One monitor reads each account independently and drives one shared light using the agreed any-busy/all-available policy.

### Included work

- Prove second-account Graph access before substantial redesign.
- Add explicit account identities, independent health/freshness and intentional cached-account selection.
- Add account management UI and a testable aggregation component.
- Isolate failures and migrate existing single-account settings/authentication appropriately.

### Dependencies and risks

Requires Phase 1. Tenant policy may differ between accounts. Resolve idle/meeting mappings and stale-status policy before validation. Separate Teams computers do not inherently require remote agents.

### Exit criteria

Both real accounts work, either can keep the light red, both must be confirmed available for green, and a failed account cannot silently clear red to green. UI explains the combined state and account health.

### Validation

Automated conflicting states in both orderings, simultaneous changes, one/both failures, reauthentication, restart and expiry. Repeat the live trial with Teams on separate devices.

## Ordering after Phase 1

Phases 2 and 3 can be reordered according to user priority. A release pipeline must not delay the initial useful build.

## Later extensions, not scheduled

Other direct light APIs and optional Home Assistant output can follow. Preserve useful existing light integrations and isolate device failures. Investigate remote agents/local Teams logs only if Graph feasibility or device placement demonstrates a need; account attribution, log rotation and client-version resilience require proof. [DECISIONS.md](DECISIONS.md) records the fallback constraints.
