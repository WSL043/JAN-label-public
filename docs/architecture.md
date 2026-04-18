# architecture

## 1. Principles

- Print core first, shell second.
- The current shipped baseline still relies on Rust for JAN normalization, proof rules, render, print routing, audit, and template-catalog resolution.
- The target architecture is `.NET / WPF` single-stack convergence; see `docs/dotnet-convergence.md`.
- The next formal release target is `v1.0.0`; acceptance is tracked in `docs/release/v1.0.0-acceptance.md`.
- The operator application direction is Windows-only.
- `apps/windows-shell` is the target operator shell and long-term visible operator app.
- `apps/admin-web` remains transitional only; it is not the long-term UX direction.
- `apps/desktop-shell` remains the current authoritative bridge and proof/print gate only until its `.NET` replacement is validated and the legacy slice is retired.

## 2. Runtime Layers

Current shipped baseline:

```text
[apps/windows-shell]       [apps/admin-web (transitional only)]
            |                         |
            +-----------+-------------+
                        |
                        v
              [apps/desktop-shell]
                        |
                        v
                 [crates/print-agent]
                   |-> [crates/domain]
                   |-> [crates/render]
                   |-> [crates/printer-adapters]
                   `-> [crates/audit-log]
```

Target `v1.0.0` runtime:

```text
[apps/windows-shell]
          |
          v
[apps/windows-shell-core]
     |-> [SQLite local state]
     |-> [catalog / artifacts / exports / backups]
     `-> [render / proof / dispatch / audit / import services]
```

## 3. Current Front Ownership

- `apps/windows-shell`
  - operator-facing shell language
  - Windows desktop chrome
  - workstation layout and lane framing
  - package-backed chrome, docking, and inspector surfaces
  - only intended long-term visible operator application
- shared `.NET` support libraries
  - extracted Windows-native workflow logic that should not live in WPF shell files
  - current first slice: `apps/windows-shell-core` for native template catalog read / merge logic
  - current `M1` foundation: LocalAppData path resolution, SQLite bootstrap, migration tracking, and first-pass runtime service contracts
- `apps/admin-web`
  - transitional operational UI
  - still useful for parity checks and backend-connected workflows during migration
  - not the design reference for shell chrome or long-term operator UX
  - should not receive new browser-style shell investment
- `apps/desktop-shell`
  - current final proof gate
  - current dispatch authority
  - current audit restore authority
  - current packaged + local template catalog resolution for dispatch

## 4. UI Direction

The operator app should stop spending time on hand-built web-style chrome.

The target is normal Windows desktop software in the style of traditional label tools:

- packaged workstation controls first
- menu / toolbar / docking / property-grid language
- explicit Windows desktop workflows
- no browser-like shell as the target design language

Current repository baseline on `main`:

- `AdonisUI` + `AdonisUI.ClassicTheme` for top-level WPF chrome and workstation framing
- `Dirkster.AvalonDock` for docked panes, documents, and operator workbench structure
- `PropertyTools.Wpf` for inspector/property-grid surfaces

Historical feature branches may still reference `Fluent.Ribbon` from the package-first migration path. Treat `docs/handoff/current-state.md` as the current shell-baseline source of truth.

Rules:

- prefer packaged Windows controls over custom `Grid + Border + ListBox` chrome when a compatible package already owns the interaction model
- keep custom layout work inside pane content, not shell chrome
- bias toward docked workbench language similar to traditional Windows label software
- avoid spending new shell-polish time on `apps/admin-web`; migrate workflow parity into WPF instead
- treat `docs/dotnet-convergence.md` as the target-state development rule when current shipped ownership and target ownership differ

## 5. Release and Packaging Path

- The private repository remains the source of truth.
- Formal Windows packaging for `v0.3.0` and later runs from the public packaging mirror `WSL043/JAN-label-public`.
- Snapshot-only exports are used for mirror sync so private git history is not published.
- Private-repo CI is useful when available, but mirror-side Windows runners are the authoritative packaging path when billing blocks private GitHub-hosted Actions.
