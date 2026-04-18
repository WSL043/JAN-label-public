# 0012 Public Packaging Mirror

## Status

Accepted

## Context

`JAN-label` remains a private repository during day-to-day development, but GitHub-hosted runner usage for private repositories is metered and can be blocked by account billing or budget limits. That creates a release risk for the Windows-native workstation direction because `v0.3.0` depends on GitHub-hosted Windows builds for the native shell installer.

Changing the primary repository between private and public is not acceptable because:

- repository contents become public immediately
- workflow logs and artifacts become public immediately
- any public forks remain public even if the original repository is made private again

The project still needs a way to use free standard GitHub-hosted runners for public repositories without exposing the private repository's git history or using the private repository as the release host.

## Decision

The project will keep the private repository as the source of truth and use a separate public packaging mirror repository for free GitHub-hosted CI and release packaging.

The mirror model is:

- private repository remains authoritative for planning, implementation, docs, and release readiness
- public mirror receives snapshot-only exports, not the private git history
- release tags for public packaging are created in the mirror repository, not by flipping the private repository's visibility
- Codex maintenance, review, comment, triage, and autofix workflows remain private-repository concerns and are skipped in the mirror

## Implementation Notes

- `scripts/publish-public-mirror.mjs` exports a clean snapshot using `git archive`, injects `.github/public-mirror-source.json`, initializes a fresh repository, and force-pushes the snapshot branch to the configured public mirror
- release tags can be pushed to the mirror snapshot with `--tag vX.Y.Z`, which lets the public mirror run the existing `Release` workflow on a public repository
- the mirror export intentionally creates fresh commits so private commit history, deleted files, and internal branches are not published
- the mirror repository name is tracked in `public-mirror.config.json`

## Consequences

Positive:

- Windows CI and release packaging can use free public-repository GitHub-hosted runners
- the private repository no longer needs to flip visibility for release packaging
- the release path avoids leaking private git history

Negative:

- mirror sync is an explicit release-prep step
- release notes should be prepared in the private repository before mirror tagging so the public release does not depend on private issue state
- releases are published from the mirror repository, so release URLs and assets live there instead of the private source repository
