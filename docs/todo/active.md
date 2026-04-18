# active-todo

Priority order for the direct `v1.0.0` route.

## Quick Read

- Repository-level task status in this file is aligned to `origin/main`.
- If a feature branch disagrees with this file, assume the branch is carrying older or branch-only task context until the branch updates handoff and todo together.
- `Now`: `T-052`
- `Next`: `T-052`
- `Deferred`: `T-030`, `T-031`

## Done

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-026 | P1 | done | Codex + Sub-Agent | Connect `admin-web` to `desktop-shell` bridge | Manual and batch submit run through the desktop bridge |
| T-026d | P1 | done | Codex + Sub-Agent | Structured bridge warnings | `warningDetails[]` exposes `code / severity / message` and UI blocks high-risk submit |
| T-026e | P1 | done | Codex + Sub-Agent | XLSX numeric JAN hardening | Scientific / decimal / ambiguous 12-digit numeric JAN is blocked; 13-digit numeric JAN warns |
| T-026f | P1 | done | Codex + Sub-Agent | Batch retry hardening | Only `ready` and `failed` rows re-submit; `submitted` rows do not |
| T-026g | P1 | done | Codex + Sub-Agent | Template catalog mismatch submit blocker | Unknown live `template_version` blocks queue/manual/batch submit |
| T-027a | P1 | done | Codex + Sub-Agent | Proof ledger and review API | `approve_proof`, `reject_proof`, and audit search are available |
| T-027b | P1 | done | Codex + Sub-Agent | Proof inbox UI | Pending proofs can be approved/rejected and approved proofs can be pinned |
| T-027c | P1 | done | Codex + Sub-Agent | Strict approved-proof match for print | Print is rejected unless proof subject and lineage match |
| T-027d | P1 | done | Codex + Sub-Agent | Proof lineage authority in backend | Explicit lineage and `reprintOfJobId` must match approved proof lineage |
| T-027e | P1 | done | Codex + Sub-Agent | Approved proof artifact validation | Missing, empty, and invalid PDF proof artifacts are rejected |
| T-028a | P1 | done | Codex + Sub-Agent | Local audit ledgers | Dispatch and proof ledgers persist locally |
| T-028b | P1 | done | Codex + Sub-Agent | Audit search UI | Local ledger search and proof status visibility are available |
| T-028c | P2 | done | Codex + Sub-Agent | Legacy proof seed / migration | Validate and seed pending proofs from CSV/XLSX |
| T-028d | P1 | done | Codex + Sub-Agent | Audit persistence fatal on dispatch | Dispatch refuses to run when audit persistence cannot be trusted |
| T-028e | P1 | done | Codex + Sub-Agent | Audit export / retention / backup | Scoped export, dry-run trim, apply trim, and backup bundles work |
| T-028f-list | P2 | done | Codex + Sub-Agent | Audit backup bundle listing | `admin-web` and `desktop-shell` can list retention backup bundles with metadata |
| T-029 | P2 | done | Codex + Sub-Agent | Operator runbook and stop/restart/escalation rules | PDF-only operator checklist and release handoff are documented |
| T-045 | P1 | done | Codex + Sub-Agent | Cut PDF-only release `v0.1.2` | Tag, GitHub Release workflow, and Windows installer asset are published |
| T-032b | P1 | done | Codex + Sub-Agent | Structured editor UX and preview workbench | Editor, local canvas, and Rust preview are usable together |
| T-032c | P1 | done | Codex + Sub-Agent | Desktop template catalog sync | `admin-web` reads desktop catalog and blocks unknown template routes |
| T-032a | P1 | done | Codex + Sub-Agent | Local template catalog write-back | Live template JSON can be saved into the desktop local catalog |
| T-033a | P1 | done | Codex + Sub-Agent | Authored template proof/print route parity | Saved local templates are used for validation and actual proof/print render |
| T-033b | P1 | done | Codex + Sub-Agent | Render parity | Border/background/color handling is aligned in SVG/PDF |
| T-034 | P1 | done | Codex + Sub-Agent | Template schema and manifest core | `packages/templates` and `crates/render` share manifest/schema rules |
| T-035 | P1 | done | Codex + Sub-Agent | Template asset export/import | Template source, form state, mapping, and draft snapshot move together |
| T-037 | P1 | done | Codex + Sub-Agent | Tauri dispatch contracts | `dispatch_print_job` and `print_bridge_status` contracts are aligned |
| T-038 | P1 | done | Codex + Sub-Agent | Printer route and proof gate hardening | Adapter route and proof gate checks are enforced in bridge/backend |
| T-039 | P1 | done | Codex + Sub-Agent | Import / retry / batch UX hardening | Queue snapshot, retry lineage, lazy XLSX import, and submit state are stable |
| T-040 | P1 | done | Codex + Sub-Agent | Render/schema hardening | SVG attribute safety and template color constraints are enforced |
| T-046a | P1 | done | Codex + Sub-Agent | Desktop shell UX reset | `admin-web` uses a rail/workspace/inspector/status-bar shell instead of a single long webpage layout |
| T-046 | P1 | done | Codex + Sub-Agent | Queue and audit desktop grid tooling | Queue/audit lanes support desktop-oriented sort, filter, page navigation, and submit-time mutation guards |
| T-047 | P1 | done | Codex + Sub-Agent | Template authoring interaction hardening | Template lane separates structure, fields, review, and catalog concerns and compose/template review makes live-vs-staged-vs-dispatch authority explicit |
| T-045b | P1 | done | Codex + Sub-Agent | Cut `v0.1.3` desktop shell release | Tag, GitHub Release workflow, and Windows installer asset are published for the desktop UI reset |
| T-028f-restore | P2 | done | Codex | Audit backup restore flow | Backup bundles can be restored from the desktop UI through `restore_audit_backup_bundle` with conflict-safe merge semantics |
| T-016 | P3 | done | Codex | Release notes automation | `pnpm release:notes --version <version>` writes `docs/release/<version>.md` from handoff, git summary, and Maintenance Ledger context |
| T-043 | P3 | done | Codex | Release checklist automation | `pnpm release:readiness --version <version>` writes machine-readable release readiness artifacts and the release workflow uploads them |
| T-048 | P1 | done | Codex | `v0.2.0` operator workstation redesign | `admin-web` uses a dark-neutral, dense three-pane workstation with lane-aware inspector focus across compose/template/queue/audit |
| T-045c | P1 | done | Codex | Cut `v0.2.0` operator workstation release | PR `#31` merged, tag `v0.2.0` published, GitHub Release workflow succeeded, and the Windows installer asset is available |
| T-041 | P2 | done | Codex + Sub-Agent | Local template catalog governance hardening | Catalog lane exposes backup/restore guidance, manifest repair guidance, local overlay diagnostics, and explicit single-writer operating rules |
| T-044 | P2 | done | Codex + Sub-Agent | Audit transaction hardening | Proof dispatch commits through a recovery marker, stale markers auto-recover on locked audit access, corrupt markers are quarantined for manual reconcile, and command-level proof dispatch coverage exists |
| T-042 | P2 | done | Codex | Template library operator UX | `apps/windows-shell` now exposes a reusable template-library board where operators can browse, select, and reason about packaged vs local templates, default winner, draft-only state, dispatch safety, and rollback path with less ambiguity |
| T-049 | P1 | done | Codex | Windows-native workstation shell migration | `apps/windows-shell` uses package-backed Windows shell primitives (`Fluent.Ribbon`, `Dirkster.AvalonDock`, `PropertyTools.Wpf`), builds on GitHub Windows runners, installer previews can be published from CI artifacts, the shell exposes module-aware operator workspaces for `Home`, `Designer`, `Print Console`, `Batch Jobs`, and `History`, shell actions land on the relevant template/object/proof/job/batch/bundle context, shared shell chrome follows the active in-lane selection, the shell headline reflects the focused work item, companion-backed read + safe ops are live for bridge/catalog/governance/preview/proof/audit state plus proof approve/reject and audit export, proof-review lanes do not silently truncate pending companion-backed review items, optional shared batch snapshot failures degrade only the Batch Jobs lane instead of the whole shell, companion packaging and validation prove a usable `desktop-shell` binary is present, focused `windows-shell` tests cover companion snapshot isolation and lane mapping, and direct print, restore, retention apply, template write-back, and direct batch mutation remain explicitly outside WPF for `v0.3.0` |
| T-045d | P1 | done | Codex + Sub-Agent | Cut `v0.3.0` Windows-native workstation release | `main` and tag `v0.3.0` were pushed, the private-repo release attempt was identified as billing-blocked before job start, `WSL043/JAN-label-public` ran the authoritative Release workflow successfully on `2026-04-17`, release readiness passed on the public Windows runner, and the desktop-shell installer plus native-shell installer and `.sha256` assets were published |
| T-050 | P1 | done | Codex | Single-installer WPF shell reset for `v0.3.1` preview | Old desktop-shell installer is removed from the user-facing release surface, `apps/windows-shell` is the only visible Windows app, the preview shell uses free packaged `AdonisUI` desktop chrome instead of ribbon-first custom framing, and the `0.3.1-rc4` preview installer was validated from the installed `JanLabel.exe` path |
| T-012 | P3 | done | Codex + Infra | Self-hosted runner / webhook operations | Codex workflows support configurable runner labels, `repository_dispatch` replay, and a documented webhook relay path without widening same-repo safety boundaries |
| T-051 | P3 | done | Codex | Admin-web XLSX parsing worker | Spreadsheet parsing for CSV/XLSX and legacy proof seed uploads keeps the `xlsx` decode path off the main UI thread while preserving existing import validation semantics |

## Now

`T-052` is active. Current batch is `M0 + M1` foundation work plus the first `M2/M3` Designer/catalog replacements, the first native proof-review slice, the next native audit-mirror slice, and the first native proof-create slice for the direct `v1.0.0` route: native template catalog resolution for `Home` and `Designer`, rewritten `docs/domain-model.md`, a first `v1.0.0` acceptance matrix, LocalAppData path resolution, SQLite bootstrap, legacy runtime import scaffolding, migration tracking, first-pass `.NET` service contracts in `apps/windows-shell-core`, native `Save to Catalog` from the WPF Designer lane, native open-from-catalog hydration back into the WPF design surface, native draft preview refresh for both opened templates and current Designer-surface edits, a follow-up hardening pass that fixes path containment, template identity drift, default-row consistency, authoring expression preservation, 12 px/mm geometry round-trip, refresh template retention, and debounced preview refresh, a render-path cleanup that routes template-open preview and explicit preview refresh through the same local render service, a preview generation guard so stale render results do not overwrite a newer template selection, centralized draft-binding defaults so hydrator and preview render do not drift on `status` / `proof_mode` / sample record semantics, a shared millimeter-based draft scene model that now feeds both the draft SVG builder and the draft PDF builder with focused tests for exact PDF media-box sizing, shared barcode-frame geometry, and SVG-only degradation when draft PDF text encoding is unsupported, a local proof ledger sync/review path that persists WPF approve/reject decisions through SQLite, a local audit mirror path that now syncs live dispatch/audit rows plus backup-bundle metadata into SQLite, writes audit export JSON from that local mirror, and projects visible History / Print Console audit rows plus bundle inventory back out of that same SQLite state after refresh, plus native `Run Proof` from `Print Console` that now writes the proof artifact, proof ledger row, matching proof dispatch row, and mirrored audit event in one local transaction. Batch order remains tracked in `docs/windows-rebuild-plan.md`.

## Next

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-052 | P1 | in_progress | Codex | `.NET-only` convergence, cleanup, and rebuild | Required operator workflows run natively from `apps/windows-shell`, the mixed-stack implementation is rebuilt rather than ported forward, and superseded legacy app surfaces can be deleted aggressively once the Windows-stack replacement exists. Current landed slices: WPF now reads packaged/local template catalog state directly for Home and Designer, `apps/windows-shell-core` now owns LocalAppData path resolution, SQLite bootstrap, legacy runtime import scaffolding, first-pass `.NET` service contracts, native local-catalog save, native template-document load, and a dual-output draft preview path that now runs through one shared millimeter-based draft scene model for both `SVG` and `PDF`, `apps/windows-shell` Designer can reopen the selected local or packaged template directly into the design surface and refresh the preview pane from both that document and the current Designer surface, the first native Designer/catalog hardening pass now keeps authoring expressions and geometry stable while blocking catalog path/identity drift, WPF approve/reject now persists through a native local proof ledger that syncs live proof records into SQLite and writes local audit events, the native local audit mirror now syncs live dispatch/audit rows plus backup-bundle metadata before both `Export Audit` and the visible `History` / `Print Console` audit rows read back from that same SQLite state, and `Print Console` `Run Proof` now creates the local proof artifact plus matching proof/dispatch ledger rows instead of leaving proof generation companion-only. Non-ASCII draft PDF text still degrades to SVG-only warning until deeper render/text work lands, and the formal release target is now `v1.0.0` instead of the old `v0.3.1` preview line |

## Blocked

None for the PDF-only release milestone.

`T-030` and `T-031` are moved to PDF-only deferral below.

## PDF-Only Deferred

| id | priority | status | owner | task | release scope |
| --- | --- | --- | --- | --- | --- |
| T-030 | P1 | deferred | Operator + Codex | Configure GitHub Actions `OPENAI_API_KEY` | Non-blocker for PDF-only release; next milestone (cloud/CI automation hardening) |
| T-031 | P1 | deferred | Operator + Codex | Physical printer matrix and measurement | Non-blocker for PDF-only release; next milestone (physical print validation) |
