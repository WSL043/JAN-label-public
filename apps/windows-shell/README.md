# Windows Shell

`apps/windows-shell` is the Windows-native workstation shell prototype for JAN Label.

Current scope:

- traditional Windows menu/ribbon/docking shell
- module navigation for Home / Designer / Print Console / Batch Jobs / History
- module-specific operator workspaces instead of a single static mock:
  - Home migration and readiness dashboard
  - Designer canvas, toolbox, object browser, property grid, and catalog snapshot
  - Print Console proof queue, dispatch grid, and guardrail inspector
  - Batch Jobs import sessions, queue table, and retry guardrails
  - History proof review, audit ledger, retention, and restore visibility
- shared context strip and status strip that keep lane purpose, authority, route, and active blocker signals visible in shell chrome
- shell-level mock data that lets operators evaluate lane density and information architecture before backend wiring lands
- shell-only scaffolding while Rust dispatch/audit/template commands are still owned by `apps/desktop-shell`
- GitHub Windows CI publishes both a self-contained preview build artifact and an installer artifact for operator UI review

Current non-goal:

- replacing the Rust proof/print/audit backend in this folder

The existing `apps/admin-web` + `apps/desktop-shell` path remains the operational baseline until the Windows shell reaches feature parity.

Packaging note:

- `windows-shell-native` on GitHub Actions is the authoritative preview packaging path for now
- the job publishes a self-contained app artifact and an Inno Setup installer artifact
- installer previews are for UI/UX validation, not the production proof/print release path
