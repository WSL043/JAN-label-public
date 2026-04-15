# known-issues

## K-001 branch protection is not configured
- status: open
- impact: direct pushes to `main` are still possible without mandatory review and CI gates
- response: configure GitHub branch protection / rulesets

## K-004 physical printer measurement is still missing
- status: open
- impact: current baseline is PDF proof only; 100% scale, scan, and mm validation on real printers is not committed yet
- response: commit physical printer measurements under `docs/printer-matrix/`

## K-005 local Windows can intermittently fail `cargo test --workspace` with `os error 5`
- status: open
- impact: local test runs can fail once during binary execution even when code is healthy
- response: re-run once and confirm `desktop-shell` tests also pass before escalating

## K-008 GitHub Actions `OPENAI_API_KEY` is not set
- status: open
- impact: cloud-side Codex fallback automation cannot fully run
- response: configure the repository secret

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

## K-023 audit backup list / restore UI is missing
- status: open
- impact: backup bundles are produced, but list/restore is still manual
- response: implement `T-028f`

## K-024 local template catalog is local-filesystem only
- status: open
- impact: packaged/local overlay works for a single operator machine, but there is no shared catalog, no restore UI, and no multi-writer coordination. A malformed local manifest can also block catalog/dispatch resolution until the local files are repaired.
- response: keep it scoped to local desktop operation for this release and harden governance under `T-041`
