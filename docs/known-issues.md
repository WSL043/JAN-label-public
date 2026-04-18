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
- impact: Codex workflows can now route through GitHub-hosted or self-hosted runners and can be replayed through `repository_dispatch`, but the actual Codex review/comment/triage/autofix/maintenance steps still skip until the secret is configured
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
- status: resolved
- resolution: `apps/admin-web` now parses XLSX uploads inside a dedicated web worker, so the heavy `xlsx` decode path no longer blocks the main operator UI thread during compose import or legacy proof seed upload

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
- impact: packaged/local overlay works for a single operator machine, but there is still no shared catalog or multi-host coordination. Manual filesystem backup/restore remains an operational procedure, and the overlay directory must still be treated as single-writer state.
- response: use the Catalog lane maintenance diagnostics before manual repair, back up or restore the overlay directory as a unit, and keep the current scope local to a single desktop operator

## K-025 PDF-only release boundary is documented
- status: watch
- impact: release tasks beyond PDF proof/print could be mistaken as blockers
- response: use a `PDF-only` lane for this milestone and gate release on printed proofs, lineage-gated approval, and local audit controls only

## K-026 proof dispatch and pending-proof registration were not transactional
- status: resolved
- resolution: proof dispatch now commits through `proof-dispatch-transaction.json`, stale markers auto-recover on the next locked audit access, and unreadable/corrupt markers are quarantined with an explicit manual-reconcile error instead of blocking every later audit operation

## K-027 GitHub Release workflow still depends on Node 20-targeted `pnpm/action-setup@v4`
- status: resolved
- resolution: CI, Release, and Codex CI autofix workflows now bootstrap pnpm through `actions/setup-node` plus Corepack, `docs-guard` no longer depends on `dorny/paths-filter@v3`, and artifact uploads now use `actions/upload-artifact@v7`

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

## K-033 local Windows host may be missing the .NET SDK for the native shell
- status: open
- impact: `apps/windows-shell` cannot be built locally when `dotnet` is unavailable, so native-shell validation must move to GitHub Windows runners
- response: treat the `windows-shell-native` CI job as the authoritative validation path unless local `.NET 8` is explicitly installed for shell work

## K-041 `docs/domain-model.md` was garbled
- status: resolved
- resolution: `docs/domain-model.md` was rewritten for the `v1.0.0` single-stack route and is back in the normal routing set

## K-042 native Designer draft PDF preview rejects non-ASCII text
- status: open
- impact: the new `.NET` draft PDF preview path now skips PDF generation with a visible warning instead of silently corrupting non-ASCII text, because the current lightweight PDF builder does not yet embed Unicode-capable fonts; draft SVG can still render the same content, so the two draft artifacts are not yet text-parity complete for multilingual labels
- response: treat this as an `M3` render-engine gap, use the draft SVG for multilingual authoring checks for now, and do not treat the current draft PDF preview as multilingual-proof-safe until the `.NET` render path owns proper font/text output

## K-043 native proof review and audit lanes still depend on live sync for upstream authority
- status: watch
- impact: WPF proof create/review decisions, visible audit rows, bundle inventory, and audit export now all come from the native SQLite mirror, but that mirror still depends on a successful live-service refresh to ingest new upstream proof/dispatch state, and retention apply, restore, and dispatch are still outside the single-stack path
- response: keep this as a `T-052` proof/audit follow-up; native visible audit state plus native proof create/review are real now, but upstream authority still needs more `.NET` replacements before the lane becomes fully single-stack

## K-034 native-shell preview packaging is CI-driven
- status: watch
- impact: the native shell now has both CI preview installers and formal tagged-release installer assets, but preview publication still depends on GitHub Actions artifacts and manual prerelease publication rather than a dedicated preview release workflow
- response: use the `jan-label-native-shell-installer` artifact from `windows-shell-native` for preview validation; tagged releases now upload the native-shell installer asset directly

## K-036 local Windows host may be missing Inno Setup for native-shell packaging
- status: open
- impact: `pnpm release:readiness --version <version>` can classify native-shell installer generation as `blocked` on hosts that can build/publish WPF but do not have `ISCC.exe`
- response: use GitHub Actions Windows runners or install Inno Setup locally only when native-shell installer generation must run on the workstation

## K-037 native-shell live parity now depends on a desktop-shell binary being available locally
- status: watch
- impact: `apps/windows-shell` now launches `apps/desktop-shell --native-shell-companion` for live bridge/catalog/governance/preview/proof/audit/bundle/shared-batch state; if no recent `desktop-shell.exe` is available, WPF falls back to seeded shell state and reports companion refresh failure
- response: build `apps/desktop-shell/src-tauri` locally or set `JAN_LABEL_DESKTOP_SHELL_BINARY` explicitly when validating native-shell live parity; publish/install outputs now stage `desktop-shell.exe` when the companion has been built, but GitHub runner validation still needs to keep that packaging path green

## K-038 native-shell designer actions still overstate editing parity
- status: resolved
- resolution: placeholder `apps/windows-shell` designer commands are now explicitly disabled until real editor handlers exist, and unsupported shell actions report that state instead of silently reading as parity-complete

## K-039 shared batch snapshot is live in WPF, but mutation still lives outside the native shell
- status: watch
- impact: `apps/admin-web` now publishes a desktop-shell-owned local batch snapshot that `apps/windows-shell` can read, but import, retry, and submit still remain outside WPF and the snapshot is still single-machine local state rather than multi-host coordination
- response: treat Batch Jobs as a live review lane for queued rows and blockers, but keep actual batch mutation in `apps/admin-web` until a follow-up backend expansion intentionally moves that authority

## K-040 private-repo GitHub Release workflow can be billing-blocked before jobs start
- status: open
- impact: tagged release runs on `WSL043/JAN-label` may fail before any step executes if the private repository account hits a billing or spending-limit block, even when release code and workflow wiring are healthy
- response: use `WSL043/JAN-label-public` as the authoritative packaging mirror for tagged release runs until private-repo billing is restored; treat the public mirror's Release workflow and readiness artifact as the release truth in that case

## K-035 native-shell startup window was missing
- status: resolved
- resolution: `apps/windows-shell/App.xaml` now declares `StartupUri="MainWindow.xaml"`, so the preview installer launches the operator shell window instead of leaving a background process with no visible UI
