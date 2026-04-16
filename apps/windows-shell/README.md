# Windows Shell

`apps/windows-shell` is the Windows-native workstation shell prototype for JAN Label.

Current scope:

- traditional Windows menu/ribbon/docking shell
- module navigation for Home / Designer / Print Console / Batch Jobs / History
- document tabs, label canvas, toolbox, object browser, and property grid
- bottom records/messages/status panes so operators can evaluate a full workstation shape instead of placeholder chrome
- shell-only scaffolding while Rust dispatch/audit/template commands are still owned by `apps/desktop-shell`
- GitHub Windows CI publishes both a self-contained preview build artifact and an installer artifact for operator UI review

Current non-goal:

- replacing the Rust proof/print/audit backend in this folder

The existing `apps/admin-web` + `apps/desktop-shell` path remains the operational baseline until the Windows shell reaches feature parity.

Packaging note:

- `windows-shell-native` on GitHub Actions is the authoritative preview packaging path for now
- the job publishes a self-contained app artifact and an Inno Setup installer artifact
- installer previews are for UI/UX validation, not the production proof/print release path
