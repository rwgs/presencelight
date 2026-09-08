# PresenceLight tasks

Status: Planning complete; application restoration has not started. Phase order lives in [ROADMAP.md](ROADMAP.md), requirements in [SPEC.md](SPEC.md), and the current approach in [PLAN.md](PLAN.md).

## Current phase: single-account restoration

- [ ] T1: Establish a reproducible desktop build and failure report.
  - Scope: record current symptoms, Windows/SDK/runtime details, redacted errors and the failing stage; inspect desktop dependencies and WebView2.
  - Acceptance: documented reproduction and successful baseline build, or a precise build failure to repair before closing the task.
  - Automated validation: desktop restore/build with recorded command and output.
  - Manual validation: launch and compare with the reported failure; reconcile supported OS/prerequisite documentation.
  - Dependencies: actual failure details and development prerequisites are not yet collected.

- [ ] T2: Make delegated sign-in and one real presence read work.
  - Scope: existing public-client registration/configuration, intentional account selection, least permissions and actionable access errors.
  - Acceptance: one actual business account signs in and returns /me/presence; optional profile data does not block it.
  - Automated validation: build and focused regressions for any repaired authentication/profile behaviour.
  - Manual validation: real consent/sign-in, presence retrieval and missing-photo case.
  - Dependencies: T1; organisation policy and app registration are unverified.

- [ ] T3: Verify authentication across restart and reauthentication.
  - Scope: protected cache, silent renewal, sign-out, revoked/expired access and explicit reconnect state.
  - Acceptance: cached restart works, sign-out clears the intended account, required reauthentication is visible, and failures never count as available.
  - Automated validation: focused cache/error-state tests where meaningful; relevant builds.
  - Manual validation: real restart, renewal and reauthentication checks; confirm diagnostics contain no tokens.
  - Dependencies: T2.

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
| Build, launch and automated application tests | Not run |
| Actual Graph account access and token recovery | Not tested |
| Hue pairing and physical transitions | Not tested |
| Recovery, latency and working-day trial | Not tested |
| Per-user installer and automatic updates | Not tested; later phase |
| Second account and combined rules | Not implemented/validated; later phase |

## Completed

- [x] Document the agreed restoration direction and migrate it into the project documentation.
  - Scope: requirements, phase order, current approach/tasks, durable decisions and agent guidance; remove the superseded plan index and its references.
  - Acceptance: root Markdown templates populated and renamed, README navigation updated, proposed work distinguished from shipped behaviour.
  - Validation: documentation filenames, relative links, remaining placeholder/reference checks and whitespace review.
  - Application tests: not applicable to this documentation-only change; no runtime claims are made.
