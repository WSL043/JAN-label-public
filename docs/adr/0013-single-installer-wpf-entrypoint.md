# 0013 Single Installer WPF Entrypoint

- Status: Accepted
- Date: 2026-04-17

## Context

After `v0.3.0`, the repository was still exposing two different Windows products to operators:

- the older `apps/desktop-shell` installer
- the newer `apps/windows-shell` installer

That split made the UI problem worse than the styling problem:

- operators could open the wrong app
- the release page looked like two parallel frontends instead of one workstation
- the older desktop shell UI could still dominate first impressions even though WPF was the intended direction

At the same time, `apps/windows-shell` still depends on `desktop-shell.exe` as its companion backend for proof, audit, catalog, and safe-op authority.

## Decision

Adopt a single visible Windows installer strategy for the next post-`v0.3.0` release line.

Rules:

- `apps/windows-shell` is the only user-facing Windows app entrypoint.
- The packaged WPF executable is named `JanLabel.exe`.
- `desktop-shell.exe` remains packaged only as an internal companion binary for WPF.
- Release automation must validate and ship the companion binary, but must not publish the old desktop-shell UI as a second end-user installer.
- Future Windows release assets should present one operator-facing installer by default.

## Consequences

- The public release surface becomes simpler: one Windows app, one installer, one launch target.
- WPF shell polish now directly improves the product operators see, instead of competing with a transitional desktop-shell installer.
- `apps/desktop-shell` still remains the backend authority path until a later ADR replaces the companion design.
- CI and release workflows now need to:
  - build the desktop-shell companion binary
  - publish WPF output with `desktop-shell.exe` staged beside `JanLabel.exe`
  - generate only the WPF installer as the user-facing Windows artifact
