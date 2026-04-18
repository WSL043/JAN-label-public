# 0014 WPF Shell Theme And Chrome Reset

- Status: Accepted
- Date: 2026-04-17

## Context

The first `v0.3.1` single-installer preview still felt like an internal prototype even after the WPF migration:

- too much of the shell read as hand-built `Border` and `Grid` chrome
- the ribbon-heavy top frame still did not match the "traditional Windows workstation" feel operators expected
- the user-facing requirement tightened to "free packages only" for shell chrome and control styling

At the same time, the backend companion architecture was already good enough for preview work:

- `apps/windows-shell` remains the only visible operator app
- `desktop-shell.exe` remains an internal packaged companion
- proof / print / audit / catalog authority does not move

## Decision

For the `v0.3.1` preview line, reset the visible WPF shell chrome to a free packaged theme and standard desktop frame.

Rules:

- `AdonisUI` + `AdonisUI.ClassicTheme` own the top-level window styling baseline.
- The main shell should prefer a standard Windows desktop layout:
  - menu bar
  - toolbar
  - split-pane workspace
  - status bar
- `Dirkster.AvalonDock` remains the docking/document baseline for the designer lane.
- `PropertyTools.Wpf` remains the property-inspector baseline for the designer lane.
- `Fluent.Ribbon` is no longer the primary shell-chrome dependency for operator-facing WPF previews.

## Consequences

- The visible workstation should feel closer to a conventional Windows line-of-business app and less like a custom dashboard shell.
- Future WPF UI polish should lean on packaged control/theme primitives first and only add custom styles where the package baseline is insufficient.
- The repository still keeps the shell logic, workspace models, and companion flow already built for `apps/windows-shell`; this is a chrome reset, not a backend rewrite.
- Existing docs that described the shell as ribbon-first need to be updated to the new menu/toolbar-first baseline.
