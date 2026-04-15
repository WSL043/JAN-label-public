# release-handoff

## 1. Release status

- `CI` green
- `docs/todo/active.md` matches release scope
- Local baseline is documented in `docs/known-issues.md` and operator playbook is updated for PDF-only release
- Latest published release is `v0.2.0`
- GitHub `Release` workflow run `24474516998` succeeded and published the Windows installer asset

### Current release basis

- Printer matrix baseline tasks remain in `docs/printer-matrix/`.
- `docs/printer-matrix` is monitored for future physical-print work; this milestone ships PDF proof/print path only.
- Latest release URL: `https://github.com/WSL043/JAN-label/releases/tag/v0.2.0`
- Latest Windows installer asset: `JAN-Label_0.2.0_windows_x64-setup.exe`

## 2. Tag policy

- `vMAJOR.MINOR.PATCH`
- Current release target is `v0.2.0`
- Use a minor release for the operator workstation redesign, audit restore, and release automation cut

### Historical tags

- `v0.1.0` used `Release` workflow preflight; output is release candidate.
- `v0.1.1` used bugfix hardening for proof/print gating and catalog logic.
- `v0.1.2` is the PDF-first patch release for local catalog parity, audit backup listing, and release-scope reclassification.
- `v0.1.3` is the desktop-shell UI reset patch release for the operator console.
- `v0.2.0` is the operator workstation release with audit restore and release artifact automation.

## 3. Preflight checks and tag workflow

```powershell
git fetch origin
git checkout main
git pull --ff-only
pnpm release:notes --version vNEXT
pnpm release:readiness --version vNEXT
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
pnpm --filter @label/admin-web build
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml
git tag vNEXT
git push origin vNEXT
```

## 4. Validation notes

- Windows builds can fail `link.exe` intermittently; rerun once.
- If local workspace has transient `target-*` directories, remove them before formatting checks.
- `main` must be passing CI and the local branch must not have unresolved blockers.
- Release workflow must run successfully with `desktop-shell` Windows installer output.
- `maintenance ledger` issue and CI summary should be attached to release notes.
- Release notes now draft to `docs/release/vNEXT.md`.
- Release readiness now drafts `artifacts/release-readiness.json` and `artifacts/release-readiness.md`.

## 5. Smoke check

- Confirm installer assets contain `JAN-Label_*_windows_*`.
- Confirm release asset hashes and signatures are recorded.
- Confirm known issues are reflected in `docs/known-issues.md` and reviewed before publish.
- Confirm printer profile loading path works on the test branch.

## 6. Commit and audit checkpoint

- Confirm `main` commit chain is documented and no non-reviewed hotfix commits are present.
- Ensure tagged commits include proof/print gate, template catalog, audit trail, and PDF pipeline changes in scope.

## 7. PDF-only release scope

- This release is PDF-first and limits gate acceptance to:
  - deterministic SVG/PDF generation path,
  - strict proof approval + lineage checks,
  - local audit persistence/export/retention,
  - local audit backup restore,
  - template catalog save/dispatch parity for packaged + local overlay.
- `T-030` (GitHub Actions `OPENAI_API_KEY`) is explicitly **non-blocking** for this release.
- `T-031` (physical printer matrix and scan validation) is explicitly **non-blocking** for this release.
- Audit backup bundle listing and restore are included in this release.
- Non-PDF items are moved to post-PDF milestones unless they become mandatory for correctness.

## 8. PDF-only operator runbook

### 8.1 scope

- Objective: enable production-adjacent release operations with virtual/standard PDF output only.
- Required path:
  - CSV/XLSX import -> proof generation -> approve/reject -> print dispatch -> audit export
- Out-of-scope for this run:
  - cloud secret-driven AI automation,
  - physical printer matrix/scan verification,
  - multi-host catalog sync.

### 8.2 operator checklist

- Pre-run
  - Verify `git status` clean and `main` sync is current.
  - Launch `desktop-shell` + `admin-web` integration path successfully.
  - Confirm PDF output target is selected; no physical printer-only checks are required.
- Data and templates
  - Import sample data and validate JAN/required fields.
  - Confirm live `template_version` exists in local catalog and can be selected.
  - If needed, save draft template to local catalog and re-open to confirm version visibility.
- Proof and approval
  - Generate proof from at least one sample row.
  - Confirm proof artifact is non-empty and parseable (`%PDF-` header).
  - Perform approve/reject actions and verify status updates are persisted.
- Dispatch and print
  - Test manual submit and batch submit/retry flows.
  - Verify print is blocked when:
    - proof is missing/expired,
    - lineage mismatch,
    - template/version mismatch,
    - queue state is not eligible.
  - Verify retry path only resends `ready` and `failed` rows.
- Audit and retention
  - Run audit search and export for release run window.
  - Run retention dry-run, create backup bundle, then apply trim only with operator signoff.
- Stop/restart and escalation
  - Pause dispatcher only when queue is drained.
  - After restart, re-check bridge health, catalog resolution, and audit write/read before accepting new jobs.
  - Escalate to issue tracker when blocking condition appears; attach logs and known-issues reference.

### 8.3 acceptance checklist

- `pnpm fixture:validate` passes
- `pnpm format:check` passes
- `pnpm lint` passes
- `pnpm typecheck` passes
- `pnpm --filter @label/admin-web build` passes
- `cargo fmt --all --check` passes
- `cargo clippy --workspace --all-targets -- -D warnings` passes
- `cargo test --workspace` and `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml` pass
- `CI` is green on `main`
- Operator checklists in 8.2 are completed and recorded
- Known issues list is current and any new risks are appended to `docs/known-issues.md`
