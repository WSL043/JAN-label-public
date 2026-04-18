# 0016 Windows-Only Operator Stack And Admin-Web Retirement

- Status: Accepted
- Date: 2026-04-17

## Context

- The repository already moved the operator-shell direction to WPF in `apps/windows-shell`.
- The user requirement is not "web UI with desktop chrome". The requirement is a normal Windows desktop operator application in the style of traditional label software.
- Existing `apps/admin-web` flows are still useful for transition and backend parity, but they are not the target UX language.
- Rust remains the print-core authority and `apps/desktop-shell` remains the proof/print/catalog/audit authority for the current backend shape.

## Decision

- The operator application direction is Windows-only, not cross-platform.
- The target operator UX is package-backed WPF, not browser-style chrome.
- Prefer existing Windows desktop packages and standard interaction models over hand-built faux controls whenever licensing and compatibility are acceptable.
- `apps/admin-web` is a transitional workflow host only:
  - keep it usable while WPF parity is incomplete
  - do not treat it as the design reference
  - do not spend new shell-chrome effort there
- `apps/windows-shell` is the only intended long-term visible operator application.
- Deleting or heavily reducing `apps/admin-web` is allowed after native-shell parity replaces the required operator workflows. It must not happen before backend-safe parity exists.

## Consequences

- Future shell UX work should target `apps/windows-shell` first.
- Future docs should stop implying macOS / Linux / mobile UI targets for operator workflows.
- Repo cleanup can remove transitional web code after WPF-native parity is reached, but not as a blind rewrite that reopens proof/print authority risks.
- The practical near-term work is parity migration and cleanup, not a full repository reset.
