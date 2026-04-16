# print-pipeline

## 1. Main Flow

1. `admin-web` assembles parent SKU, SKU, JAN, qty, brand, template route, and printer profile.
2. CSV/XLSX can be imported and mapped through alias-based column matching.
3. Rows are classified as `ready`, `pending`, or `error`.
4. `admin-web` snapshots ready rows for manual or batch submit.
5. `desktop-shell` receives `dispatch_print_job`.
6. `desktop-shell` validates:
   - bridge environment
   - audit store readiness
   - proof gate
   - template route
   - printer route
7. Rust performs JAN normalization, lineage validation, render, adapter submission, and audit persistence.

## 2. Print Gate

Print requires all of the following unless the release-disabled bypass is intentionally reintroduced:

- `sourceProofJobId`
- an approved proof in the proof ledger
- a matching proof dispatch record
- a valid template route
- exact match on:
  - `templateVersion`
  - `sku`
  - `brand`
  - normalized `jan`
  - `qty`
  - `lineage`
- a readable non-empty PDF proof artifact with a valid `%PDF-` header
- a writable audit store

Lineage rules:

- If `jobLineageId` is omitted, `desktop-shell` derives lineage from the approved proof.
- If `jobLineageId` is supplied, it must match the approved proof lineage.
- If `reprintOfJobId` is supplied, it must also match the approved proof lineage chain.

## 3. Proof Flow

- Proof submit is forced through the PDF route.
- `desktop-shell` writes `proof-dispatch-transaction.json` before committing proof audit state.
- Proof dispatch in `dispatch-ledger.json` and the matching `pending` proof record in `proof-ledger.json` are applied as one recovery-aware transaction.
- Stale proof transaction markers are replayed automatically on the next locked audit access.
- Unreadable or corrupt proof transaction markers are quarantined and the current audit operation returns an explicit reconcile error.
- `admin-web` proof inbox drives `approve` / `reject`.
- Approved proofs can be pinned back into print-ready operator flow.

## 4. Audit Export / Retention

- `export_audit_ledger` returns `all`, `dispatch`, or `proof` snapshots.
- `admin-web` can export those snapshots as JSON.
- `trim_audit_ledger` supports:
  - `maxAgeDays`
  - `maxEntries`
  - `dryRun`
- Trim preserves proof/proof-dispatch dependency chains.
- Applied trim writes a backup bundle under `audit/backups/`.
- `restore_audit_backup_bundle` merges a selected backup bundle back into the active ledger.

Restore rules:

- restore is explicit operator action from the audit lane
- conflicts against existing dispatch/proof job ids fail the whole restore
- invalid bundle schema fails the whole restore
- after successful restore, `admin-web` refreshes audit search and backup inventory

## 5. Legacy Proof Seed Flow

1. `admin-web` imports CSV/XLSX for legacy proof seed.
2. `validate_legacy_proof_seed` performs row-level checks.
3. `seed_legacy_proofs` writes matching pending proof and dispatch records.
4. Seeded records still require normal approve/reject review.

Safety:

- `artifactPath` must resolve to a PDF under the configured proof output expectations.
- Existing `proofJobId` / `jobLineageId` collisions are rejected.
- Seed does not create approved proofs directly.

## 6. Bridge Status

`print_bridge_status` reports:

- available adapters
- resolved Zint path
- proof / print / spool output directories
- audit log / audit backup directories
- active print adapter
- Windows printer name
- `warningDetails[]`
  - `code`
  - `severity`
  - `message`
- legacy `warnings[]`

`admin-web` treats `severity === "error"` warnings as submit blockers.

## 7. Template Authoring / Preview

Current authoring flow:

1. `admin-web` reads the desktop template catalog from `desktop-shell`.
2. The template lane is split into four operator sub-flows:
   - `Structure`: template identity, page, and border controls
   - `Fields`: text bindings, ordering, and placement
   - `Review`: local canvas vs Rust renderer comparison
   - `Catalog`: JSON editing, import/export, and save-to-local-catalog actions
3. Structured controls update the live JSON draft immediately.
4. Local canvas preview shows approximate layout only.
5. `preview_template_draft` renders live JSON through Rust for authoritative SVG preview.
6. The operator saves valid live JSON into the desktop local template catalog.
7. Saved local templates become valid dispatch targets by `template_version`.
8. Proof and print dispatch resolve the same packaged/local overlay manifest that catalog validation uses.

Important distinctions:

- Browser autosave preserves the live draft locally, but it is not catalog approval.
- Local canvas preview is approximate.
- Rust preview is authoritative for the current live JSON draft.
- Proof/print dispatch is authoritative for saved catalog entries, not unsaved editor state.
- Unknown live `template_version` blocks queue/manual/batch submit.
- Compose review treats staged snapshots as pinned review/export copies only; submit still uses the live payload.

## 8. Excel / CSV Import Direction

- CSV/XLSX should remain usable without a strict external database schema.
- Alias-based header mapping is the primary contract.
- XLSX numeric JAN handling for this release:
  - scientific numeric cells: error
  - decimal numeric cells: error
  - ambiguous 12-digit numeric JAN: error
  - 13-digit numeric JAN: warning
- Final JAN normalization and validation remains in Rust.

## 9. Remaining Gaps

- Physical printer matrix and scan validation
- Local template catalog governance for backup/restore and multi-writer guidance
