# domain-model

Target domain model for `v1.0.0`.

This file defines the intended single-stack `.NET / WPF` model. Use `docs/handoff/current-state.md` for what is shipping today and `docs/architecture.md` for current-versus-target ownership.

## 1. Product Boundary

- Product shape: one local Windows desktop application, `apps/windows-shell`
- Storage shape: `SQLite + local asset directories`
- Default root: `%LocalAppData%/JANLabel/`
- Override for development or tests: `JAN_LABEL_APP_ROOT`
- First release output boundary: deterministic `SVG` and `PDF`
- Not in `v1.0.0`: multi-host sync, server mode, browser operator shell, physical printer matrix, `allowWithoutProof`

Local runtime layout:

```text
%LocalAppData%/JANLabel/
  state/
    janlabel.db
    batch-queue-snapshot.json
  catalog/
    template-manifest.json
    *.json
  artifacts/
    proofs/
    prints/
  exports/
  backups/
    legacy-runtime/
```

## 2. Runtime Services

`apps/windows-shell-core` is the application-core boundary for the first single-stack release.

Required service seams:

- `ITemplateCatalogService`
- `IDesignerDocumentService`
- `IRenderService`
- `IBatchImportService`
- `IProofService`
- `IAuditService`
- `IDispatchService`
- `IPrintAdapter`
- `ILocalMigrationService`

Rule:

- WPF view-models project local domain state directly from these services.
- Companion DTOs are transitional only and must not become the long-term authority model.

## 3. Primary Aggregates

### Template Catalog Entry

Represents a packaged or local saved template version.

Core fields:

- `template_version`: immutable identifier such as `basic-50x30@v2`
- `label_name`
- `source`: `packaged` or `local`
- `manifest_path`
- `content_json`
- `is_default`
- `updated_at_utc`

Rules:

- `template_version` is immutable once published to catalog.
- Dispatch and proof work only from packaged or saved local catalog state.
- Draft-only designer state is never dispatch authority.

### Designer Document

Represents mutable WPF authoring state.

Core fields:

- `document_id`
- `template_version`
- `title`
- `content_json`
- `updated_at_utc`

Rules:

- Designer documents may diverge from saved catalog state.
- Saving to catalog is an explicit promotion step.
- Rollback must remain explicit by keeping versioned catalog state visible.

### Batch Import Session

Represents one CSV/XLSX ingest session.

Core fields:

- `session_id`
- `source_name`
- `source_kind`: `csv` or `xlsx`
- `status`
- `created_at_utc`
- `updated_at_utc`

### Batch Import Row

Represents one normalized row inside a batch session.

Core fields:

- `row_id`
- `session_id`
- `row_index`
- `sku`
- `jan_raw`
- `jan_normalized`
- `qty`
- `brand`
- `template_version`
- `status`
- `warning_json`
- `error_json`
- `payload_json`

Rules:

- Explicit alias mapping is allowed; silent column guessing is not.
- Unknown `template_version` blocks submit.
- Retry may only target eligible rows; submitted rows are not replayed blindly.

### Proof Record

Represents a generated proof artifact and its review state.

Core fields:

- `proof_job_id`
- `job_lineage_id`
- `subject_sku`
- `template_version`
- `artifact_path`
- `status`
- `requested_at_utc`
- `reviewed_at_utc`
- `review_actor`
- `notes_json`

Allowed statuses:

- `pending`
- `approved`
- `rejected`
- `superseded`

Rules:

- Legacy proof seed is allowed only as `pending`.
- Approval and rejection must happen through review flow.
- Proof artifact must exist, be non-empty, and be a valid PDF before it can unlock dispatch.

### Dispatch Record

Represents one print-intent execution after proof gating.

Core fields:

- `dispatch_job_id`
- `job_lineage_id`
- `template_version`
- `subject_sku`
- `artifact_path`
- `status`
- `submitted_at_utc`
- `completed_at_utc`
- `adapter_kind`
- `detail_json`

Rules:

- Dispatch requires approved proof lineage and subject match.
- `allowWithoutProof` is not valid for release use.
- Print output remains derived from the same normalized render scene as proof.

### Audit Event

Represents searchable operator history.

Core fields:

- `audit_event_id`
- `lane`
- `subject_key`
- `event_kind`
- `occurred_at_utc`
- `actor`
- `detail_json`

Rules:

- Audit history must support local search, export, retention, and restore review.
- Recovery and migration events are auditable first-class records, not hidden side effects.

### Backup Bundle

Represents one retention or restore artifact.

Core fields:

- `bundle_id`
- `file_name`
- `file_path`
- `created_at_utc`
- `size_bytes`
- `source`
- `detail_json`

### Migration Run

Represents one bootstrap or import attempt from the legacy runtime.

Core fields:

- `migration_id`
- `status`
- `started_at_utc`
- `completed_at_utc`
- `detail_json`

Rules:

- Migration must be idempotent.
- Existing new-runtime state is not overwritten silently.
- Failure must leave a recoverable state instead of partially resetting the workstation.

## 4. JAN Normalization And Validation

JAN remains a product guardrail, not a UI hint.

Rules:

- Empty input: reject
- Non-digit characters: reject
- `12` digits: compute checksum and normalize to `13`
- `13` digits: verify checksum, reject on mismatch
- Any other length: reject

Persistence rules:

- Keep the original cell or field as `jan_raw`
- Persist the authoritative value as `jan_normalized`
- Downstream proof, render, and dispatch logic consume the normalized value

Reference flow:

```text
if empty -> error
if non-digit -> error
if length == 12 -> append checksum
if length == 13 -> verify checksum
else -> error
```

## 5. Template Governance

Template identity and authoring rules:

- Identifier format: `template_id@version`
- Existing catalog versions are immutable
- Packaged and local entries may coexist, but the effective winner must be explicit
- Rollback path must stay visible
- Saving to catalog is the only promotion path from draft to proof-eligible state

Dispatch and review rules:

- Proof and dispatch record both `template_version`
- Local saved versions are valid only after they land in the catalog
- Live WPF edits are preview-only until saved

## 6. Batch Workflow Model

Import sources:

- CSV
- XLSX

Normalized input surface:

```text
parent_sku,sku,jan,qty,brand,template,printer_profile,enabled
```

Rules:

- Explicit alias handling happens inside import service
- Required values cannot be empty: `parent_sku`, `sku`, `brand`, `template`, `printer_profile`
- `qty` must be an integer greater than or equal to `1`
- `enabled` must be explicit boolean input
- Extra columns are not silently accepted into the authoritative payload
- Unknown template or blocked proof conditions must remain visible before submit

## 7. Render And Print Model

Rules:

- One `.NET` scene model feeds both `SVG` and `PDF`
- No HTML/CSS, WebView, or screenshot render path
- Barcode generation remains Zint-backed
- Printer-specific behavior stays behind `IPrintAdapter`

Artifact rules:

- Proof artifacts live in `artifacts/proofs/`
- Print artifacts live in `artifacts/prints/`
- Golden comparisons use deterministic `SVG` and `PDF`

## 8. Audit, Retention, And Restore

Required local capabilities:

- search
- export
- retention dry-run
- retention apply
- backup bundle listing
- restore review

Rules:

- Retention must be scoped and auditable
- Restore must validate before merge
- Backup bundle metadata must be indexed in SQLite even when the bundle file is filesystem-backed

## 9. Legacy Import Model

The first `.NET` bootstrap imports reusable local assets from the mixed-stack runtime one time.

Imported sources:

- local template overlay
- audit ledgers and backup bundles
- proof and print artifacts
- shared batch snapshot

Rules:

- import runs once per workstation root
- existing new-runtime files win over legacy copies
- imported audit backups remain attributable as `legacy-desktop-shell`
- migration result is recorded in `migration_runs`

## 10. Release Authority

`v1.0.0` is done only when this domain model is the real authority for:

- catalog save and version selection
- designer draft versus saved state
- batch import and submit staging
- proof review and lineage gate
- dispatch and PDF print
- audit export, retention, and restore review

Until then, treat hybrid ownership as a temporary execution detail, not the target model.
