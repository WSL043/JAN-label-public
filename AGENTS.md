# AGENTS

Working notes for humans, local Codex, and cloud agents in this repository.

## 1. Read First

1. `docs/handoff/current-state.md`
2. `docs/todo/active.md`
3. `docs/architecture.md`
4. `docs/domain-model.md`
5. `docs/print-pipeline.md`
6. `docs/github-governance.md`
7. `docs/known-issues.md`
8. `docs/adr/`

## 2. Non-Negotiable Constraints

- Print core first, not UI first.
- JAN normalization and validation are authoritative in Rust.
- Barcode rendering must use Zint, not a custom barcode renderer.
- The first production output targets are `SVG` and `PDF`.
- Printer-specific behavior must stay inside `crates/printer-adapters`.
- Fixture, render, and docs updates should land together.
- `allowWithoutProof` is not allowed for release use.
- Legacy proofs may only be seeded as `pending`; approval must happen through review flow.
- `apps/desktop-shell` owns the final proof/print gate.
- `apps/desktop-shell` owns packaged and local template catalog resolution for dispatch.
- Live authoring JSON is not authoritative until it is saved into the desktop local template catalog.

## 3. Minimum Validation

```powershell
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
pnpm --filter @label/admin-web build
dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release
dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml
```

Notes:

- `cargo test --workspace` may intermittently hit `os error 5` on local Windows. Re-run once and confirm `desktop-shell` tests also pass before treating it as a code failure.
- If formatter checks start scanning a workspace-level `target-*` directory by mistake, remove that transient directory and re-run `pnpm format:check`.
- If `dotnet` is unavailable on the local host, use the GitHub Actions `windows-shell-native` job as the authoritative validation path for `apps/windows-shell`.

## 4. Change Rules

- Record new design decisions in `docs/adr/`.
- Update `docs/handoff/current-state.md` when the handoff state changes.
- Update `docs/todo/active.md` when priorities or next steps change.
- Record recurring traps in `docs/known-issues.md`.
- If printer adapter behavior changes, review fixtures and print docs.
- If `apps/admin-web`, `apps/desktop-shell`, or `packages/job-schema` contracts change, update both sides in the same pass.
- If `apps/windows-shell` changes, sync ADR, handoff, and validation notes in the same pass.
- If proof gate logic changes, update `docs/print-pipeline.md` and `docs/known-issues.md`.

## 5. Current Fronts

- `apps/admin-web`
- `apps/desktop-shell`
- `apps/windows-shell`
- `crates/render`
- `crates/print-agent`
- `crates/printer-adapters`
- `crates/audit-log`
- `packages/templates`
- `packages/job-schema`
- `packages/fixtures`

## 6. Lead / Sub-Agent Model

- Local Codex is the project lead for planning, integration, verification, docs sync, and GitHub sync.
- Sub-agents are execution workers for bounded slices such as UI, render, bridge, verification, and red-team review.
- Default sub-agent model is `gpt-5.3-codex-spark`.
- If `gpt-5.3-codex-spark` is unavailable or rate-limited, fall back to `gpt-5.3-codex`.
- Always bring sub-agent results back through the lead before merge, docs updates, or release decisions.
- Keep at least one adversarial review pass running on release-sensitive changes when practical.

## 7. GitHub Coordination

- Local Codex remains the source of truth for the working tree.
- GitHub-side Codex is used for PR review, CI triage, autofix, and maintenance follow-up.
- Sync state through `docs/todo/active.md` and `docs/handoff/current-state.md`.
- After landing a meaningful batch on the release branch, push it and post a PR sync note.

## 8. Release Direction

- Next release must keep PDF output stable.
- For PDF-only release, stable release criteria are limited to:
  - deterministic SVG/PDF render
  - proof review → approve/reject loop
  - print gated by approved proof lineage/subject
  - local audit persistence, export, and retention
  - local template catalog save-and-dispatch parity
- Template authoring must support save-to-catalog, preview, and proof/print dispatch parity.
- Excel / CSV import must remain usable without a strict external database schema.
- Proof review, audit search, export, and retention are part of the operator baseline.
- Physical printer measurement / scan validation (`T-031`) and GitHub secret setup (`T-030`) are deferred to a non-PDF-only milestone.
