# 0018 - v1 release target and local .NET foundation

- Status: Accepted
- Date: 2026-04-18

## Context

The repository had already chosen Windows-only `.NET / WPF` convergence in ADR `0017`, but the release route and the application-core model were still split:

- some docs still described `v0.3.1` as the next formal target
- `docs/domain-model.md` was unusable because of encoding damage
- `apps/windows-shell` had started reading native catalog state, but there was no single documented local-runtime foundation for storage, migration, and service boundaries

Without a concrete application-core baseline, the project risk was carrying hybrid DTO and companion assumptions deeper into the WPF codebase even after the release target changed.

## Decision

Lock the next formal release target to `v1.0.0` and establish the first local `.NET` runtime foundation now.

Release target rules:

- `v0.3.1` becomes a historical preview line only
- the only formal next release target is `v1.0.0`
- `v1.0.0` means Windows-only, PDF-only, and `apps/windows-shell` as the only shipped operator app

Foundation rules:

- `%LocalAppData%/JANLabel/` is the default workstation root, with `JAN_LABEL_APP_ROOT` as the local override
- `state/janlabel.db` is the SQLite authority for local state
- `catalog/`, `artifacts/proofs/`, `artifacts/prints/`, `exports/`, and `backups/` are the standard local asset directories
- `apps/windows-shell-core` owns the bootstrap for path resolution, SQLite schema creation, migration tracking, and first-pass application service contracts
- startup initializes this local runtime before the shell begins normal operation

Migration rules:

- the first bootstrap imports reusable overlay, audit, artifact, and batch-snapshot data from the legacy runtime
- migration is one-time and idempotent
- imported content never silently overwrites new-runtime state

Validation rule:

- when local `dotnet` is unavailable, the authoritative validation path is GitHub Actions job `windows-shell-native`

## Consequences

Positive:

- one release route is now documented instead of preview-versus-future ambiguity
- WPF work can target a defined local application-core contract
- local bootstrap and migration can be tested independently of shell chrome

Costs:

- the repository still has to carry hybrid runtime paths until later `T-052` batches delete them
- docs must clearly distinguish shipped baseline from target architecture until `M6`
- startup failures in local storage bootstrap are now explicit instead of being deferred deeper into workflow execution

## Notes

- This ADR extends ADR `0017` by turning the convergence direction into the only formal release route.
- Historical ADRs about preview or hybrid behavior remain valid as record, but they no longer define the forward release target.
- Early `M2` native Designer/catalog work must preserve template expressions, honor the 12 px/mm canvas geometry scale, keep local catalog writes inside the resolved overlay root, and label draft preview semantics clearly until proof authority is rebuilt natively.
- Early `M2/M3` draft preview work should route through one local render-service path and one shared draft-binding baseline so template-open preview and explicit preview refresh do not drift on semantics such as `status`, `proof_mode`, or sample record values.
- The first proof-side native authority slices may synchronize live proof rows into SQLite, persist WPF approve/reject decisions locally, and create fresh pending proof artifacts plus matching proof/dispatch ledger rows locally before full native audit restore/dispatch replacement exists, but docs must continue to label proof preview authority, retention apply, restore, and dispatch as hybrid until those lanes are rebuilt natively.
- The first audit-side native slice may synchronize live dispatch/audit rows plus backup-bundle metadata into SQLite, move WPF audit export onto that local mirror, and project visible `History` / `Print Console` audit rows plus bundle inventory back out of that local mirror before native audit restore or dispatch parity exists, but docs must continue to label retention apply, restore, and dispatch as hybrid until those lanes are rebuilt natively.
