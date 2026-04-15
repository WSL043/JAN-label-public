# Windows Shell

`apps/windows-shell` is the Windows-native workstation shell prototype for JAN Label.

Current scope:

- traditional Windows menu/ribbon/docking shell
- module navigation for Job Setup / Designer / Batch Manager / History
- native property inspector language and document chrome
- shell-only scaffolding while Rust dispatch/audit/template commands are still owned by `apps/desktop-shell`

Current non-goal:

- replacing the Rust proof/print/audit backend in this folder

The existing `apps/admin-web` + `apps/desktop-shell` path remains the operational baseline until the Windows shell reaches feature parity.
