# ADR 0010: Local Template Overlay Catalog

- Date: 2026-04-15
- Status: accepted

## Context

The release branch now has a structured template editor and Rust preview, but operator-authored templates must also participate in real proof/print dispatch. Packaged templates alone are not enough once operators begin saving local variants and new template versions.

We need a way to:

- keep packaged templates as the immutable baseline
- allow local operator-authored templates on a desktop machine
- preserve `template_version` as the dispatch contract
- make catalog validation and actual render resolve through the same source of truth

## Decision

We introduce a local template overlay catalog owned by `apps/desktop-shell`.

- Packaged templates remain the built-in baseline in `packages/templates`.
- Local templates are saved under a desktop-local overlay directory.
- A local manifest overlays packaged entries by `version`.
- A local entry with the same `version` overrides the packaged entry.
- New local versions are appended to the effective catalog.
- Disabled local entries are hidden from resolution.
- `admin-web` can only make a template authoritative by saving it into this local catalog.
- `desktop-shell` passes the local manifest path into `print-agent` policy so proof/print render and validation use the same overlay source.

## Consequences

Positive:

- Saved operator-authored templates can be used for real proof/print dispatch without replacing packaged assets.
- `template_version` remains the stable contract across UI, bridge, and backend.
- Preview validation and dispatch resolution now share the same overlay-aware resolution path.
- Packaged templates remain available as a safe fallback baseline.

Tradeoffs:

- The local catalog is machine-local and not multi-host.
- There is no shared catalog service in this release.
- Backup/restore remains a filesystem procedure, not a shared service.
- Multi-writer coordination remains an operator rule, not an enforced lock service.

## Follow-Up

- `T-028f`: audit backup list / restore
- `T-041`: local template catalog governance hardening delivered desktop diagnostics, manifest repair guidance, backup/restore guidance, and explicit single-writer rules
- `T-042`: template library operator UX
- physical printer validation before release
