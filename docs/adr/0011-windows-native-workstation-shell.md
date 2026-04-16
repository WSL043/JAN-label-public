# 0011 Windows-Native Workstation Shell

- Status: Accepted
- Date: 2026-04-16

## Context

The current `apps/admin-web` + Tauri shell is operational, but its interaction model still reads like a web console even after several density and chrome passes.

For operator-facing label software, the target reference is no longer an AI-desktop shell. The target is traditional Windows label software:

- native menu / ribbon / docking language
- fixed desktop workspace widths
- property-grid and inspector-first interaction
- Windows-only release posture for the shell layer

The Rust proof, print, render, audit, and template-catalog authority does not change.

## Decision

Introduce `apps/windows-shell` as the Windows-native workstation shell, implemented in C# WPF.

Rules:

- `apps/windows-shell` owns the future operator shell language.
- `apps/admin-web` remains a transitional UI path until the Windows shell reaches feature parity.
- `apps/desktop-shell` keeps owning proof/print gate, audit restore, and template-catalog authority until an explicit backend migration ADR supersedes it.
- Desktop shell layout decisions may stop optimizing for mobile or non-Windows viewport compromises.
- Validation for `apps/windows-shell` is authoritative on GitHub Windows runners when local `dotnet` is unavailable.

## Consequences

- The repository now carries two UI fronts during migration:
  - transitional web/Tauri operator path
  - Windows-native WPF shell path
- Release packaging remains on the current `desktop-shell` path until the native shell is wired into backend commands.
- Future operator UX work should target the WPF shell first for shell language decisions, then backport only what is required to keep the transitional web path usable.
- The native shell should not remain a placeholder chrome mock. Its baseline should include a practical label-designer frame:
  - left toolbox and object browser
  - center document tabs and design canvas
  - right property grid
  - bottom records, messages, and operator status
- The shell should quickly move from a single designer mock to lane-aware workspaces so operators can evaluate the whole workstation shape:
  - `Home` migration and readiness dashboard
  - `Designer` authoring surface
  - `Print Console` proof / dispatch lane
  - `Batch Jobs` import / queue lane
  - `History` proof review / audit / retention lane
- The shell should also be judged by operational readability:
  - current authority should be visible without opening another pane
  - blockers should be legible in the current lane
  - route / proof / catalog state should remain visible in shared shell chrome
