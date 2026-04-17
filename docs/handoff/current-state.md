# current-state

- Updated: 2026-04-17
- Branch: `main`
- Release base: `v0.2.0` (`0b6827e`)
- Next formal release target: `v0.3.0`
- Active PR: `none`
- Branch relation to `origin/main` on 2026-04-17: `main` includes the merged `T-041` governance batch from PR `#33`, including local template catalog diagnostics, repair guidance, single-writer operating rules, and the initial native WPF workstation shell baseline under `apps/windows-shell`

## Shipping Now

- Rust core
  - `domain`: JAN normalization and validation
  - `barcode`: Zint CLI adapter
  - `render`: deterministic SVG/PDF output with packaged and local template catalog overlay support
  - `print-agent`: dispatch, proof gate, lineage, and overlay-aware render routing
  - `printer-adapters`: PDF output and Windows spool staging
  - `audit-log`: lineage, proof status, audit export/search/retention data model
- `apps/admin-web`
  - manual draft, batch queue, retry, and bridge-status-aware submit blocking
  - CSV/XLSX import with alias mapping and numeric JAN hardening
  - proof inbox, audit search, audit export, audit retention, and audit backup restore controls
  - three-pane workstation shell with classic Windows/BarTender-style chrome, dense toolbar rows, and lane-aware inspector focus
  - structured template editor, local canvas preview, Rust renderer preview, and catalog authority split
  - desktop template catalog sync, source display, and save-to-local-catalog action
  - catalog maintenance diagnostics with backup/restore guidance, manifest repair guidance, and single-writer rules
  - explicit live payload vs staged snapshot vs dispatch boundary review in compose
- `apps/desktop-shell`
  - `dispatch_print_job`
  - `print_bridge_status`
  - `search_audit_log`
  - `export_audit_ledger`
  - `trim_audit_ledger`
  - `list_audit_backup_bundles`
  - `restore_audit_backup_bundle`
  - `approve_proof` / `reject_proof`
  - `template_catalog_command`
  - `template_catalog_governance_command`
  - `save_template_to_local_catalog`
  - `load_batch_queue_snapshot` / `save_batch_queue_snapshot` / `clear_batch_queue_snapshot`
  - `preview_template_draft`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`
  - `--native-shell-companion` stdio JSON mode for WPF-native shell read + safe-op parity
  - proof dispatch audit persistence now uses a recovery marker with stale-marker replay and corrupt-marker quarantine
- `apps/windows-shell`
  - WPF operator workstation baseline with native menu / ribbon / docking layout
  - `Fluent.Ribbon` shell chrome for real ribbon, backstage, and quick-access behavior instead of hand-built ribbon lookalikes
  - `Dirkster.AvalonDock` for package-backed tool windows, docked inspectors, and designer document tabs
  - `PropertyTools.Wpf` for the right-side designer property inspector baseline
  - designer canvas selection now drives the property inspector instead of leaving it as static metadata
  - Home and Designer now share a template-library board that makes winning default, draft-only entries, dispatch safety, authority owner, and rollback path visible from one selection surface
  - ribbon, quick-access, and header actions now write visible shell feedback instead of remaining inert buttons
  - shell actions now also route operators toward the relevant lane instead of acknowledging clicks without any workspace transition
  - shell actions now also focus the relevant template, canvas object, proof, job, batch, bundle, or audit row in place instead of landing on a generic lane shell
  - Print Console, Batch Jobs, and History now use selection-driven detail panes, so choosing a proof, job, import session, bundle, or ledger row updates blocker, route, and next-action context in place
  - shared context badges and status-strip cells now follow the currently selected template, object, proof, job, batch, bundle, or audit row instead of staying module-static
  - the shell title and lead text now also follow the active in-lane focus so the operator sees the current work item in top-level chrome
  - companion-backed live modules for `Home`, `Designer`, `Print Console`, `Batch Jobs`, and `History` now read real bridge/catalog/governance/preview/audit/bundle/batch-snapshot data from `apps/desktop-shell`
  - WPF safe operations now call `approve_proof`, `reject_proof`, and `export_audit_ledger` through the companion path instead of staying mock-only
  - unsupported `v0.3.0` actions now render as disabled/explanatory controls instead of pretending direct print, restore, retention apply, or template write-back moved into WPF
  - `Print Console` is now explicitly audit-derived rather than implying a native-shell-owned live dispatch queue
  - `admin-web` now persists a desktop-shell-owned shared batch snapshot, and `Batch Jobs` now reads that live queued-row state in WPF while keeping import / retry / submit disabled
  - quick-access buttons now mirror the current lane's real action labels and enabled/disabled state instead of staying hard-wired
  - refresh-style actions now report lane-specific results and keep companion refresh failures visible instead of overwriting them with generic success text
  - shell chrome now keeps runtime mode visible as `seeded shell`, `live companion`, `seeded fallback`, or `stale live snapshot` instead of hiding companion state in the last action message only
  - native-shell build and publish outputs now stage `desktop-shell.exe` beside the WPF shell when the companion has been built, so packaged live mode no longer depends on repo-relative fallback paths
  - focused `windows-shell` tests now cover companion audit-window sizing, batch-lane-only degradation, and lane mapping without relying on UI automation
  - startup seeds the shell immediately, then attempts a companion refresh on `Loaded`; if the companion is unavailable, the shell remains in seeded explanatory mode and reports the refresh failure instead of implying live parity
  - non-live runtime states now also prefix the workspace lead text so seeded/fallback mode is visible even without reading the status strip
  - `Home`, `Print Console`, `Batch Jobs`, and `History` now use resizable WPF work areas with `TabControl`, `DataGrid`, and `GridSplitter` instead of fixed stacked list panes
  - pane secondary labels in `Home`, `Print Console`, `Batch Jobs`, and `History` now come from workspace model state instead of hardcoded placeholder text, so seeded/live/transitional context stays truthful inside the work area
  - `Designer` and the shared template-library board now also derive operator-facing counts and entries summaries from workspace model state instead of hardcoded placeholder totals
  - companion requests now honor cancellation during stdio I/O, and shell shutdown no longer waits indefinitely on a stuck companion request before closing
  - BarTender-style document tabs, design canvas, toolbox, object browser, property grid, and record/message panes
  - module navigation for `Home`, `Designer`, `Print Console`, `Batch Jobs`, and `History`
  - module-aware native workspaces for migration/readiness, designer, print console, batch queue, and history/audit review instead of a single static shell mock
  - shared context strip and status strip that keep lane purpose, current authority, route, and blocker signals visible at shell level
  - Windows-only shell language baseline for future operator UX work
  - GitHub Windows CI now emits both self-contained preview binaries and an installer artifact for native-shell evaluation
  - startup path now opens `MainWindow` directly instead of idling as a background process with no shell window
- release automation
- `pnpm release:notes --version <version>`
- `pnpm release:readiness --version <version>`
- `Release` workflow uploads release notes and readiness artifacts
- `release:readiness` now validates native-shell build, self-contained publish, and installer generation separately
- tagged formal releases now build and upload the native-shell installer asset in addition to the desktop-shell installer asset
- GitHub release publishing
  - `v0.2.0` tag published through the Release workflow
  - Windows installer asset uploaded successfully

## Landed In This Batch

- `admin-web` now ships a dark-neutral operator workstation instead of the earlier light enterprise-console shell.
- The workstation keeps the fixed three-pane model:
  - left workspace rail
  - center workbench
  - right lane-aware inspector
  - bottom utility/status bar
- Compose is now review-first:
  - live payload remains authoritative
  - staged snapshot stays review/export only
  - dispatch authority remains proof/catalog gated
- Template authoring now behaves like a workbench:
  - `Structure`
  - `Fields`
  - `Review`
  - `Catalog`
  - selected field properties surface in the inspector
- Queue and audit lanes now behave like dense operational grids:
  - sticky filters, sort, and pagination
  - queue session lock visibility
  - focused row / focused ledger entry shown in the inspector
- Audit backup bundles are no longer list-only:
  - the desktop bridge exposes `restore_audit_backup_bundle`
  - `admin-web` supports selecting a bundle, explicit confirmation, and restore result feedback
  - conflict or invalid bundle restore fails before any merge is applied
- Release packaging no longer depends on hand-written notes:
  - `release:notes` drafts `docs/release/<version>.md`
  - `release:readiness` emits `artifacts/release-readiness.{json,md}`
  - `Release` workflow now uploads both artifacts during tagged release execution
- Workflow hygiene now avoids Node 20-targeted helper actions in the main release path:
  - CI and Release install pnpm through `actions/setup-node` plus Corepack
  - `docs-guard` now evaluates changed files through native `git diff`
  - Codex CI autofix uses the same Corepack-based pnpm bootstrap as the main CI jobs
- Local template catalog governance is now operator-visible instead of implicit:
  - `desktop-shell` exposes `template_catalog_governance_command`
  - Catalog lane surfaces manifest status, overlay file health, effective default resolution, and orphaned local JSON warnings
  - operators now get explicit backup/restore guidance, manifest repair guidance, and single-writer operating rules before manual catalog repair
- Proof audit persistence is now recovery-backed instead of split across two best-effort writes:
  - `dispatch_print_job` commits proof dispatch and pending-proof state through `proof-dispatch-transaction.json`
  - stale markers auto-replay on the next locked audit access
  - corrupt markers are quarantined with an explicit reconcile error instead of wedging later audit operations
  - `desktop-shell` now has command-level proof dispatch coverage with a fake Zint path
- The workstation chrome is no longer targeting an AI desktop look:
  - top-level shell now uses a titlebar plus toolbar layout
  - lane navigation, tables, and inspector panels are styled closer to traditional Windows label software
  - buttons, tabs, and metric cards now use denser desktop-oriented controls instead of rounded dark cards
- A new migration front is now in place for the shell itself:
  - `apps/windows-shell` establishes the Windows-native workstation frame in WPF
  - shell chrome is now based on `Fluent.Ribbon`, so the native shell is using a real Windows ribbon/backstage package instead of a custom imitation
  - the designer surface now uses `Dirkster.AvalonDock` and `PropertyTools.Wpf`, so docked tools and the right-side inspector are moving onto package-backed Windows controls instead of a fixed hand-built pane grid
  - the shell no longer stops at generic chrome; it now carries practical operator workspaces for `Home`, `Designer`, `Print Console`, `Batch Jobs`, and `History`
  - each module now has its own lane-aware surface so operators can evaluate information density and flow before backend command wiring lands
  - shared shell chrome now exposes lane context, authority, route, and blocker-oriented status so the shell is judged against operator decision speed, not only visual density
  - template-library reasoning is no longer a flat mock list; `Home` and `Designer` now share a library board with selected-template detail, dispatch-safe state, default resolution, and rollback guidance
  - shell actions now route operators to the appropriate lane for review while still keeping backend authority in `apps/desktop-shell`
  - shell actions now also land on the relevant in-lane object, so a proof, job, batch, bundle, template, or canvas object is already focused when the operator arrives
  - the operational lanes no longer keep a frozen right pane; selection inside proof, queue, bundle, and audit lists now changes the decision context the operator sees
  - shared shell chrome no longer stops at module defaults; top-level badges and bottom status cells now reflect the current in-lane selection context
- window title and workspace lead text now also describe the focused proof, job, batch, bundle, template, or object when one is selected
- Native-shell backend parity is no longer only a planning note:
  - `desktop-shell` now has a `--native-shell-companion` stdio JSON mode with command-level coverage for bridge status, audit search/export, proof approve/reject, template catalog/governance, backup listing, preview draft, and shared batch snapshot loading
  - `desktop-shell` now also owns a local shared batch snapshot contract that `admin-web` can write and `windows-shell` can read through the same authority path
  - `windows-shell` now launches the companion as a hidden child process, reads live workstation state from it, and keeps unsupported backend actions visibly disabled
  - companion request validation now preserves `requestId` for contract errors where possible and exits cleanly instead of panicking on I/O loop failure
  - native-shell quick access now routes to real live-safe actions, companion responses are checked against the sent `requestId`, preview refresh reuses cached results when the template source has not changed, preview status now distinguishes `live` / `cached` / `fallback` / `degraded`, proof-review lanes no longer silently truncate pending review items to a tiny audit window, and batch snapshot load failures now degrade only that lane instead of dropping the whole shell back to seeded/stale state
  - Designer placeholder commands are now visibly disabled in both live and seeded shell states until real editor handlers exist, instead of masquerading as parity-complete actions
- `apps/admin-web` remains the operational path until backend parity lands in the native shell
  - GitHub Windows runners are the authoritative validation path for the native shell on hosts without `.NET`
  - preview packaging for the native shell now includes an installer path instead of requiring operators to launch a loose published `.exe`
  - tagged release automation is now wired to publish the native-shell installer asset, so formal releases can carry both the transitional desktop shell and the native-shell preview direction

## Release Boundary

- Proof-to-print gate is strict on:
  - `templateVersion`
  - `sku`
  - `brand`
  - normalized `jan`
  - `qty`
  - `lineage`
- Approved proof artifacts must be readable non-empty PDFs with a valid `%PDF-` header.
- Unknown `template_version` blocks queue/manual/batch submit in `admin-web`.
- Saved local templates are now authoritative for proof/print dispatch when their `template_version` is selected.
- Audit export, retention dry-run/apply, JSON backup bundles, and bundle restore are working locally.
- PDF-only release boundary for this milestone:
  - Proof/queue flow uses local artifacts and PDF outputs only.
  - Windows spooler / physical printer scan validation is out of scope for now.
  - `T-030` (OPENAI_API_KEY secret) and `T-031` (physical printer matrix + scan check) are explicitly marked **non-blockers** for the PDF-only gate.

## Validation

Passed in the latest release-hardening batch:

- `git diff --check`
- `pnpm fixture:validate`
- `pnpm format:check`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- `cargo clippy --workspace --all-targets -- -D warnings`
- `cargo test --workspace`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`
- `pnpm --filter @label/desktop-shell build --ci --no-sign`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- `dotnet publish apps/windows-shell/JanLabel.WindowsShell.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true`

Passed in the latest native-shell UX batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell selection-state batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell action-focus batch:

- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell shared-context batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell focused-headline batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell backend-parity batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`
- local smoke test: launch `desktop-shell --native-shell-companion` and round-trip `print_bridge_status`

Passed in the latest native-shell quick-access-state batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell refresh-reporting batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell runtime-state batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell work-area batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell pane-metadata batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest native-shell companion-safety batch:

- `git diff --check`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`

Passed in the latest shared-batch-snapshot batch:

- `pnpm --filter @label/admin-web build`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`

Passed in the latest `v0.3.0` gate-check batch:

- `git diff --check`
- `pnpm fixture:validate`
- `pnpm format:check`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- `dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release`
- `cargo fmt --all --check`
- `cargo clippy --workspace --all-targets -- -D warnings`
- `cargo test --workspace`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`
- `pnpm release:readiness --version v0.3.0` (expected `blocked` on this host because local native-shell installer generation still depends on `ISCC.exe`)

Passed in the latest desktop-shell release-metadata batch:

- `pnpm --filter @label/desktop-shell build --ci --no-sign`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`

Operational note:

- Local Windows may intermittently return `os error 5` during `cargo test --workspace`. Re-run once and confirm `desktop-shell` tests pass before treating it as a regression.
- This host can now complete local Rust/Tauri verification and desktop-shell NSIS bundling.
- The remaining local packaging blocker is native-shell installer generation because `ISCC.exe` is not installed:
  - `pnpm release:readiness --version v0.3.0` reports the native-shell installer step as `blocked`
  - GitHub Windows runners remain the authoritative packaging path for native-shell installers until Inno Setup is intentionally installed on the local workstation
- `docs/release/v0.3.0.md` and `artifacts/release-readiness.{json,md}` are generated locally; the current readiness report should now settle at `blocked` on this host because `T-049` is complete but the local workstation still lacks Inno Setup for native-shell installer generation.
- the latest readiness artifact now also confirms that native-shell publish output includes `desktop-shell.exe`; companion packaging is no longer an implicit side effect
- the latest readiness artifact now also verifies that `apps/desktop-shell/package.json`, `apps/desktop-shell/src-tauri/tauri.conf.json`, and `apps/desktop-shell/src-tauri/Cargo.toml` all match the requested release version before tag

## Release Status

- `v0.1.3` was tagged and published on `2026-04-15`.
- `v0.2.0` was tagged and published on `2026-04-15`.
- The next formal release target is `v0.3.0`.
- `apps/desktop-shell/package.json`, `apps/desktop-shell/src-tauri/tauri.conf.json`, and `apps/desktop-shell/src-tauri/Cargo.toml` are now aligned to `0.3.0` ahead of the release tag.
- GitHub `Release` workflow run `24474516998` succeeded and published the Windows installer asset.
- Release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.2.0`
- Windows installer asset: `JAN-Label_0.2.0_windows_x64-setup.exe`
- The operator workstation redesign, audit restore flow, and release automation artifacts are now in the published baseline.
- PR `#33` was merged on `2026-04-16`, adding local template catalog governance diagnostics and the first native shell baseline on top of the `v0.2.0` release state.
- Deferred non-PDF milestones:
  - `T-030` GitHub Actions secret setup
  - `T-031` physical printer matrix and scan confirmation

## v0.3.0 Gate

- `v0.3.0` is the next formal release target for the Windows-native workstation direction.
- Release intent:
  - native shell should look and read like a real Windows operator application
  - template library reasoning should be clearer than the transitional web path
  - pre-release bug hunt must include repeated sub-agent review passes before formal announcement
- `T-049` is now complete in the working tree and validation/docs baseline.
- Minimum additional gate beyond the current baseline:
  - audit recovery expectations stay aligned between code, docs, and release readiness artifacts
  - native-shell build / publish / installer checks stay green on GitHub Windows release runners, including staged `desktop-shell.exe` companion availability in publish output, explicit CI assertions for that companion binary, and focused `windows-shell` companion/lane tests
  - sub-agent review pass logged before tag / GitHub Release publication

## Next Main Tasks

1. `T-045d`: cut `v0.3.0` Windows-native workstation release
2. `T-012`: self-hosted runner / webhook operations

## External Deferrals

- `T-030`: GitHub repository secret `OPENAI_API_KEY` (non-blocker for PDF-only release)
- `T-031`: physical printer matrix and measurement commit in `docs/printer-matrix/` (non-blocker for PDF-only release)
