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
- status: open
- impact: backup bundles are produced and can now be listed, but restore remains manual
- response: implement `T-028f-restore`

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
- status: open
- impact: the app now has a desktop application shell, but queue/audit lanes still rely on simple HTML tables without sort/filter/virtualized navigation for larger operator workloads
- response: harden the queue and audit grids under `T-046`

## K-029 template authoring is denser than desired even after the shell reset
- status: open
- impact: the shell is desktop-oriented now, but the template lane still mixes structured properties, JSON editing, and preview into a dense workbench that needs clearer sub-modes
- response: split the template lane into clearer authoring sub-flows under `T-047`
