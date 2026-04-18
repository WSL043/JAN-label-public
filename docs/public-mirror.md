# public-mirror

## Purpose

`JAN-label` now uses a separate public packaging mirror so Windows CI and release packaging can run on free GitHub-hosted runners for public repositories, while the main repository stays private.

The mirror is packaging-only. The private repository remains the source of truth for:

- implementation
- handoff and todo state
- release readiness decisions
- Codex maintenance and CI triage

## Current mirror target

- repository: `WSL043/JAN-label-public`
- branch: `main`

Configuration lives in [public-mirror.config.json](/C:/Users/Omo/Downloads/JAN-label/public-mirror.config.json).

## Safety model

Do not make the private repository public for packaging. Public mirror export is safer because it:

- exports a tree snapshot, not the private git history
- creates fresh mirror commits
- can push tags to the mirror without exposing private branches or deleted history

The export script writes `.github/public-mirror-source.json` into the mirror snapshot so public builds can still show which private commit produced the mirror state.

## Commands

Sync the current committed `HEAD` to the public mirror branch:

```powershell
pnpm mirror:sync
```

Sync an explicit ref:

```powershell
pnpm mirror:sync -- --ref main
```

Sync and create a public release tag in the mirror:

```powershell
pnpm mirror:release -- --ref main --tag v0.3.0
```

Override the mirror target if needed:

```powershell
pnpm mirror:sync -- --repo owner/repo --branch main
```

## Release flow

1. Keep development in the private repository.
2. Prepare release notes in the private repository first.

```powershell
pnpm release:notes --version vNEXT
pnpm release:readiness --version vNEXT
```

3. Commit the release notes and readiness-related docs in the private repository if they changed.
4. Export the release-ready snapshot to the public mirror:

```powershell
pnpm mirror:release -- --ref main --tag vNEXT
```

5. Let the public mirror run its `CI` and `Release` workflows.
6. Publish or validate the release assets from the public mirror repository.

## Workflow behavior in the mirror

- `CI` runs normally in the public mirror
- `Release` runs normally in the public mirror
- Codex review/comment/maintenance/autofix workflows are skipped in the public mirror because they are private-repository concerns

## Notes

- `scripts/publish-public-mirror.mjs` requires `gh auth status` to succeed
- the script requires a clean working tree when exporting `HEAD`
- if the public mirror repository does not exist, the script can create it from the checked-in config
- any code pushed to the public mirror becomes public immediately, so do not sync branches that are not intended for public release packaging
