# PresenceLight tasks

Status: T1, T2 and T4 complete; the desktop application builds, signs in, reads presence and drives the paired Hue light. T5 needs the agreed mapping and the busy/DND/in-call transitions; T6 needs the freshness and fallback policy. Phase order lives in [ROADMAP.md](ROADMAP.md), requirements in [SPEC.md](SPEC.md), and the current approach in [PLAN.md](PLAN.md).

## Current phase: single-account restoration

- [ ] T3: Verify authentication across restart and reauthentication.
  - Progress 2026-09-09: the cached restart works. The installed application was stopped and replaced with a new build at 00:14, and the restarted process authenticated from the protected MSAL cache with no prompt and no sign-in click, reaching the profile/presence batch 4 seconds after loading settings. Settings persistence across restarts also holds for the installed copy: the client and tenant identifiers written at 20:06 the previous evening were still loaded, because the application is launched with its own folder as the working directory. Still unverified: sign-out clearing the intended account, visible required reauthentication, and revoked or expired access never counting as available.
  - Scope: protected cache, silent renewal, sign-out, revoked/expired access and explicit reconnect state.
  - Acceptance: cached restart works, sign-out clears the intended account, required reauthentication is visible, and failures never count as available.
  - Automated validation: focused cache/error-state tests where meaningful; relevant builds.
  - Manual validation: real restart, renewal and reauthentication checks; confirm diagnostics contain no tokens.
  - Dependencies: T2. Note from T1: the standalone build saves `settings.json` to `Directory.GetCurrentDirectory()` ([StandaloneSettingsService.cs:18](src/DesktopClient/PresenceLight/Services/Settings/StandaloneSettingsService.cs#L18)), so saved configuration follows the working directory of whatever started the application rather than a per-user location. The MSAL token cache, by contrast, lives under `%LOCALAPPDATA%\PresenceLight\` ([TokenCacheHelper.cs:19](src/PresenceLight.Core/GraphServices/TokenCacheHelper.cs#L19)). Confirm which behaviour is intended before relying on restart persistence, and revisit it in T8 where a standard user may launch from a directory they cannot write to.

- [ ] T5: Connect presence transitions to the physical light.
  - Progress 2026-09-09: presence already drives the light. Between 00:39 and 00:57 the light alternated between `00ff00` and `ffff00` five times with no manual involvement, which is the account's own presence moving between available and idle, and each write was accepted by the bridge. Working hours have since been turned off in the installed settings, so monitoring is no longer confined to 08:00-18:30, and the selected light has since been changed to `id:/lights/4` from the `id:/lights/2` that the pairing run saved. What this does not yet establish is the mapping or the latency. The log recorded only the resulting colour, and the installed settings give several statuses the same colour, so a light change could not be traced back to the presence that caused it: `ActivityBusyStatus` and `ActivityInAMeetingStatus` are both `#ff0000`, and `ActivityAwayStatus`, `ActivityInactiveStatus` and `ActivityBeRightBackStatus` are all `#ffff00`. The polling loop now logs each presence change and how long the previous one was held, which makes the mapping and the observed delay checkable after the fact. Verified on the installed build at 01:14:52, which logged `Presence read as Away/Away` immediately before setting the light to `ffff00`, so the yellow observed all evening comes from `ActivityAwayStatus`. Still outstanding: real busy, DND, in-call and presenting transitions, the agreed mapping for idle and meetings, and a measured Graph-to-light delay, which needs the time a status change was made in Teams as well as the time the application saw it.
  - Scope: existing mapping and polling, including available, busy, DND and in-call behaviour.
  - Acceptance: physical light follows the agreed single-account mapping; observed latency is recorded.
  - Automated validation: focused state-mapping checks for changed logic.
  - Manual validation: exercise transitions on the actual business account and Hue light.
  - Dependencies: T2 and T4; agree brightness and relevant mappings.
  - Note for T7, from the same logs rather than an observed failure: the loop writes the current colour to the bridge on every iteration, 516 times in the hour observed, whether or not the presence changed. That is a crude form of the reconciliation T7 requires, but it is also the only reason the log shows the loop is alive, so removing it needs the freshness tracking in T6 first.

- [ ] T6: Define and implement freshness/failure indication.
  - Observed 2026-09-08: a paused application is indistinguishable from a broken one. The installed settings enable working hours of 08:00-18:30 Monday to Friday, and outside them the polling loop takes no action and writes no log line at all, so presence reads simply stop with nothing in the UI, the tray icon or the log to say why. This is what interrupted the T2 run: the working-hours checkbox is bound straight to the configuration, so ticking it took effect on the next iteration at 20:22:30, 40 seconds before the save at 20:23:12, and 20:22 is after 18:30. Nothing was hung. The same code path also drops the end-of-day action after a restart, because `previousWorkingHours` is a local variable that starts false in every process, so an application started outside working hours never applies `HoursPassedStatus` (currently `Off`) and the light keeps whatever colour it last had.
  - Scope: last-success tracking, fallback colour/timeout, startup/shutdown and working-hours behaviour.
  - Acceptance: failed/stale reads never silently turn green; UI explains unknown and reauthentication states.
  - Automated validation: controlled fresh, failed and expired-read cases against agreed thresholds.
  - Manual validation: confirm fallback and visible status on the physical setup.
  - Dependencies: agree fallback, freshness timeout and lifecycle policies from SPEC.md.

- [ ] T7: Recover polling and light state after interruptions.
  - Note from the T2/T3 runs, from code reading rather than an observed failure: the loop in [MainWindow.xaml.cs:504](src/DesktopClient/PresenceLight/MainWindow.xaml.cs#L504) breaks and clears `isInteractRunning` whenever `SignedIn` is false, and only `LoadApp` or a sign-in request starts it again. [Index.razor:81](src/PresenceLight.Razor/Components/Pages/Index.razor#L81) sets `SignedIn` back to true on its own when the cached account still authenticates, without restarting the loop, so a sign-out followed by a page initialisation can leave the application signed in with no polling and no sign-in button. The loop also has no timeout or cancellation around its Graph and light calls, and the constructor runs a second unbounded `while (true)` flag-polling loop with a `Thread.Sleep(100)` in it, which is what consumes CPU while idle.
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
  - Known environmental limitation: a locally built, unsigned executable was blocked by Microsoft Defender Attack Surface Reduction on a second, managed device, including when run from the user's home directory. Relocating the files does not help, because the relevant rule judges the file's signature, prevalence and age rather than its path. This does not affect the monitoring design: presence is read from Graph per account, so the application only needs to run on one machine the user controls. Treat installation on a managed device as out of scope unless the build is code signed or the device's administrator adds an exclusion; see [publisher-verification.md](docs/publisher-verification.md).
  - Dependencies: T1-T7 and agreed latency/freshness acceptance thresholds. Installer/update publication belongs to a later phase.

## Parked

- Remove the Blazorise dependency. It is used for two things only: the per-status colour swatch in [Statuses.razor:29](src/PresenceLight.Razor/Components/Shared/Statuses.razor#L29), shared by the Hue, LIFX, Wiz and Yeelight pages, and two `<Label>` elements in [CustomApiSetup.razor](src/PresenceLight.Razor/Components/Pages/CustomApiSetup.razor). Both have direct replacements: `ColorEdit` renders an `<input type="color">`, so a plain input keeps the existing `ChangeStatusColor` handler unchanged, and `<Label>` becomes `<label>`, which is already the idiom beside those inputs. Removing it also removes the free-tier licence notice the library prints at startup, two package references, one `@using`, three service registrations and six stylesheet links across the desktop and web hosts. Bootstrap styling does not come from Blazorise, so those stylesheets can go without visual loss. Parked at the user's request on 2026-09-09 because `Statuses.razor` is the page the T5 colour validation runs against; revisit once presence colours are confirmed working.

## Upstream contributions

Three repairs made here apply unchanged to [isaacrlevin/presencelight](https://github.com/isaacrlevin/presencelight) and were opened against its `main` on 2026-09-09. Each branch was cut from `upstream/main` and carries only its own change, so none of them include this project's planning documents, the funding file or the artifact workflow. No work here depends on them being merged; they are recorded so the same repairs are not made twice when upstream moves.

- [PR 988](https://github.com/isaacrlevin/presencelight/pull/988), branch `feat/978-entra-app-registration`: creating the Entra application registration from Settings, which is what upstream issue 978 asks for. See T2.
- [PR 989](https://github.com/isaacrlevin/presencelight/pull/989), branch `fix/987-language-resource-packages`: removes `Language` from the packaging project's automatic resource package qualifiers, so the MSIX bundle stops emitting the per-language resource packages whose `resources.pri` fails to merge during installation (upstream issue 987). This one corresponds to no task here, because the restoration work does not touch packaging, and it is not build-verified: the packaging project needs MSBuild with the Desktop Bridge targets, which this machine does not have. The language-qualified content is entirely .NET framework satellite assemblies, 17.9 MB across 13 languages, since the application has no localised resources of its own; `SatelliteResourceLanguages` would remove them instead of duplicating them into each architecture package, but that is upstream's decision to make and the pull request offers it as an alternative.
- [PR 990](https://github.com/isaacrlevin/presencelight/pull/990), branch `fix/969-hue-bridge-link-window`: the link-window retry and the pairing messages. It references upstream issue 969 rather than closing it, because that report also covers the Hue remote/cloud API, which is untouched here. See T4.

## Validation status

| Area | Result |
| --- | --- |
| Initial source/repository comparison | Read-only inspection completed; rationale recorded in DECISIONS.md and PLAN.md |
| Desktop restore and build | Passed 2026-09-08 on .NET SDK 10.0.401: 0 errors, 127 warnings, 23.9 s |
| Desktop launch | Passed 2026-09-08: window renders, no startup error; requires `DOTNET_ROOT` for the per-user toolchain |
| Automated application tests | Not run; no test project exists |
| Actual Graph account access | Passed 2026-09-08: interactive sign-in, one profile/presence batch and 157 consecutive presence reads, with the missing-photo case exercised |
| Graph token recovery across restart and reauthentication | Cached restart passed 2026-09-09, twice: at 00:14 and again at 01:13 the replaced build authenticated from the protected cache with no prompt. Sign-out, visible reauthentication and revoked/expired access are outstanding in T3 |
| Presence polling stability | Passed 2026-09-08 for the observed window: 157 consecutive reads over ~14 minutes, then correctly idle outside working hours, which the application does not report; see T6 |
| Hue pairing and physical transitions | Passed 2026-09-09 after the link-window repair: bridge key stored, lights enumerated, selection saved, and manual colour changes accepted by the bridge between 00:24 and 00:31. Presence-driven available/idle transitions followed between 00:39 and 00:57; busy, DND and in-call transitions and the measured delay are outstanding in T5 |
| Recovery, latency and working-day trial | Not tested |
| Per-user installer and automatic updates | Not tested; later phase |
| Second account and combined rules | Not implemented/validated; later phase |

## Completed

- [x] T4: Pair the bridge and control the selected light manually. Completed 2026-09-09.
  - Result: the bridge pairs and the selected light changes colour under direct local control, with no Home Assistant involved. Three defects in that path were repaired to get there.
  - Evidence: pairing succeeded at 00:17 on the repaired build, a bridge key was stored, the lights enumerated and a selection was saved. Manual colour changes were then written to the light between 00:24 and 00:31, including colours chosen in the interface as well as the configured busy and do-not-disturb colours. Each of those writes is recorded only after the bridge call returned, because [HueService.cs](src/PresenceLight.Core/Lights/HueServices/HueService.cs) logs `Setting Hue Light` after `UpdateAsync` completes and logs an error and rethrows otherwise, so every recorded write was accepted by the bridge.
  - Repairs: [HueService.cs](src/PresenceLight.Core/Lights/HueServices/HueService.cs) made a single registration attempt, which fails whenever it races the physical button press; it now retries every 2 seconds for the bridge's 30-second link window, so the button can be pressed before or after confirming the dialog. [HueSetup.razor](src/PresenceLight.Razor/Components/Pages/HueSetup.razor) logged success before checking whether a key had been returned, and showed nothing while registration was in progress; it now logs success only when a key exists and shows a waiting message. `RegisterBridge` also returned an empty key without throwing when the window closed without a press, so the catch never ran and the waiting message stayed on screen indefinitely, leaving a timed-out pairing indistinguishable from one still in progress; it now reports the expiry.
  - Not exercised: the link-window expiry message, because the verified run paired successfully rather than timing out.
  - Two earlier attempts at 00:12 and 00:13 failed with `LinkButtonNotPressedException` in the same way as the previous evening; both were still on the old build, which was replaced at 00:14.
  - Upstream: the whole repair went to [PR 990](https://github.com/isaacrlevin/presencelight/pull/990).

- [x] T2: Make delegated sign-in and one real presence read work. Completed 2026-09-08.
  - Result: an application registration created from the Settings page in the user's own tenant signs in and returns presence, which confirms the diagnosis in [PLAN.md](PLAN.md) that the original failure was the retired upstream registration rather than a code fault.
  - Evidence: in the recorded run, interactive sign-in completed at 20:08:24, the profile/presence batch returned at 20:08:44, and the polling loop then performed 157 presence reads between 20:08:56 and 20:22:30 without logging an error. `SetColor` and `MapUI` dereference `Presence.Availability` on every iteration, so a null or failed presence would have been logged; none was.
  - Missing photo: covered by the same run, which logged "Profile photo does not exist" and continued to presence, so absent optional profile data does not stop monitoring.
  - Consent: tenant-wide consent returned Forbidden because the account may register applications but holds no role that can consent for the organisation. This did not block sign-in, because both requested permissions are user-consentable and consent at first sign-in was sufficient. Neither `AADSTS900971` nor the sign-in loop reported in upstream issue 978 occurred.
  - Cancelled sign-in: an earlier attempt in the same run was cancelled at the browser (`authentication_canceled` at 20:08:15) and the next attempt succeeded, so a cancelled sign-in does not require restarting the application.
  - Note carried forward: the application the user runs is the self-contained copy in the user profile rather than the repository build, so a source change only reaches it after a publish and copy. [install-local-build.ps1](Build/scripts/install-local-build.ps1) now does that in one command and keeps the installed `settings.json`, which holds the configured client and tenant identifiers.
  - Upstream: the setup automation this task produced was offered to upstream as [PR 988](https://github.com/isaacrlevin/presencelight/pull/988), because upstream issue 978 asks for exactly this. The pull request states plainly that tenant-wide consent returned Forbidden in the verified run and that the multi-tenant and second-tenant consent paths remain unexercised.

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
