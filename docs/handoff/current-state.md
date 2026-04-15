# current-state

- Updated: 2026-04-15
- Branch: `codex/release-bridge-proof-hardening`
- Release base: `v0.1.3` (`0e22216`)
- Active PR: `#25`
- Branch relation to `origin/main` on 2026-04-15 after `v0.1.3`: `0 behind / 19 ahead`

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
  - proof inbox, audit search, audit export, and audit retention controls
  - audit backup bundle listing
  - structured template editor, local canvas preview, and Rust renderer preview
  - desktop template catalog sync, source display, and save-to-local-catalog action
  - desktop-style shell reset with navigation rail, workspace pane, inspector, and status bar
- `apps/desktop-shell`
  - `dispatch_print_job`
  - `print_bridge_status`
  - `search_audit_log`
  - `export_audit_ledger`
  - `trim_audit_ledger`
  - `list_audit_backup_bundles`
  - `approve_proof` / `reject_proof`
  - `template_catalog_command`
  - `save_template_to_local_catalog`
  - `preview_template_draft`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`

## Landed In This Batch

- `admin-web` no longer renders as a single landing-page-like vertical form stack.
- The app shell now uses a desktop layout:
  - left navigation rail
  - center workspace
  - right inspector
  - bottom status bar
- Work is split into four operator lanes:
  - draft
  - template
  - queue
  - audit
- Compose actions were tightened:
  - submit now uses the current live draft
  - preview shows the live payload instead of a stale staged payload
- Queue clear is now explicit through a dedicated command instead of inline ad-hoc state mutation.
- Queue and audit lanes now support:
  - client-side sort and filter controls
  - page-size and page navigation for larger result sets
  - explicit queue mutation lock while batch submit is active

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
- Audit export, retention dry-run/apply, and JSON backup bundles are working locally.
- Audit backup bundles can now be listed in `admin-web`; restore remains manual.
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
- `cargo clippy --workspace --all-targets -- -D warnings`
- `cargo test --workspace`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`

Operational note:

- Local Windows may intermittently return `os error 5` during `cargo test --workspace`. Re-run once and confirm `desktop-shell` tests pass before treating it as a regression.

## Release Status

- `v0.1.3` was tagged and published on `2026-04-15`.
- GitHub `Release` workflow run `24451489938` succeeded and published the Windows installer asset.
- Release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.1.3`
- Windows installer asset: `JAN-Label_0.1.3_windows_x64-setup.exe`
- Deferred non-PDF milestones:
  - `T-030` GitHub Actions secret setup
  - `T-031` physical printer matrix and scan confirmation

## Next Main Tasks

1. `T-047`: template authoring interaction hardening and staged/live review clarity
2. `T-028f-restore`: audit backup restore flow
3. `T-016`: release notes / packaging hardening
4. `T-043`: release checklist automation

## External Deferrals

- `T-030`: GitHub repository secret `OPENAI_API_KEY` (non-blocker for PDF-only release)
- `T-031`: physical printer matrix and measurement commit in `docs/printer-matrix/` (non-blocker for PDF-only release)
