# current-state

- Updated: 2026-04-15
- Branch: `codex/release-bridge-proof-hardening`
- Release base: `v0.1.2` (`881e92b`)
- Active PR: `#25`
- Branch relation to `origin/main` on 2026-04-15 after `v0.1.2`: `0 behind / 17 ahead`

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

- Local template catalog write-back is live through `desktop-shell`.
- Catalog responses now report `packaged` vs `local` source to `admin-web`.
- The save-to-catalog Tauri contract was aligned end to end:
  - command name
  - response shape
  - UI success/error messaging
- Proof/print dispatch now uses the same local overlay manifest that validation uses.
- Added regression coverage for:
  - overlay catalog merge behavior in `render`
  - local overlay render path in `print-agent`
  - catalog source reporting and local save flow in `desktop-shell`

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

- `v0.1.2` was tagged and published on `2026-04-15`.
- GitHub `Release` workflow run `24449920688` succeeded and published the Windows installer asset.
- Release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.1.2`
- Deferred non-PDF milestones:
  - `T-030` GitHub Actions secret setup
  - `T-031` physical printer matrix and scan confirmation

## Next Main Tasks

1. `T-028f-restore`: audit backup restore flow
2. `T-016`: release notes / packaging hardening
3. `T-043`: release checklist automation
4. `T-012`: self-hosted runner / webhook operations

## External Deferrals

- `T-030`: GitHub repository secret `OPENAI_API_KEY` (non-blocker for PDF-only release)
- `T-031`: physical printer matrix and measurement commit in `docs/printer-matrix/` (non-blocker for PDF-only release)
