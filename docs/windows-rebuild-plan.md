# windows-rebuild-plan

Execution plan for the Windows-only `.NET / WPF` rebuild.

This file is the practical sequencing doc for `T-052`.

Use [dotnet-convergence.md](/Users/Omo/Downloads/JAN-label/docs/dotnet-convergence.md) for the architectural rule.
Use this file for the actual order of work.

## Non-Negotiable Rules

- Rebuild in `.NET / WPF`; do not port mixed-stack implementation details line by line.
- Delete old slices as soon as the Windows replacement for that slice is usable.
- Keep only one active source of truth per workflow.
- Validate on GitHub Windows runners whenever local `dotnet` is unavailable.

## Phase Order

1. Core extraction
   - Move pure Windows-side logic out of giant shell files and into dedicated `.NET` libraries.
   - Remove direct dependence on mixed-stack DTO shape where a native model can replace it.
   - Current batch starts here.

2. Catalog and designer authority
   - Native packaged/local template catalog read
   - Native save-to-catalog flow
   - Native preview and template state model
   - Delete corresponding web-only catalog surfaces once WPF owns the workflow

3. Batch workflow rebuild
   - Native import session model
   - Native CSV/XLSX ingest
   - Native validation, retry, and submit staging
   - Delete `admin-web` batch workflow slices after WPF replacement exists

4. Proof and audit rebuild
   - Native proof inbox
   - Native approve/reject
   - Native audit search/export/restore review
   - Remove companion-only proof/audit shell wiring when WPF owns the flow

5. Dispatch and print rebuild
   - Native dispatch gate
   - Native route selection and print job submission
   - Native lineage and artifact checks
   - Delete `desktop-shell` bridge ownership after parity and guardrails are proven

6. Legacy retirement
   - Delete `apps/admin-web`
   - Delete `apps/desktop-shell`
   - Delete mixed-stack bridge/companion glue
   - Delete Rust app-layer slices that are no longer required

## Current Batch

Current rebuild batch:

- retarget release docs and domain docs to `v1.0.0`
- extract Windows-native catalog logic into a dedicated `.NET` core library
- establish the LocalAppData-rooted application runtime layout
- create the first SQLite schema and migration-run tracking
- import reusable legacy local state once instead of keeping legacy runtime directories as the steady-state store
- keep `Home` and `Designer` template boards on native catalog models
- stop adding new catalog behavior to companion-only code paths

Landed so far:

- `apps/windows-shell-core` exists and owns native template catalog filesystem + merge logic
- `apps/windows-shell` catalog boards now consume the shared native catalog model
- `apps/windows-shell-core` now owns local runtime path resolution, SQLite bootstrap, legacy import scaffolding, and first-pass service contracts
- shell startup now initializes the local runtime before normal UI work begins
- native `Save to Catalog` now persists the current Designer surface into the local catalog through `apps/windows-shell-core`
- native template open now reloads the selected packaged or local catalog document directly into the WPF Designer surface through `apps/windows-shell-core`
- native draft preview now refreshes from the opened template document instead of keeping the last companion SVG stuck in the preview pane
- native preview-refresh actions now regenerate that draft preview from the current Designer surface instead of forcing a live companion refresh
- the first native hardening pass now keeps catalog writes inside the local overlay root, rejects embedded template-version drift, normalizes SQLite default-row state, preserves template expressions on open, keeps geometry edits on the canvas scale, preserves the chosen template across refresh, and debounces property-grid preview refresh
- template-open preview and explicit preview refresh now share the same local render-service path instead of maintaining separate SVG-generation routes
- draft binding defaults now come from one core helper instead of being hand-built separately in the hydrator and the preview-refresh path
- that local render-service path now feeds both draft SVG and draft PDF builders from one shared millimeter-based scene model, with focused tests covering exact PDF media-box sizing, shared barcode-frame geometry, and the dual-output contract
- live proof rows now sync into a native SQLite-backed proof ledger, WPF approve/reject persists through that local proof service, and refreshed proof/audit lanes overlay local proof-review state back onto the live snapshot
- live dispatch/audit rows and backup-bundle metadata now sync into a native SQLite-backed audit mirror, WPF audit export now writes JSON from that local mirror instead of calling companion export directly, and refreshed History / Print Console lanes now read visible audit rows plus bundle inventory back from that same local mirror
- native `Run Proof` now renders the proof PDF locally and writes the proof ledger row, matching proof dispatch row, and mirrored dispatch audit event in one SQLite transaction for the selected Print Console subject

Done when:

- `apps/windows-shell-core` exists and owns native catalog filesystem logic
- `apps/windows-shell` consumes the new core library instead of embedding that logic in shell files
- local runtime bootstrap and migration state exist independently of companion DTOs
- designer save-to-catalog no longer depends on companion write-back
- designer open-from-catalog no longer depends on seeded canvas state or companion-side document hydration
- designer draft preview no longer depends on stale companion SVG after the selected template changes
- proof review persistence no longer depends on companion approve/reject commands
- proof generation for the selected Print Console subject no longer depends on companion proof commands
- audit export no longer depends on companion export commands
- visible audit rows and bundle listing no longer depend on direct companion snapshot consumption inside WPF
- docs and handoff status point to this batch explicitly

## Delete Gates

Delete a legacy slice only when all of these are true:

- the Windows replacement exists
- the replacement has a clear validation path
- the replacement is the documented source of truth
- handoff and todo docs were updated in the same pass

Do not keep dead bridge code just because it still compiles.

## Validation

Primary Windows validation path:

- workflow: `.github/workflows/ci.yml`
- job: `windows-shell-native`
- runner: `windows-latest`

Required commands in that path:

- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release`
- `dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release`

## Documentation Sync

Whenever the active rebuild batch changes, update:

- [docs/handoff/current-state.md](/Users/Omo/Downloads/JAN-label/docs/handoff/current-state.md)
- [docs/todo/active.md](/Users/Omo/Downloads/JAN-label/docs/todo/active.md)
- [docs/dotnet-convergence.md](/Users/Omo/Downloads/JAN-label/docs/dotnet-convergence.md)

If the delete boundary changes, update the relevant ADR in `docs/adr/`.
