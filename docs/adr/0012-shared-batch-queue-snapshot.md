# 0012 Shared Batch Queue Snapshot

- Status: Accepted
- Date: 2026-04-17

## Context

`apps/windows-shell` needed a real `Batch Jobs` data source for `v0.3.0`, but the repository did not expose any backend-owned queue snapshot contract.

Before this decision:

- `apps/admin-web` held batch queue state only in React memory
- `apps/windows-shell` could not read real queued-row state through the companion path
- moving full batch mutation into WPF would have expanded release scope beyond read + safe ops

## Decision

Introduce a desktop-shell-owned local shared batch snapshot contract.

Rules:

- `apps/admin-web` may save, load, and clear the shared batch snapshot through `apps/desktop-shell`.
- `apps/windows-shell` may only read the shared batch snapshot through the companion path in `v0.3.0`.
- The shared snapshot is local-machine state, not multi-host coordination.
- Import, retry, and submit authority remain outside WPF for `v0.3.0`.
- Proof / print gate authority does not move out of `apps/desktop-shell`.

## Consequences

- `Batch Jobs` in WPF can now show real queued rows, submit state, blockers, and snapshot metadata.
- `apps/admin-web` and `apps/windows-shell` now share one queue-review state source instead of diverging local mocks.
- The native shell is materially closer to backend parity without expanding direct mutation scope.
- The batch snapshot remains operationally single-writer local state and must not be treated as a shared network queue.
