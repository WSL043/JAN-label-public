# Windows Shell Core

`apps/windows-shell-core` is the first extracted `.NET` support library for the Windows-only rebuild.

Current scope:

- native packaged template-manifest read
- native local overlay-manifest read
- packaged/local catalog merge logic
- native catalog issue modeling for WPF consumption
- LocalAppData-rooted application directories
- SQLite local state schema and bootstrap
- legacy runtime import scaffold for overlay, audit, artifacts, and batch snapshot
- first-pass service contracts for the `.NET` single-stack runtime
- `WindowsShellPlatform.Initialize()` bootstrap for shell startup
- native local template catalog save service for the first Designer replacement slice
- native template document load service for the first Designer open-from-catalog slice
- native draft SVG preview builder for the first Designer preview replacement slice
- shared parsing path for template-spec JSON so native template load and draft preview use the same document interpretation
- native proof-create service that now renders proof PDFs locally and writes proof plus dispatch mirror rows into SQLite

Rules:

- keep this library free of WPF shell chrome and view-model concerns
- move pure workflow/domain logic here before adding more shell UI
- use this library to replace mixed-stack DTO dependence where a native model is enough

Current batch:

- `T-052` phase 1 core extraction
- M0/M1 foundation for `v1.0.0`
- first extracted slice is template catalog filesystem and merge logic
- current extracted platform slice adds local runtime paths, SQLite bootstrap, legacy import scaffolding, native template save, native template load, native draft preview generation, native proof create/review persistence, and local audit mirror/export services
