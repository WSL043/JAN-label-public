# current-state

- Updated: 2026-04-16
- Branch: `main`
- Release base: `v0.2.0` (`0b6827e`)
- Active PR: `none`
- Branch relation to `origin/main` on 2026-04-16: `main` includes the merged `T-041` governance batch from PR `#33`, including local template catalog diagnostics, repair guidance, single-writer operating rules, and the initial native WPF workstation shell baseline under `apps/windows-shell`

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
  - `preview_template_draft`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`
- `apps/windows-shell`
  - WPF operator shell prototype with native menu / ribbon / docking layout
  - module navigation for `Job Setup`, `Designer`, `Batch Manager`, and `History`
  - Windows-only shell language baseline for future operator UX work
  - GitHub Windows CI now emits both self-contained preview binaries and an installer artifact for native-shell evaluation
  - startup path now opens `MainWindow` directly instead of idling as a background process with no shell window
- release automation
- `pnpm release:notes --version <version>`
- `pnpm release:readiness --version <version>`
- `Release` workflow uploads release notes and readiness artifacts
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
- The workstation chrome is no longer targeting an AI desktop look:
  - top-level shell now uses a titlebar plus toolbar layout
  - lane navigation, tables, and inspector panels are styled closer to traditional Windows label software
  - buttons, tabs, and metric cards now use denser desktop-oriented controls instead of rounded dark cards
- A new migration front is now in place for the shell itself:
  - `apps/windows-shell` establishes the Windows-native workstation frame in WPF
  - `apps/admin-web` remains the operational path until backend parity lands in the native shell
  - GitHub Windows runners are the authoritative validation path for the native shell on hosts without `.NET`
  - preview packaging for the native shell now includes an installer path instead of requiring operators to launch a loose published `.exe`

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

Passed on this batch:

- `git diff --check`
- `pnpm fixture:validate`
- `pnpm format:check`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- Local Rust/Tauri validation for this branch still depends on a Windows MSVC linker and was not completed on this host
- Local WPF validation for this branch also depends on a local `.NET` SDK and was not completed on this host

Operational note:

- Local Windows may intermittently return `os error 5` during `cargo test --workspace`. Re-run once and confirm `desktop-shell` tests pass before treating it as a regression.
- This host currently cannot complete Rust/Tauri verification because `link.exe` is not available:
  - `cargo clippy --workspace --all-targets -- -D warnings`
  - `cargo test --workspace`
  - `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`
  - `pnpm release:readiness --version v0.2.0`
- This host currently cannot complete native-shell verification because `dotnet` is not available:
  - `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- GitHub Actions has already passed the Windows desktop and native-shell validation path for PR `#33`, so local MSVC and `.NET` toolchain absence is no longer a blocker as long as the remote Windows runners remain green.
- Native-shell validation should move to the GitHub Actions `windows-shell-native` job until local `.NET` is available or intentionally installed.
- Native-shell preview packaging should also be taken from the GitHub Actions `jan-label-native-shell-installer` artifact until a dedicated release workflow supersedes the CI path.
- `docs/release/v0.2.0.md` and `artifacts/release-readiness.{json,md}` are generated locally; the local readiness report remains `fail` only because this workstation does not have the Windows desktop linker toolchain installed.

## Release Status

- `v0.1.3` was tagged and published on `2026-04-15`.
- `v0.2.0` was tagged and published on `2026-04-15`.
- GitHub `Release` workflow run `24474516998` succeeded and published the Windows installer asset.
- Release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.2.0`
- Windows installer asset: `JAN-Label_0.2.0_windows_x64-setup.exe`
- The operator workstation redesign, audit restore flow, and release automation artifacts are now in the published baseline.
- PR `#33` was merged on `2026-04-16`, adding local template catalog governance diagnostics and the first native shell baseline on top of the `v0.2.0` release state.
- Deferred non-PDF milestones:
  - `T-030` GitHub Actions secret setup
  - `T-031` physical printer matrix and scan confirmation

## Next Main Tasks

1. `T-049`: Windows-native workstation shell migration
2. `T-042`: template library operator UX
3. `T-044`: audit transaction hardening
4. `T-012`: self-hosted runner / webhook operations

## External Deferrals

- `T-030`: GitHub repository secret `OPENAI_API_KEY` (non-blocker for PDF-only release)
- `T-031`: physical printer matrix and measurement commit in `docs/printer-matrix/` (non-blocker for PDF-only release)
