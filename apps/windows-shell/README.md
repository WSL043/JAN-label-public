# Windows Shell

`apps/windows-shell` is the Windows-native workstation shell prototype for JAN Label.

Current scope:

- traditional Windows menu/ribbon/docking shell
- `Fluent.Ribbon` now owns the shell chrome baseline so the app uses a real Windows ribbon/backstage/quick-access model instead of a hand-drawn imitation
- `Dirkster.AvalonDock` now owns the designer docking language so the shell uses real tool windows, docked inspectors, and document tabs instead of a fixed mock grid
- `PropertyTools.Wpf` now owns the right-side designer inspector baseline so property review is moving toward a real Windows property-grid surface
- module navigation for Home / Designer / Print Console / Batch Jobs / History
- module-specific operator workspaces instead of a single static mock:
  - Home migration and readiness dashboard
  - Designer docked toolbox, template library board, object browser, data-source panes, design documents, and package-backed property inspector
  - Print Console proof queue, dispatch grid, and guardrail inspector
  - Batch Jobs import sessions, queue table, and retry guardrails
  - History proof review, audit ledger, retention, and restore visibility
- shared context strip and status strip that keep lane purpose, authority, route, and active blocker signals visible in shell chrome
- designer canvas selection now drives the property inspector, so the inspector is no longer a static metadata pane
- Home and Designer now share a reusable template-library board that makes default winner, draft-only state, dispatch safety, authority owner, and rollback path visible from one panel
- ribbon, quick-access, and header actions now route operators toward the relevant lane and produce visible shell feedback instead of dead UI buttons
- Print Console, Batch Jobs, and History now use selection-driven detail boards so choosing a proof, job, import session, bundle, or ledger row updates blocker, route, and next-action context instead of leaving the right pane static
- shell-level mock data that lets operators evaluate lane density and information architecture before backend wiring lands
- shell-only scaffolding while Rust dispatch/audit/template commands are still owned by `apps/desktop-shell`
- GitHub Windows CI publishes both a self-contained preview build artifact and an installer artifact for operator UI review

Current non-goal:

- replacing the Rust proof/print/audit backend in this folder

The existing `apps/admin-web` + `apps/desktop-shell` path remains the operational baseline until the Windows shell reaches feature parity.

Packaging note:

- `windows-shell-native` on GitHub Actions is the authoritative preview packaging path for now
- the job publishes a self-contained app artifact and an Inno Setup installer artifact
- formal tag releases now also build and upload the native-shell installer asset through `.github/workflows/release.yml`
- installer previews are for UI/UX validation, not the production proof/print release path

Release target:

- the next formal release target is `v0.3.0`
- `v0.3.0` is intended to be the first release where the native shell is treated as the primary operator shell direction, with pre-release sub-agent review required before formal announcement
