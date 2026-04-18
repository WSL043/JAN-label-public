# current-state

## Read This First

- This file is a routing snapshot, not a full release changelog.
- `origin/main` is the repository source of truth for the latest shipped state and the next planned work.
- Branch-local copies of handoff or todo docs can lag behind `main` or describe historical in-progress work.

If you are not on `main`, confirm the latest repository state first:

```powershell
git fetch --all --prune
git show origin/main:docs/handoff/current-state.md
git show origin/main:docs/todo/active.md
```

## Repository Baseline

- Updated: `2026-04-19`
- Primary branch: `main`
- Latest formal release: `v0.3.0` (`1c0f166`)
- Next formal release target: `v1.0.0`
- Active PR: `none`
- Current top pending task: `T-052`

## Shipping Now

- Rust core remains authoritative for JAN normalization and validation, Zint barcode generation, SVG/PDF render, proof gate enforcement, dispatch routing, and audit persistence.
- `apps/admin-web` remains the transitional workflow host for compose, import, retry, proof review, audit work, and template authoring. It is not the long-term shell direction.
- `apps/desktop-shell` remains the authoritative bridge, proof/print gate, audit restore authority, packaged/local template catalog resolver, and companion process for WPF live mode.
- `apps/windows-shell` is the target Windows operator shell and intended long-term visible operator app. The current shipped baseline uses `AdonisUI` + `AdonisUI.ClassicTheme`, `Dirkster.AvalonDock`, and `PropertyTools.Wpf`, with native template-catalog reads plus companion-backed lane state and safe-op parity. Unsupported direct mutations remain explicitly disabled instead of implying full backend parity.

## Recently Closed

- `T-049` is complete: Windows-native workstation shell migration landed on `main`.
- `T-045d` is complete: `v0.3.0` was tagged and published on `2026-04-17` through `WSL043/JAN-label-public` after the private-repo release attempt was billing-blocked before job start.
- `T-050` is complete: the single-installer WPF shell reset is in place as the historical preview baseline that now feeds the `v1.0.0` route.
- `T-012` is complete: Codex GitHub automation now supports configurable runner labels, `repository_dispatch` replay, and a documented webhook relay path.
- `T-051` is complete: `admin-web` spreadsheet parsing now runs in a dedicated worker so CSV/XLSX import and legacy proof seed uploads avoid blocking the main UI thread on `xlsx` decode.

## Remaining Work

- `T-052` in progress: `.NET-only` convergence and legacy-stack retirement.
- `T-030` deferred: GitHub Actions `OPENAI_API_KEY`.
- `T-031` deferred: physical printer matrix and measurement.

## Current Operational Truth

- Formal tagged packaging may need to run through `WSL043/JAN-label-public` while private-repo GitHub Actions remains billing-blocked before job start.
- The current shipped baseline is still hybrid; that is a temporary fact, not the target architecture.
- `v0.3.1` is no longer the next formal release target; treat it as an internal historical preview line while `v1.0.0` becomes the only release-track target.
- The locked product direction is Windows-only `.NET / WPF` with package-backed controls; browser-style shell work is not the target.
- `T-052` now follows `docs/dotnet-convergence.md`: rebuild workflows natively in `.NET`, and delete superseded or structurally-wrong legacy slices instead of porting the old implementation line by line.
- `docs/windows-rebuild-plan.md` is the execution order for `T-052`; use it to decide batch order and deletion gates.
- First `T-052` code slice is landed in `apps/windows-shell`: Home and Designer template-library boards now read the packaged manifest and local overlay directly from WPF instead of depending on companion snapshot data for that catalog view.
- `apps/windows-shell-core` now exists as the first extracted `.NET` support library and owns the native template catalog filesystem / merge model for the rebuild.
- The current `T-052` batch started as `M0 + M1` foundation work and now includes early `M2` slices: `apps/windows-shell-core` now owns LocalAppData path resolution, SQLite bootstrap, legacy runtime import scaffolding, first-pass `.NET` service contracts, native local-catalog save, native template document load, and native draft preview SVG generation for opened template documents.
- `WindowsShellPlatform.Initialize()` now bootstraps the local runtime before `apps/windows-shell` starts normal UI work, and focused tests cover legacy import plus migration idempotence for that scaffold.
- Early `M2` slices are now landed: `apps/windows-shell-core` owns native local template catalog save + load services plus a native draft preview builder, `apps/windows-shell` can save the current Designer surface into the local catalog, reopen the selected local or packaged template directly into the WPF design surface, refresh the preview pane from the selected template document, and regenerate that draft preview from the current Designer surface on native preview actions or property edits without routing template open/write-back through companion code, and focused tests cover manifest creation, safe manifest update, malformed-manifest refusal, packaged-template load, local-overlay load, and native preview SVG generation.
- The current hardening pass closes the first native `M2` regressions before more feature work lands: local catalog save/load now uses one resolved overlay root, blocks path traversal, rejects embedded `template_version` drift, keeps a single SQLite default row, preserves template expressions on native open, keeps Designer geometry on the 12 px/mm scale used by the canvas, preserves the selected template across refresh, and debounces property-grid draft-preview refresh so operator messages do not churn away immediately.
- Native Designer draft preview is now routed through one `.NET` render-service path for both template-open and explicit preview refresh, instead of mixing hydrator-side SVG generation with action-side render calls.
- Native draft preview writes are now generation-guarded as well, so a slower render for an older template selection should not overwrite a newer Designer preview.
- Draft binding defaults such as `status`, `proof_mode`, `job`, and sample JAN/SKU values are now centralized in `apps/windows-shell-core`, so hydrator state and preview-render state do not each carry their own copy of that semantic baseline.
- One local render-service request can now return draft `SVG + PDF` artifacts for the Designer lane from a shared millimeter-based draft scene model, focused tests lock exact PDF media-box sizing plus shared barcode-frame geometry across both builders, and non-ASCII draft PDF failures now degrade to SVG-only preview with an explicit warning instead of taking down the whole native draft-preview chain. Proof/print authority still remains elsewhere, but the previous SVG/PDF scene-model split is now closed.
- The first native proof-side authority slice is now landed: live companion proof records are synchronized into the LocalAppData SQLite store, WPF approve/reject actions now persist through `apps/windows-shell-core` local proof service instead of calling companion review commands directly, local proof review decisions write native `audit_events`, and the refreshed WPF proof/audit lanes overlay local proof status back onto the live snapshot so review decisions stay visible immediately after refresh.
- The next native audit slice is now landed as well: live companion dispatch/audit rows and backup-bundle metadata are synchronized into the LocalAppData SQLite store, `apps/windows-shell-core` now owns a local audit mirror/export service, WPF `Export Audit` now writes JSON from that local SQLite-backed mirror instead of calling companion export directly, and refreshed `History` / `Print Console` lanes now read visible audit rows and bundle inventory back from that same local mirror instead of continuing to consume the companion snapshot directly.
- The next native proof-creation slice is now landed too: `apps/windows-shell-core` `CreateProofAsync()` now renders the proof PDF locally, persists a pending `proof_records` row plus matching proof-mode `dispatch_records` row and mirrored `audit_events` entry in one local transaction, and `apps/windows-shell` `Print Console` now exposes `Run Proof` from the selected subject payload instead of leaving proof generation companion-only.
- `docs/domain-model.md` is rewritten and back in the routing set as the domain authority for the `v1.0.0` direction.
- `docs/release/v1.0.0-acceptance.md` is the acceptance matrix for the first formal single-stack release target.
- `apps/admin-web`, `apps/desktop-shell`, and mixed-stack legacy authority paths are retirement targets once their `.NET` replacements are validated.
- New strategic workflow work should land in `apps/windows-shell`, not in legacy web or bridge surfaces.
- The dual-repository operating model remains in place: private repo for source-of-truth development, public repo for cost-sensitive GitHub-hosted automation or release surfaces.
- Cloud `.NET` validation is available and authoritative through `.github/workflows/ci.yml` job `windows-shell-native` on `windows-latest`.
- Codex workflows default to `ubuntu-latest` unless the repository variable `CODEX_RUNNER_LABELS_JSON` is set.
- Repository-dispatch replay and webhook relay now route through `scripts/dispatch-codex-event.mjs` and `docs/ops/codex-automation.md`.
- `OPENAI_API_KEY` is still required for GitHub-side Codex review, comment, triage, autofix, and maintenance execution.
- Local `cargo test --workspace` can intermittently hit `os error 5`; rerun once before treating it as a regression.
- Native-shell live parity still depends on a usable `desktop-shell.exe` today, but that dependency is itself a cleanup target under `T-052`.
- Native save-to-catalog, native open-from-catalog, native draft preview, native proof create/review persistence, native audit export, and native visible audit/history state are now local to WPF, but audit restore, retention apply, and dispatch still rely on hybrid runtime paths today.
- Local native-shell packaging may still be gated by missing `.NET`, MSVC, or `ISCC.exe`; GitHub Windows runners remain authoritative when those tools are absent.

## Read Next

- `docs/todo/active.md`
- `docs/dotnet-convergence.md`
- `docs/windows-rebuild-plan.md`
- `docs/domain-model.md`
- `docs/release/v1.0.0-acceptance.md`
- `docs/adr/0018-v1-release-target-and-local-dotnet-foundation.md`
- `docs/adr/0017-dotnet-only-convergence-and-legacy-stack-retirement.md`
- `docs/github-governance.md`
- `docs/ops/codex-automation.md`
- `docs/architecture.md`
- `docs/print-pipeline.md`
- `docs/known-issues.md`
- `docs/release/v0.3.0.md`
