# Changelog

For users installing or upgrading builds from this restoration effort. Record shipped user-visible changes here, newest first; internal changes and documentation belong in commit history.

## Unreleased

No release has been published. The changes below are in the local build described in [AGENTS.md](AGENTS.md) and have been validated against a real Microsoft 365 account and a real Hue Bridge.

### Added

- Create the Entra application registration from the Settings page, so signing in no longer depends on the retired upstream registration. The setup signs in with a device code when it has no window available, presents the code, and saves a transcript when it fails. Tenant-wide consent is attempted but is not required: both requested permissions can be consented to by the signing-in user.

### Fixed

- Pairing a Hue Bridge no longer depends on the physical button being pressed at the exact moment the request is made. The application now asks the bridge for the length of its link window, so the button can be pressed before or after confirming the dialog.
- Pairing reports what happened. Success is claimed only once the bridge has actually returned a key, a waiting message appears while the link window is open, and the window closing without a press is reported instead of leaving the waiting message on screen indefinitely.
- Registration setup no longer reports a successful run as a failure. Its final disconnect can warn that it could not clear its own token cache, which happens after every useful step has completed.

Planned work is tracked in [ROADMAP.md](ROADMAP.md) and [TASKS.md](TASKS.md), not as released features here. This file does not reconstruct upstream release history; existing upstream releases are linked from [README.md](README.md).
