# active-todo

Priority order for post-`v0.1.2` branch work.

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

## Now

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-028f-restore | P2 | pending | Codex + Sub-Agent | Audit backup restore flow | Backup bundles can be restored from the desktop UI or documented operator procedure |
| T-016 | P3 | pending | Codex | Release notes automation | Release notes can be generated from the maintenance ledger with low manual cleanup |
| T-043 | P3 | pending | Codex + Sub-Agent | Release checklist automation | The release branch can emit a concise machine-readable readiness report |

## Next

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-041 | P2 | pending | Codex + Sub-Agent | Local template catalog governance hardening | Local catalog maintenance has backup/restore guidance, manifest repair guidance, and clear single-writer operational rules |
| T-042 | P2 | pending | Codex + Sub-Agent | Template library operator UX | Operators can browse, select, and reason about packaged vs local templates with less ambiguity |
| T-044 | P2 | pending | Codex + Sub-Agent | Audit transaction hardening | Proof dispatch and pending-proof registration are atomic or have explicit recovery tooling |
| T-012 | P3 | pending | Codex + Infra | Self-hosted runner / webhook operations | GitHub-side Codex automation can run with predictable local/remote coordination |

## Blocked

None for PDF-only release milestone.  
`T-030` and `T-031` are moved to PDF-only deferral below.


## PDF-Only Deferred

| id | priority | status | owner | task | release scope |
| --- | --- | --- | --- | --- | --- |
| T-030 | P1 | deferred | Operator + Codex | Configure GitHub Actions `OPENAI_API_KEY` | Non-blocker for PDF-only release; next milestone (cloud/CI automation hardening) |
| T-031 | P1 | deferred | Operator + Codex | Physical printer matrix and measurement | Non-blocker for PDF-only release; next milestone (physical print validation) |
