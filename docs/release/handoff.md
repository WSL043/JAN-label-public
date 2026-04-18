# release-handoff

## 1. Release status

- Use `docs/handoff/current-state.md` as the release routing snapshot.
- `docs/todo/active.md` should match the current release scope.
- Local baseline and traps are documented in `docs/known-issues.md`.
- Latest published release is `v0.3.0`.
- `v0.3.1` is historical preview context only.
- Next formal release target is `v1.0.0`.
- Authoritative tagged packaging currently runs through `WSL043/JAN-label-public` when private-repo GitHub-hosted Actions are billing-blocked before job start.

### Current release basis

- Printer matrix baseline tasks remain in `docs/printer-matrix/`.
- `docs/printer-matrix` is monitored for future physical-print work; this milestone ships PDF proof/print path only.
- Current convergence batch is `T-052` `M0 + M1` plus early `M2/M3` Designer/catalog slices, the first local proof-review slice, the next local audit-mirror slice, and the first local proof-create slice: native `Save to Catalog`, native open-from-catalog, and native draft preview in the Designer lane, including native preview-refresh from the current Designer surface plus the first hardening pass for catalog path safety, template identity retention, authoring-expression preservation, 12 px/mm geometry round-trip, refresh selection retention, debounced preview refresh, a single local render-service route for both template-open preview and explicit preview refresh, a shared millimeter-based scene model that now feeds both draft `SVG` and draft `PDF` artifacts for the Designer lane, a native SQLite proof ledger that now persists WPF approve/reject decisions locally, a native SQLite audit mirror that now syncs live dispatch/audit rows plus bundle metadata before WPF audit export plus the visible `History` / `Print Console` audit rows read back from local SQLite, and a native `Run Proof` path that now writes the proof artifact plus matching proof/dispatch ledger rows locally.
- Current repository state is still hybrid, but release routing and domain docs now point only at the `v1.0.0` single-stack target.
- `docs/release/v1.0.0-acceptance.md` is the release acceptance matrix.
- Latest release URL: `https://github.com/WSL043/JAN-label-public/releases/tag/v0.3.0`
- Latest desktop-shell installer asset: `JAN-Label_0.3.0_windows_x64-setup.exe`
- Latest native-shell installer asset: `JAN-Label_windows-native-shell_v0.3.0.exe`
- Latest native-shell checksum asset: `JAN-Label_windows-native-shell_v0.3.0.exe.sha256`

## 2. Tag policy

- `vMAJOR.MINOR.PATCH`
- Next formal tag target is `v1.0.0`
- Do not treat `v0.3.1` as a future release target; it is historical preview material only.
- Do not tag `v1.0.0` until `docs/release/v1.0.0-acceptance.md` is complete and legacy release/runtime dependencies have been retired from the operator path.

### Historical tags

- `v0.1.0` used `Release` workflow preflight; output is release candidate.
- `v0.1.1` used bugfix hardening for proof/print gating and catalog logic.
- `v0.1.2` is the PDF-first patch release for local catalog parity, audit backup listing, and release-scope reclassification.
- `v0.1.3` is the desktop-shell UI reset patch release for the operator console.
- `v0.2.0` is the operator workstation release with audit restore and release artifact automation.
- `v0.3.0` is the Windows-native workstation release with packaged-shell migration and the single visible native Windows app path.

## 3. Current convergence preflight

Use this preflight while `T-052` is still carrying hybrid runtime slices in-tree.

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
dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release
dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml
pnpm mirror:release -- --ref main --tag vNEXT
```

### Public mirror release note

- formal Windows packaging now publishes from the public packaging mirror, not by flipping the private repository visibility
- `pnpm mirror:release -- --ref main --tag vNEXT` exports a snapshot-only tree to the configured public mirror and pushes the release tag there
- the mirror repository then runs the existing `CI` and `Release` workflows on free public-repository GitHub-hosted runners
- prepare `docs/release/vNEXT.md` in the private repository before pushing the mirror release tag

## 4. Final `v1.0.0` gate

The final `v1.0.0` gate is narrower than the current convergence preflight.

Required final release conditions:

- `docs/release/v1.0.0-acceptance.md` is complete.
- `apps/windows-shell` is the only shipped operator application.
- Release packaging no longer depends on `desktop-shell.exe` or companion checks.
- Release workflows no longer require `cargo build companion` or `desktop-shell version check`.
- `.NET` build, test, render golden, installer build, and installer smoke are the authoritative release gates.

## 5. Validation notes

- Windows builds can fail `link.exe` intermittently; rerun once.
- If local workspace has transient `target-*` directories, remove them before formatting checks.
- `main` must be passing CI and the local branch must not have unresolved blockers.
- During convergence, local `dotnet` may be unavailable; in that case `windows-shell-native` on GitHub Actions is the authoritative Windows validation path.
- Final `v1.0.0` release workflow must run successfully with the WPF installer output only.
- Release workflow must keep the GitHub Release in draft state until installer assets and checksum upload successfully.
- `maintenance ledger` issue and CI summary should be attached to release notes.
- Release notes now draft to `docs/release/vNEXT.md`.
- Release readiness now drafts `artifacts/release-readiness.json` and `artifacts/release-readiness.md`.
- public mirror releases use snapshot exports, so they must not depend on private git history being present in the mirror repository.

## 6. Smoke check

- Confirm installer assets contain the single WPF `JanLabel` package and checksum.
- Confirm release asset hashes and signatures are recorded.
- Confirm known issues are reflected in `docs/known-issues.md` and reviewed before publish.
- Confirm local runtime bootstrap succeeds from an installed path, including SQLite creation and migration-state tracking.

## 7. Commit and audit checkpoint

- Confirm `main` commit chain is documented and no non-reviewed hotfix commits are present.
- Ensure tagged commits include proof/print gate, template catalog, audit trail, and PDF pipeline changes in scope.

## 8. `v1.0.0` PDF-only scope

- This release is PDF-first and limits gate acceptance to:
  - deterministic SVG/PDF generation path,
  - strict proof approval + lineage checks,
  - local audit persistence/export/retention,
  - local audit backup restore,
  - template catalog save/dispatch parity for packaged + local overlay,
  - CSV/XLSX import.
- `T-030` (GitHub Actions `OPENAI_API_KEY`) is explicitly **non-blocking** for this release.
- `T-031` (physical printer matrix and scan validation) is explicitly **non-blocking** for this release.
- Audit backup bundle listing and restore are included in this release.
- Non-PDF items are moved to post-PDF milestones unless they become mandatory for correctness.

## 9. Operator runbook

### 9.1 scope

- Objective: enable production-adjacent release operations with virtual/standard PDF output only.
- Required path:
  - CSV/XLSX import -> proof generation -> approve/reject -> print dispatch -> audit export
- Out-of-scope for this run:
  - cloud secret-driven AI automation,
  - physical printer matrix/scan verification,
  - multi-host catalog sync.

### 9.2 operator checklist

- Pre-run
  - Verify `git status` clean and `main` sync is current.
  - Use the current `T-052` authoritative path for each workflow and do not mix legacy and rebuilt paths blindly.
  - Confirm PDF output target is selected; no physical printer-only checks are required.
- Data and templates
  - Import sample data and validate JAN/required fields.
  - Confirm selected `template_version` exists in the local catalog and can be selected.
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

### 9.3 acceptance checklist

- `pnpm fixture:validate` passes
- `pnpm format:check` passes
- `pnpm lint` passes
- `pnpm typecheck` passes
- `pnpm --filter @label/admin-web build` passes while legacy slices still exist
- `dotnet build apps/windows-shell/JanLabel.WindowsShell.csproj -c Release` passes
- `dotnet test apps/windows-shell-tests/JanLabel.WindowsShell.Tests.csproj -c Release` passes
- `cargo fmt --all --check`, `cargo clippy --workspace --all-targets -- -D warnings`, `cargo test --workspace`, and `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml` pass while hybrid release dependencies still exist
- `CI` is green on `main`
- `docs/release/v1.0.0-acceptance.md` is complete
- Operator checklists in 9.2 are completed and recorded
- Known issues list is current and any new risks are appended to `docs/known-issues.md`
