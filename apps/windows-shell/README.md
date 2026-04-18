# Windows Shell

`apps/windows-shell` is the primary convergence surface for the Windows-only `.NET / WPF` product.

Current scope:

- traditional Windows menu/toolbar/docking shell
- `AdonisUI` + `AdonisUI.ClassicTheme` now own the shell chrome baseline so the app uses a free packaged classic desktop theme instead of a hand-drawn frame
- `Dirkster.AvalonDock` now owns the designer docking language so the shell uses real tool windows, docked inspectors, and document tabs instead of a fixed mock grid
- `PropertyTools.Wpf` now owns the right-side designer inspector baseline so property review is moving toward a real Windows property-grid surface
- module navigation for Home / Designer / Print Console / Batch Jobs / History
- module-specific operator workspaces instead of a single static mock:
  - Home migration and readiness dashboard
  - Designer docked toolbox, template library board, object browser, data-source panes, design documents, and package-backed property inspector
  - Print Console proof queue, dispatch grid, and guardrail inspector
  - Batch Jobs import sessions, queue table, and retry guardrails
  - History proof review, audit ledger, retention, and restore visibility
- shared context strip and status strip that keep lane purpose, authority, route, and active blocker signals visible in shell chrome
- designer canvas selection now drives the property inspector, so the inspector is no longer a static metadata pane
- Home and Designer now share a reusable template-library board that makes default winner, draft-only state, dispatch safety, authority owner, and rollback path visible from one panel
- Home and Designer template-library boards now read the packaged template manifest and local overlay directly from WPF instead of depending on companion snapshot data for catalog hydration
- `apps/windows-shell-core` now owns the native template-catalog filesystem read and packaged/local merge model so the rebuild can move shell logic out of giant WPF files before broader workflow replacement
- shell startup now initializes the LocalAppData-rooted runtime scaffold, SQLite database, and one-time legacy import tracking before live lane refresh begins
- Designer `Save to Catalog` now persists the current design surface into the native local catalog through `apps/windows-shell-core` instead of routing template write-back through companion-only code
- Designer template selection now reopens the selected packaged or local catalog template directly into the WPF design surface through `apps/windows-shell-core` instead of leaving the canvas seeded
- Designer preview pane now refreshes with a native draft SVG generated from the opened template document instead of keeping the last companion preview stuck after template changes
- Designer preview-refresh actions and property edits now regenerate that native draft preview from the current WPF design surface instead of forcing a live companion refresh just to inspect draft-state changes
- menu, toolbar, and header actions now route operators toward the relevant lane and produce visible shell feedback instead of dead UI buttons
- shell actions now also focus the relevant template, canvas object, proof, job, batch, or audit item in place so the operator lands on actionable context instead of a generic lane shell
- Print Console, Batch Jobs, and History now use selection-driven detail boards so choosing a proof, job, import session, bundle, or ledger row updates blocker, route, and next-action context instead of leaving the right pane static
- the shared context strip and status strip now also follow the currently selected template/object/proof/job/batch/bundle row so shell chrome mirrors in-lane decision context instead of staying module-static
- the shell title and workspace lead text now also follow the current in-lane focus so the top chrome reads like the active operator task, not just the module name
- a `DesktopShellCompanionClient` now launches `apps/desktop-shell --native-shell-companion` as a hidden child process and reads live bridge/catalog/governance/preview/audit state from the existing Rust authority path
- Home / Designer / Print Console / History now render live desktop-shell-backed data instead of staying entirely seeded
- native proof approve / reject, native `Run Proof`, and native audit export now execute through the local SQLite-backed path
- native save-to-catalog, native open-from-catalog, native draft preview, native proof create/review, and native visible audit state are live, but authoritative proof preview, retention apply, restore, and dispatch still remain hybrid paths today
- quick-access actions now mirror the current lane's real action state instead of staying hard-wired and always enabled
- refresh-style actions now report lane-specific results and keep companion refresh failures visible instead of overwriting them with generic success text
- shell chrome now keeps runtime mode visible as `seeded shell`, `live companion`, `seeded fallback`, or `stale live snapshot` instead of hiding companion state in the last action message only
- companion responses are now checked against the sent `requestId`, and repeated refreshes reuse the last preview result when the template source is unchanged
- preview state now distinguishes `live`, `cached`, `fallback`, and `degraded` instead of treating every non-null preview as equally live
- Home, Print Console, Batch Jobs, and History now use resizable WPF work areas with `TabControl`, `DataGrid`, and `GridSplitter` instead of fixed stacked list panes
- pane secondary labels in Home, Print Console, Batch Jobs, and History now come from workspace model state instead of hardcoded placeholder text, so seeded/live/transitional context stays visible inside the work area
- Designer and the shared template-library board now also derive operator-facing counts and entries summaries from workspace model state instead of hardcoded placeholder totals
- companion requests now honor cancellation during stdio I/O, and shell shutdown no longer waits indefinitely on a stuck companion request before closing
- placeholder designer actions are now explicitly disabled until real editor handlers exist, rather than reading as parity-complete commands
- unsupported `v0.3.0` actions are now explicitly disabled or explanatory in WPF instead of implying direct backend ownership
- `admin-web` now persists a desktop-shell-owned shared batch snapshot, and `Batch Jobs` reads that live queued-row state through the companion path while keeping import / retry / submit disabled in WPF
- proof-review lanes now load a larger companion-backed audit window instead of truncating pending proofs to a tiny fixed slice
- shared batch snapshot load failures now degrade only the Batch Jobs lane instead of dropping the whole shell out of live mode
- native-shell build / publish outputs now stage `desktop-shell.exe` beside the WPF shell when the companion has already been built, so packaged live mode does not rely on repo-relative fallback paths
- focused `windows-shell` tests now cover companion audit-window sizing, batch-lane-only degradation, and lane mapping without UI automation
- legacy backend authority for dispatch, restore, retention apply, and template write-back still remains outside this app today and is a replacement target during convergence
- startup still seeds the workstation immediately, then attempts a companion refresh on `Loaded`; if the companion is unavailable, the shell stays in seeded explanatory mode and reports the refresh failure
- non-live runtime states also prefix the workspace lead text so operators can tell they are in seeded/fallback mode without checking the status strip
- GitHub Windows CI publishes both a self-contained preview build artifact and an installer artifact for operator UI review

Current development rule:

- new strategic workflow work belongs in `apps/windows-shell`
- pure Windows-side workflow/domain logic should be extracted into shared `.NET` support libraries such as `apps/windows-shell-core`
- legacy mixed-stack behavior is reference material, not the implementation template
- once a workflow is replaced natively, the superseded legacy slice should be deleted

The existing `apps/admin-web` + `apps/desktop-shell` path remains the current shipped baseline only until each workflow is replaced and retired.

Packaging note:

- `windows-shell-native` on GitHub Actions is the authoritative Windows validation and preview packaging path during `T-052`
- the job publishes a self-contained app artifact and an Inno Setup installer artifact
- formal tag releases now also build and upload the native-shell installer asset through `.github/workflows/release.yml`
- installer previews are for UI/UX validation, not the production proof/print release path

Release target:

- the next formal release target is `v1.0.0`
- `v0.3.1` is historical preview context only
- current shell work is foundation and workflow replacement for the `v1.0.0` single-stack route, not a standalone preview line
