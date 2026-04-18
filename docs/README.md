# docs

## Start Here

1. `docs/handoff/current-state.md`
2. `docs/todo/active.md`
3. `docs/dotnet-convergence.md`
4. `docs/windows-rebuild-plan.md`
5. `docs/architecture.md`
6. `docs/domain-model.md`
7. `docs/print-pipeline.md`
8. `docs/known-issues.md`
9. `docs/release/v1.0.0-acceptance.md`
10. `docs/adr/README.md`

## Source Of Truth Rules

- `origin/main` is the repository source of truth for the latest shipped state and the next planned work.
- Feature-branch copies of handoff and todo docs can lag behind `main` or preserve historical branch-only context.
- If branch-local docs disagree with `origin/main`, trust `origin/main` unless you are intentionally reviving that branch and updating docs in the same pass.

Use these commands before relying on a non-`main` branch snapshot:

```powershell
git fetch --all --prune
git show origin/main:docs/handoff/current-state.md
git show origin/main:docs/todo/active.md
```

## Route By Question

- What is shipping now, and what is next:
  `docs/handoff/current-state.md`, `docs/todo/active.md`
- What owns proof, print, audit, and template authority:
  `docs/architecture.md`, `docs/print-pipeline.md`, `docs/adr/0008-dispatch-gate-owned-by-desktop-shell.md`
- What the `v1.0.0` release target actually means:
  `docs/handoff/current-state.md`, `docs/release/handoff.md`, `docs/release/v1.0.0-acceptance.md`, `docs/adr/0018-v1-release-target-and-local-dotnet-foundation.md`
- What the long-term operator app direction is:
  `docs/dotnet-convergence.md`, `docs/domain-model.md`, `docs/architecture.md`, `docs/adr/0011-windows-native-workstation-shell.md`, `docs/adr/0017-dotnet-only-convergence-and-legacy-stack-retirement.md`, `docs/adr/0018-v1-release-target-and-local-dotnet-foundation.md`
- Where the clean rebuild / cleanup rule is defined:
  `docs/dotnet-convergence.md`, `docs/windows-rebuild-plan.md`, `docs/handoff/current-state.md`, `docs/todo/active.md`
- Where the first extracted `.NET` rebuild support code lives:
  `apps/windows-shell-core/README.md`, `docs/dotnet-convergence.md`, `docs/windows-rebuild-plan.md`
- Where the first `v1.0.0` acceptance gates are tracked:
  `docs/release/v1.0.0-acceptance.md`, `docs/release/handoff.md`, `docs/todo/active.md`
- What is blocked, deferred, or operationally risky:
  `docs/todo/active.md`, `docs/known-issues.md`
- Why the Windows shell looks or behaves this way:
  `docs/handoff/current-state.md`, `docs/release/v0.3.0.md`, `docs/adr/0011-windows-native-workstation-shell.md`, `docs/adr/0013-single-installer-wpf-entrypoint.md`, `docs/adr/0014-wpf-shell-theme-and-chrome-reset.md`
- How public release packaging works:
  `docs/release/handoff.md`, `docs/handoff/current-state.md`, `docs/release/v0.3.0.md`
- How GitHub-side Codex automation is operated:
  `docs/github-governance.md`, `docs/ops/codex-automation.md`, `docs/adr/0015-codex-github-automation-runner-and-webhook-ops.md`

## Stable Vs Fast-Moving Docs

- Stable boundaries:
  `docs/architecture.md`, `docs/domain-model.md`, `docs/print-pipeline.md`, `docs/github-governance.md`, ADRs
- Fast-moving status:
  `docs/handoff/current-state.md`, `docs/todo/active.md`, `docs/known-issues.md`, `docs/ops/`
- Historical release detail:
  `docs/release/`

## Editing Rule

- When task status changes, update `docs/handoff/current-state.md` and `docs/todo/active.md` together.
- When shell direction, release flow, or proof-gate authority changes, update the relevant ADR or reference doc in the same pass.
