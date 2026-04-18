# dotnet-convergence

Working development route for `T-052`.

## Goal

Move JAN Label to a Windows-only, `.NET / WPF` single-stack operator product.

Target end state:

- `apps/windows-shell` is the only long-term operator application.
- Operator workflows are implemented natively in `.NET / WPF`.
- Existing `apps/admin-web`, `apps/desktop-shell`, and Rust app-layer slices are replaced and then deleted.
- UI uses package-backed Windows controls and normal desktop interaction patterns, not browser-like shell chrome.

## Core Rule

Do not port the old implementation line by line.

The old stack is reference material for requirements, edge cases, fixtures, and operator expectations. It is not the template for the new codebase.

## Rebuild Rule

This convergence path allows aggressive cleanup.

- Legacy mixed-stack code is not protected.
- If a slice is judged structurally wrong, it should be cleared and rebuilt in `.NET / WPF` instead of being adapted.
- Compatibility shims are temporary at most; they are not a success criterion.
- The repository may be simplified by deleting old app-layer code as soon as the replacement scaffold or workflow exists in the Windows stack.

The target is the lowest long-term maintenance cost, not preserving prior implementation investment.

## Replacement Rule

For each workflow:

1. Define the operator outcome and required guardrails.
2. Rebuild that workflow natively in `.NET / WPF`.
3. Verify the new path against fixtures, documents, and real operator scenarios.
4. Delete the superseded legacy slice.

If a legacy implementation is poorly structured, the correct action is replacement and deletion, not migration of the same design into a new framework.

## Scope Of Legacy Deletion

The following areas are legacy retirement targets once replaced:

- `apps/admin-web`
- `apps/desktop-shell`
- legacy bridge / companion contracts that only exist to connect WPF to the old stack
- Rust app-layer authority that is no longer needed after the `.NET` replacement is validated

Legacy code survives only while it is still the active implementation for an unreplaced workflow.

If keeping a legacy slice in-tree slows down the rebuild, removing that slice is allowed. The rule is to protect documented operator behavior, not old source trees.

## Where New Work Goes

Strategic product work now goes here:

- `apps/windows-shell`
- shared `.NET` libraries that support the Windows shell
- Windows-native tests, fixtures, and validation harnesses

Do not add new strategic workflow features to:

- `apps/admin-web`
- `apps/desktop-shell`
- bridge code whose only purpose is preserving the mixed-stack design

## Workflow Ownership Target

- `Home`: workstation status, readiness, operator entry points
- `Designer`: template authoring, object selection, properties, preview, save/publish flow
- `Print Console`: proof review, approval, rejection, dispatch decisions
- `Batch Jobs`: import, validation, retry, submit, batch state review
- `History`: audit search, export, retention, restore

Each lane should become natively actionable inside `apps/windows-shell`, not a thin shell over a legacy host.

## Current Landed Slice

The first active convergence slice is `M0 + M1` foundation work plus the first `M2` Designer/catalog replacements.

- `Home` and `Designer` template-library boards now read the packaged manifest and local overlay directly from WPF.
- Companion snapshot data is no longer required just to hydrate that catalog board.
- `apps/windows-shell-core` is the first extracted `.NET` support library and now owns the native template catalog model plus packaged/local merge logic.
- `apps/windows-shell-core` now also owns LocalAppData path resolution, SQLite schema bootstrap, one-time legacy runtime import scaffolding, and first-pass service contracts for the single-stack runtime.
- `apps/windows-shell-core` now also owns the first native local-catalog save service, and `apps/windows-shell` Designer can persist the current design surface into the local catalog without companion write-back.
- `apps/windows-shell-core` now also owns the first native template-document load service, and `apps/windows-shell` Designer now reopens the selected local or packaged catalog template directly into the WPF design surface instead of leaving the canvas seeded while only the side panel selection changes.
- `apps/windows-shell-core` now also owns a native draft SVG preview builder, and `apps/windows-shell` Designer now refreshes the preview pane from the opened template document instead of showing a stale companion SVG after template selection changes.
- Designer `Print Preview` / preview-refresh actions in the WPF lane now regenerate that native draft preview from the current Designer surface instead of forcing a live companion refresh just to see draft-state changes.
- The first native Designer/catalog hardening pass is also landed: local catalog save/load now shares one overlay root, blocks path traversal, rejects embedded template identity drift, maintains a single SQLite default row, keeps field template expressions intact on native open, keeps geometry round-trips on the 12 px/mm canvas scale, preserves the chosen template across refresh, and debounces property-grid preview churn.
- Template-open preview and explicit preview-refresh actions now both route through the same local render service, so the native draft SVG no longer has one hydrator-side generator for open and a different render-service path for later refresh.
- Native draft preview writes now also carry a generation guard so a slower render for an older template selection does not overwrite a newer Designer preview.
- Canonical draft binding defaults now live in `apps/windows-shell-core`, so `status`, `proof_mode`, and sample record values are shared between the Designer hydrator and the local render-service preview path.
- One local render-service request can now return both draft `SVG` and draft `PDF` artifacts for the Designer lane through one shared millimeter-based draft scene model, with focused tests locking exact PDF media-box sizing, shared barcode-frame geometry, and the dual-output artifact contract. The previous SVG/PDF scene-model split is now closed; the remaining draft-PDF limitation in this area is non-ASCII text fallback.
- The first native proof-review slice is now landed as well: `apps/windows-shell-core` owns a local proof ledger service backed by SQLite, live proof rows from companion refresh are synchronized into that ledger, WPF approve/reject actions now persist through the native proof service instead of companion review commands, and local proof decisions write native audit-event rows before the refreshed shell overlays local proof state back onto the live proof/audit snapshot.
- The next native audit slice is now landed too: `apps/windows-shell-core` owns a local audit mirror/export service, live companion dispatch/audit rows plus backup-bundle metadata now synchronize into SQLite during refresh, WPF audit export now writes from that local mirror instead of calling companion export directly, and refreshed `History` / `Print Console` lanes now read their visible audit rows and bundle inventory back from that same local mirror instead of continuing to consume the companion snapshot directly.
- The next native proof slice is now landed too: `apps/windows-shell-core` `CreateProofAsync()` now renders the proof PDF locally, persists a pending `proof_records` row plus matching proof-mode `dispatch_records` row and mirrored `audit_events` entry in one local transaction, and `apps/windows-shell` `Print Console` now exposes `Run Proof` from the selected subject payload instead of leaving proof generation companion-only.
- `WindowsShellPlatform.Initialize()` now runs before the WPF shell starts so local runtime bootstrap failures surface immediately.
- Proof preview authority, audit restore, retention apply, and dispatch are still hybrid paths today; the new local proof and audit slices now cover sync + visible audit projection + proof create/review persistence + export, not the full proof/audit/dispatch authority surface yet.
- This is a replacement step, not a port of the old UI: the shell owns the catalog read path, catalog write path, template open path, draft preview path, and local runtime foundation while broader workflow replacement continues lane by lane.

## Cloud Validation Rule

When local `.NET` tooling is unavailable, the authoritative Windows-native validation path is GitHub Actions.

- `.github/workflows/ci.yml`
- job: `windows-shell-native`
- runner: `windows-latest`
- commands:
  - `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
  - `dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release`

For this repository, cloud Windows runners are the correct fallback whenever the local machine cannot run `dotnet`.

## Validation Rule

A legacy slice can be deleted only after:

- the replacement workflow runs in `.NET / WPF`
- required guardrails and validation exist in the new path
- docs and handoff state are updated in the same pass

Do not keep obsolete code "just in case" after the replacement path is validated.

## Dual-Repo Rule

Keep the current two-repository operating model:

- private repository: source of truth for day-to-day development
- public repository: GitHub-hosted automation or release surfaces that are cheaper or operationally necessary there

The dual-repo workflow remains an operations decision. It does not change the product architecture target.
