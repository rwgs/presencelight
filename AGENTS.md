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

Run from the repository root on Windows. These are project-targeted baseline commands, not a claim that a successful build or launch has been verified:

```powershell
dotnet --info
dotnet restore .\src\DesktopClient\PresenceLight\PresenceLight.csproj
dotnet build .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug --no-restore -p:ChannelName=Standalone
dotnet run --project .\src\DesktopClient\PresenceLight\PresenceLight.csproj -c Debug -p:ChannelName=Standalone
git diff --check
git status --short
```

No automated test project or dedicated formatting/lint gate was found during the initial inspection. Establish the focused regression-test command when introducing tests and record it here; do not report a solution-level test command as proof of coverage. Build diagnostics run through the project build. Do not run publishing workflows as validation.

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
