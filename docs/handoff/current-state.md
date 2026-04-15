# current-state

- Updated: 2026-04-16
- Branch: `codex/v020-workstation-redesign`
- Release base: `v0.1.3` (`0e22216`)
- Active PR: `#31` `[codex] implement v0.2.0 workstation release batch`
- Branch relation to `origin/main` on 2026-04-16: published integration branch for the `v0.2.0` workstation release batch; draft PR is open against `main`

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
  - dark-neutral three-pane workstation shell with lane-aware inspector focus
  - structured template editor, local canvas preview, Rust renderer preview, and catalog authority split
  - desktop template catalog sync, source display, and save-to-local-catalog action
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
  - `save_template_to_local_catalog`
  - `preview_template_draft`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`
- release automation
  - `pnpm release:notes --version <version>`
  - `pnpm release:readiness --version <version>`
  - `Release` workflow uploads release notes and readiness artifacts

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

- `pnpm fixture:validate`
- `pnpm format:check`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- `cargo fmt --all --check`
- `pnpm release:notes --version v0.2.0`
- GitHub Actions `CI` workflow on PR `#31`
  - `fixture-validation`
  - `web-format-lint`
  - `web-typecheck`
  - `rust-format`
  - `rust-lint`
  - `rust-test`
  - `golden-tests`
  - `desktop-shell-windows`

Operational note:

- Local Windows may intermittently return `os error 5` during `cargo test --workspace`. Re-run once and confirm `desktop-shell` tests pass before treating it as a regression.
- This host currently cannot complete Rust/Tauri verification because `link.exe` is not available:
  - `cargo clippy --workspace --all-targets -- -D warnings`
  - `cargo test --workspace`
  - `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`
  - `pnpm release:readiness --version v0.2.0`
- GitHub Actions has already passed the Windows desktop build/test path for PR `#31`, so local MSVC toolchain absence is no longer a release blocker as long as the remote Windows runner remains green.
- `docs/release/v0.2.0.md` and `artifacts/release-readiness.{json,md}` are generated locally; the local readiness report remains `fail` only because this workstation does not have the Windows desktop linker toolchain installed.

## Release Status

- `v0.1.3` was tagged and published on `2026-04-15`.
- GitHub `Release` workflow run `24451489938` succeeded and published the Windows installer asset.
- Release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.1.3`
- Windows installer asset: `JAN-Label_0.1.3_windows_x64-setup.exe`
- Next target is `v0.2.0` with operator workstation redesign, audit restore, and release automation artifacts in scope.
- Deferred non-PDF milestones:
  - `T-030` GitHub Actions secret setup
  - `T-031` physical printer matrix and scan confirmation

## Next Main Tasks

1. `T-045c`: cut and verify the `v0.2.0` operator workstation release using the GitHub Windows runner as the primary verification host
2. `T-041`: local template catalog governance hardening
3. `T-044`: audit transaction hardening

## External Deferrals

- `T-030`: GitHub repository secret `OPENAI_API_KEY` (non-blocker for PDF-only release)
- `T-031`: physical printer matrix and measurement commit in `docs/printer-matrix/` (non-blocker for PDF-only release)
