# 0015 Codex GitHub Automation Runner And Webhook Ops

- Status: Accepted
- Date: 2026-04-17

## Context

- The repository already shipped GitHub-side Codex automation for PR review, `@codex` PR comments, CI triage, CI autofix, and maintenance summaries.
- `T-012` requires predictable local / remote coordination without forking the automation into a separate code path.
- Local Codex remains the source of truth for implementation, integration, and release decisions.
- GitHub-side Codex must stay bounded to review, triage, and maintenance work, even when runner topology changes.
- `OPENAI_API_KEY` is still external configuration and remains tracked separately.

## Decision

- Codex workflows keep their native GitHub triggers and also accept `repository_dispatch` replays.
- The repository supports these replay event types:
  - `codex-pr-review`
  - `codex-pr-comment`
  - `codex-ci-triage`
  - `codex-ci-autofix`
  - `codex-maintenance`
- Codex workflows select their runner through the repository variable `CODEX_RUNNER_LABELS_JSON`.
- If `CODEX_RUNNER_LABELS_JSON` is unset, Codex workflows default to `ubuntu-latest`.
- Same-repo restrictions remain in force for review, comment response, and autofix. Webhook replay does not widen write scope to fork pull requests.
- Replay payload validation stays inside repository-owned workflow logic and the repo-owned dispatch script.

## Consequences

- Self-hosted runner adoption is an operator / infra setting, not a workflow fork.
- Webhook relays and manual replays can target the same repository workflows without editing YAML for each incident.
- The repository keeps one operational boundary for Codex side effects, regardless of whether the runner is GitHub-hosted or self-hosted.
- Persistent `codex-home` is allowed but optional; it is not a repository contract.
- Missing `OPENAI_API_KEY` still causes Codex steps to skip explicitly.
