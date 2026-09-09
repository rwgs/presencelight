# Project instructions

## Purpose

Restore the PresenceLight Windows desktop app so one Microsoft 365 business account reliably controls the user's existing Hue Bridge. Multiple business accounts on separate Teams devices come later. Read [DECISIONS.md](DECISIONS.md) before revisiting the starting repository or architecture.

## Architecture

- `src/DesktopClient/PresenceLight/`: WPF desktop shell, tray behaviour, startup and polling orchestration in `MainWindow.xaml.cs`.
- `src/PresenceLight.Razor/`: shared Blazor settings and UI components.
- `src/PresenceLight.Core/`: Graph authentication/presence, configuration and light services. Follow the existing MediatR request/handler patterns where applicable.
- `src/DesktopClient/PresenceLight.Package/`: Windows packaging and App Installer configuration.
- `src/PresenceLight.Web/`: web host; initial restoration is desktop only. Check affected shared consumers without expanding into web authentication restoration.
- `Build/` and `.github/workflows/`: existing publishing infrastructure; upstream credentials, signing and hosting are not assumed available.
- Build outputs under `bin/` and `obj/` are generated. Regenerate resource/settings designer files through their source inputs when changes require it.
- The desktop project declares `net10.0-windows10.0.19041`; desktop build properties enable WPF/Windows Forms and preview C#. It embeds Blazor through WebView2. Actual SDK/runtime prerequisites and supported deployment versions remain to be validated.

## Working boundaries

- Preserve unrelated changes. Make the smallest coherent repair and follow existing naming and layout.
- Remove code orphaned by the current change; leave unrelated cleanup for separate work.
- Never commit or print credentials, tokens, sessions, private account data or secret-bearing configuration.
- Do not ship a client secret in a public desktop app. Preserve current-user token protection.
- Use direct Hue control. Home Assistant is optional future work, and the sibling pyTeamsStatus repo is a fallback reference.
- Defer multi-account abstractions until their phase; avoid adding new first-account assumptions while repairing existing code.
- Failed or stale presence must not silently count as available. One coordinator owns the shared light.
- Ask before destructive work or settling an unresolved product/architecture decision unless already authorised by the user. Routine edits and renames requested by the user are authorised.

## Before editing

- State the intended result and how it will be checked.
- Read the code and relevant decisions first. Investigate unread facts rather than asking the user to supply facts the repository can answer.
- Resolve consequential ambiguity with the user; continue independent work while waiting. State routine assumptions.
- Raise a simpler approach or an incorrect premise before implementing unnecessary work.

## Commands

Run from the repository root on Windows. The restore, build and launch below were verified on 2026-09-08; see T1 in [TASKS.md](TASKS.md) for the recorded output.

The .NET 10 SDK is installed for the current user only at `%LOCALAPPDATA%\Microsoft\dotnet` (SDK 10.0.401 with the 10.0.12 NETCore, AspNetCore and WindowsDesktop runtimes) and is deliberately not on the machine `PATH`. Start each shell session with:

```powershell
$env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
$env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
```

`DOTNET_ROOT` is required to run the built executable. Without it the apphost searches the machine-wide runtime location and exits with `0x80008083` (`CoreHostLibMissingFailure`) before any application code runs; that exit code is a toolchain symptom, not an application fault. `run-presencelight.cmd` sets it and starts the application from the repository root, so the built executable can also be launched from Explorer.

The application the user runs day to day is a self-contained copy installed in `%USERPROFILE%\PresenceLight`, which keeps its own `settings.json`. A source change only reaches it after a publish and copy, so use `install-local-build.ps1` for that; it publishes the same self-contained build as the artifact workflow, stops the running instance, preserves the installed `settings.json` and restarts the application.

```powershell
dotnet --info
dotnet restore .\src\DesktopClient\PresenceLight\PresenceLight.csproj
dotnet build .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug --no-restore -p:ChannelName=Standalone
dotnet run --project .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug -p:ChannelName=Standalone
dotnet test .\src\PresenceLight.Core.Tests\PresenceLight.Core.Tests.csproj
.\Build\scripts\run-presencelight.cmd
.\Build\scripts\install-local-build.ps1
git diff --check
git status --short
```

`dotnet test .\src\PresenceLight.Core.Tests\PresenceLight.Core.Tests.csproj` is the focused regression command. It is an xunit project covering `PresenceLight.Core` and holds 17 tests as of 2026-09-09, all passing. It covers only what has been deliberately made testable, currently the presence freshness decision, so it is not evidence of coverage for anything else; state what a change actually exercised. There is no dedicated formatting or lint gate. Build diagnostics run through the project build. Do not run publishing workflows as validation.

Behaviour that depends on Windows, WPF or the tray lives in the desktop project and is not covered by these tests. When repairing such behaviour, extract the decision into `PresenceLight.Core` where it can be tested, as the freshness tracker is, and leave only the wiring in the desktop project.

## Validation

- For code repairs, reproduce the failure and run focused checks, then the desktop build and affected shared-project checks.
- Verify UI changes with screenshots or equivalent inspection. Graph sign-in and Hue state changes require separate live checks.
- For documentation-only work, validate changed links, filenames and whitespace; application builds and live hardware tests are unnecessary.
- Inspect final status/diff, including untracked files. Report skipped checks and outstanding manual validation.
- Mark tasks complete only after their stated acceptance and required validation pass.

## Documentation routing

- [SPEC.md](SPEC.md): requirements, acceptance criteria and unresolved product questions.
- [ROADMAP.md](ROADMAP.md): phase order and exit criteria.
- [TASKS.md](TASKS.md): current work and validation status.
- [PLAN.md](PLAN.md): approach for the current restoration effort; replace for the next substantial change.
- [DECISIONS.md](DECISIONS.md): durable choices and rejected alternatives.
- [CHANGELOG.md](CHANGELOG.md): shipped user-visible changes; never list planned repairs as released.
- [README.md](README.md): entry point and navigation.
