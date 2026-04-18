# codex-automation

## Purpose

`T-012` makes the repository-side Codex automation operable on either GitHub-hosted runners or a repo-managed self-hosted runner, while keeping the same workflow files and same review / comment / CI / maintenance boundaries.

Local Codex remains the source of truth for the working tree. GitHub-side Codex remains bounded to:

- PR review
- `@codex` PR comment response
- failed-CI triage
- failed-CI autofix
- maintenance summaries

## Runner Selection

Codex workflows now read the repository variable `CODEX_RUNNER_LABELS_JSON`.

- If it is unset, Codex workflows default to `ubuntu-latest`.
- If it is set, it must be a JSON array of runner labels.

Example:

```json
["self-hosted", "linux", "x64", "codex"]
```

Recommended self-hosted baseline for one shared Codex runner:

- Git
- GitHub CLI (`gh`)
- Node.js LTS with Corepack enabled
- pnpm
- Rust stable

`codex-ci-autofix` needs the full Node + pnpm + Rust toolchain because it installs dependencies and runs narrow validation after the fix.

## Required Secrets And Tokens

- Repository secret: `OPENAI_API_KEY`
- Dispatch token for manual or webhook relay: `GH_TOKEN` or `GITHUB_TOKEN` with permission to call repository dispatch on this repo

`OPENAI_API_KEY` is still a separate release-adjacent configuration item and remains tracked under `T-030`.

## Replay / Webhook Event Types

The workflows keep their native GitHub triggers and also accept `repository_dispatch` replays with these event types:

| event type | workflow | required payload |
| --- | --- | --- |
| `codex-pr-review` | `Codex PR Review` | `{ "pr_number": 123 }` |
| `codex-pr-comment` | `Codex PR Comment` | `{ "pr_number": 123, "comment_body": "@codex ..." }` |
| `codex-ci-triage` | `Codex CI Triage` | `{ "workflow_run_id": 123456789 }` |
| `codex-ci-autofix` | `Codex CI Autofix` | `{ "workflow_run_id": 123456789 }` |
| `codex-maintenance` | `Codex Maintenance` | `{}` |

Same-repo safety rules still apply:

- PR review only runs for same-repo pull requests.
- PR comment response only runs for same-repo pull requests.
- CI autofix only runs for same-repo pull requests and does not recurse on `codex/ci-fix-pr-*` branches.
- Fork PRs do not gain write-capable automation through webhook replay.

## Dispatch Script

Use [dispatch-codex-event.mjs](../../scripts/dispatch-codex-event.mjs) to send a replay event through GitHub's repository dispatch API.

Examples:

```powershell
$env:GH_TOKEN = "ghp_xxx"
node scripts/dispatch-codex-event.mjs codex-pr-review --repo WSL043/JAN-label --payload '{"pr_number":33}'
node scripts/dispatch-codex-event.mjs codex-pr-comment --repo WSL043/JAN-label --payload '{"pr_number":33,"comment_body":"@codex summarize the latest diff"}'
node scripts/dispatch-codex-event.mjs codex-ci-triage --repo WSL043/JAN-label --payload '{"workflow_run_id":24557904646}'
node scripts/dispatch-codex-event.mjs codex-maintenance --repo WSL043/JAN-label
```

## Webhook Relay Pattern

Recommended relay shape:

1. GitHub emits the native webhook or Actions event.
2. An operator-run relay decides whether the event should be replayed.
3. The relay sends `repository_dispatch` with the normalized payload above.
4. The repository workflow runs on the configured Codex runner labels.

This keeps the repository workflow as the source of truth for validation, same-repo checks, and final side effects.

## Boundaries

- This does not replace release packaging or Windows validation runners.
- This does not remove the need for `OPENAI_API_KEY`.
- This does not move implementation authority away from local Codex.
- This does not change the PDF-only release boundary.
