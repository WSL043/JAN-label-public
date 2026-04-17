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
- For shell chrome, prefer a real Windows ribbon package over hand-crafted imitation when the dependency is compatible and clearly licensed. Current shell-chrome choice: `Fluent.Ribbon`.
- For the designer frame, prefer package-backed docking and inspector controls over hand-built faux panes when the dependency is compatible and clearly licensed. Current designer-shell choices: `Dirkster.AvalonDock` for docking/documents and `PropertyTools.Wpf` for the right-side property inspector baseline.

## Consequences

- The repository now carries two UI fronts during migration:
  - transitional web/Tauri operator path
  - Windows-native WPF shell path
- Release packaging remains on the current `desktop-shell` path until the native shell is wired into backend commands.
- Backend parity for `v0.3.0` should use the existing `desktop-shell` binary as a host:
  - add a `--native-shell-companion` stdio JSON mode on the same Rust/Tauri executable
  - keep `apps/desktop-shell` as the sole authority for proof/print gate, audit restore, and template catalog resolution
  - expose read + safe operations to WPF first instead of forking business logic into a second backend path
- Future operator UX work should target the WPF shell first for shell language decisions, then backport only what is required to keep the transitional web path usable.
- The native shell should not remain a placeholder chrome mock. Its baseline should include a practical label-designer frame:
  - left toolbox and object browser
  - center document tabs and design canvas
  - right property grid
  - bottom records, messages, and operator status
- The practical designer baseline should be package-backed where possible:
  - ribbon/backstage from `Fluent.Ribbon`
  - docked tool windows and documents from `Dirkster.AvalonDock`
  - property inspector from `PropertyTools.Wpf`
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
  - shared shell chrome should follow the current in-lane selection, not only the active module default
  - the shell title and lead text should describe the current focused work item when a lane selection exists
- Template-library reasoning should not be hidden behind designer state alone:
  - `Home` and `Designer` should expose the same packaged-vs-local library board language
  - the winning default, draft-only state, dispatch eligibility, and rollback path should be visible from a single selection panel
- Operational lanes should not leave the right-side detail pane static:
  - selecting a proof, dispatch job, import session, backup bundle, or ledger row should update blocker, route, and next-action context in place
  - lane focus should help an operator decide what to do next without scanning a separate document or modal first
- Shell actions may route operators to the appropriate lane for review, but that routing must not imply backend authority moved out of `apps/desktop-shell`
- Shell actions should also prefer landing on the relevant in-lane object, not only the target module:
  - template and overlay actions should focus the relevant template entry
  - proof, queue, and restore actions should focus the relevant proof, job, batch, bundle, or audit row
  - designer actions should focus the relevant canvas object when a reasonable default exists
- Native-shell parity should move through explicit release scope instead of silently implying full backend ownership:
  - `Home`, `Designer`, `Print Console`, and `History` may read live desktop-shell state through the companion path
  - `Print Console` should describe an audit-derived proof / dispatch subject view, not imply a native-shell-owned live queue
  - safe mutations for `v0.3.0` are limited to proof approve/reject and audit export
  - direct print dispatch, audit restore, destructive retention apply, and template write-back remain out of scope for direct WPF execution in `v0.3.0`
- Formal release automation should validate more than shell compilation:
  - native-shell build
  - self-contained publish
  - installer generation
  - GitHub Release asset upload on tagged releases
