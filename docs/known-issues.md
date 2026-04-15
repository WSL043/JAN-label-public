# known-issues

## K-001 branch protection is not configured
- status: open
- impact: direct pushes to `main` are still possible without mandatory review and CI gates
- response: configure GitHub branch protection / rulesets

## K-004 physical printer measurement is still missing
- status: open
- impact: current baseline is PDF proof only; 100% scale, scan, and mm validation on real printers is not committed yet
- response: commit physical printer measurements under `docs/printer-matrix/`
- release impact for PDF-only milestone: non-blocker; defer to `T-031` milestone

## K-005 local Windows can intermittently fail `cargo test --workspace` with `os error 5`
- status: open
- impact: local test runs can fail once during binary execution even when code is healthy
- response: re-run once and confirm `desktop-shell` tests also pass before escalating

## K-008 GitHub Actions `OPENAI_API_KEY` is not set
- status: open
- impact: cloud-side Codex fallback automation cannot fully run
- response: configure the repository secret
- release impact for PDF-only milestone: non-blocker; defer to `T-030` milestone

## K-009 browser preview mode is submit-disabled
- status: open
- impact: browser-only `admin-web` can preview, but cannot submit, read bridge status, or use audit actions
- response: run through `desktop-shell` for operational use

## K-010 bridge env fallback still requires operator review
- status: open
- impact: safe defaults prevent unsafe execution, but operator intent can still drift if warnings are ignored
- response: use `warningDetails[]` and the runbook checklist before production operation

## K-011 XLSX import chunk is still large
- status: watch
- impact: lazy-load is in place, but the XLSX bundle remains heavy
- response: consider worker/off-main-thread parsing if field use grows

## K-012 audit ledger is local-filesystem only
- status: open
- impact: multi-host and shared-storage operation is not implemented
- response: keep the current release scope local and document retention/backup handling

## K-013 Excel numeric JAN remains risky if source owners do not export text
- status: watch
- impact: UI blocks/warns on risky numeric cells, but upstream spreadsheets can still cause avoidable cleanup work
- response: require JAN columns to be stored/exported as text in operational guidance

## K-020 template editor write-back gap
- status: resolved
- resolution: template JSON can now be saved to the desktop local catalog, and saved local templates are used by proof/print dispatch

## K-021 unknown template routes can reach queue/submit
- status: resolved
- resolution: unknown live `template_version` now blocks queue/manual/batch submit in `admin-web`

## K-022 approved proof artifact checks were weak
- status: resolved
- resolution: `desktop-shell` now validates path, directory scope, non-empty PDF content, and `%PDF-` header before print

## K-023 audit backup restore is still manual
- status: resolved
- resolution: backup bundles can now be selected and restored from the `admin-web` audit lane through `restore_audit_backup_bundle`

## K-024 local template catalog is local-filesystem only
- status: open
- impact: packaged/local overlay works for a single operator machine, but there is no shared catalog, no restore UI, and no multi-writer coordination. A malformed local manifest can also block catalog/dispatch resolution until the local files are repaired.
- response: keep it scoped to local desktop operation for this release and harden governance under `T-041`

## K-025 PDF-only release boundary is documented
- status: watch
- impact: release tasks beyond PDF proof/print could be mistaken as blockers
- response: use a `PDF-only` lane for this milestone and gate release on printed proofs, lineage-gated approval, and local audit controls only

## K-026 proof dispatch and pending-proof registration are not transactional
- status: open
- impact: if the process crashes between persisted proof dispatch and pending proof registration, manual ledger reconciliation may be required
- response: keep this as an operational recovery case for the PDF-only release and harden it in a follow-up audit transaction task

## K-027 GitHub Release workflow still depends on Node 20-targeted `pnpm/action-setup@v4`
- status: watch
- impact: `v0.1.2` released successfully, but GitHub Actions emitted deprecation warnings because the action is being forced onto Node 24 compatibility mode
- response: upgrade or replace the action before the next workflow break turns the warning into a release blocker

## K-028 desktop shell reset is in place, but queue/audit tables are still basic
- status: resolved
- resolution: queue and audit lanes now support desktop-oriented sort, filter, and page-size navigation instead of remaining fixed basic tables

## K-029 template authoring is denser than desired even after the shell reset
- status: resolved
- resolution: the template lane now separates structure, fields, review, and catalog sub-flows with explicit live-draft, Rust preview, and saved-catalog authority states

## K-030 queue can be cleared while batch submit is still running
- status: resolved
- resolution: queue mutation actions are now locked while batch submit is active, including source reset, queue snapshot rebuild, and clear-batch actions

## K-031 staged review and live dispatch semantics are still ambiguous
- status: resolved
- resolution: compose preview and template authoring now call out the live payload, staged snapshot, and saved catalog dispatch boundary separately so submit authority is explicit

## K-032 local Windows host may be missing the MSVC linker toolchain
- status: open
- impact: `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml` and Tauri desktop builds fail immediately if `link.exe` is unavailable on the host
- response: prefer GitHub Actions Windows runners for release verification; only install Visual Studio Build Tools with the C++ workload when local Tauri build/test is explicitly required
