# 0017 - .NET-only convergence and legacy-stack retirement

- Status: Accepted
- Date: 2026-04-17

## Context

The repository currently ships through a mixed stack:

- `apps/windows-shell` for the visible Windows shell
- `apps/admin-web` for transitional operator workflows
- `apps/desktop-shell` for bridge and authority behavior
- Rust crates for validation, render, proof/print gate, audit, and related logic

That shape reduced short-term delivery risk, but it increases long-term maintenance cost:

- multiple UI and bridge stacks
- duplicated contracts and workflow logic
- repeated cross-language synchronization
- risk of carrying weak legacy implementation details into the next shell

The product direction is now explicitly Windows-only, and the desired maintenance model is the lowest practical long-term cost rather than preserving the current mixed architecture.

## Decision

Converge to a `.NET / WPF` single-stack operator product.

- `apps/windows-shell` becomes the long-term operator application.
- New strategic workflow development happens in `.NET / WPF`.
- Legacy `apps/admin-web`, `apps/desktop-shell`, and mixed-stack bridge surfaces are replacement targets, not protected architecture.
- Old behavior should be re-specified and re-implemented where necessary, not ported mechanically.
- After a `.NET` replacement is validated for a workflow, the superseded legacy slice should be deleted.

## Consequences

Positive:

- simpler long-term architecture
- fewer duplicated contracts
- less cross-stack drift
- Windows UX can use normal desktop controls and patterns directly

Costs:

- short-term rewrite effort for workflows that currently still depend on the mixed stack
- temporary need to keep current and target implementations visible during staged replacement
- documentation and validation must clearly distinguish shipped baseline from target convergence path

## Notes

- This decision tightens and supersedes the transitional direction implied by `0016-windows-only-operator-stack-and-admin-web-retirement.md`.
- The existing dual-repository operating model remains in place for cost and GitHub workflow reasons; it does not change this product architecture decision.
- Initial implementation starts by moving template catalog resolution for `Home` and `Designer` into `apps/windows-shell` instead of continuing to treat companion snapshot data as the only source for that board.
