# github-governance

## 1. Branch Protection / Ruleset

`main` should require:

- no direct push
- pull requests only
- required status checks
- stale review dismissal
- no force push
- no branch deletion

`merge queue` can be reconsidered after team size grows.

## Current Constraint

As of `2026-04-14`, the main remote is the private repository `WSL043/JAN-label`.

`gh api repos/WSL043/JAN-label/branches/main/protection` returned `HTTP 403`, confirming the current plan constraint: branch protection requires either a public repository or a higher GitHub plan.

Because of that, governance is split in two layers:

- repository layer:
  commit the workflow checks, PR templates, labels, and CODEOWNERS rules first
- remote layer:
  enable branch protection / rulesets once the repository plan allows it

## 2. Required Status Checks

Register these job names directly in the GitHub ruleset:

- `rust-format`
- `rust-lint`
- `rust-test`
- `golden-tests`
- `fixture-validation`
- `web-format-lint`
- `web-typecheck`
- `desktop-shell-windows`
- `docs-guard`

If `golden-tests` fails, the PR must not merge.

## 3. CODEOWNERS

Initial ownership stays lightweight, with `@WSL043` as the default owner while explicitly protecting the print-core paths:

- `crates/render/**`
- `crates/printer-adapters/**`
- `crates/print-agent/**`
- `packages/fixtures/**`
- `docs/**`

## 4. PR Rules

- one PR, one purpose
- printer adapter changes require the `area:printer-adapters` label
- fixture changes must update either golden tests or importer validation in the same PR
- if product code changes do not require docs updates, state the reason in the PR template

## 5. Labels

- `type:bug`
- `type:feature`
- `type:task`
- `type:printer-profile`
- `area:print-core`
- `area:admin-web`
- `area:printer-adapters`
- `area:docs`
- `priority:p0`
- `priority:p1`
- `priority:p2`
- `status:blocked`

## 6. CI Baseline

- Rust:
  `fmt`, `clippy`, `test`
- Web:
  `biome`, `typecheck`
- Fixtures:
  `node scripts/validate-fixtures.mjs`
- Docs:
  verify that product-code changes land with docs changes

## 6.5 Codex Automation

The repository baseline now includes all of these stages:

1. `openai/codex-action@v1` for same-repo PR review and `@codex` PR comment response
2. `workflow_run` triage for failed `CI` runs on PRs
3. scheduled / manual `Codex Maintenance` summaries
4. same-repo failed-`CI` autofix branches and draft PRs
5. maintenance summaries collected into a persistent GitHub issue ledger
6. configurable self-hosted runner and webhook-replay operations through `repository_dispatch`

Operational rules:

- Codex workflows keep their native GitHub triggers and also accept these replay event types:
  - `codex-pr-review`
  - `codex-pr-comment`
  - `codex-ci-triage`
  - `codex-ci-autofix`
  - `codex-maintenance`
- repository variable `CODEX_RUNNER_LABELS_JSON` selects runner labels for Codex workflows
- if that variable is unset, Codex workflows default to `ubuntu-latest`
- same-repo restrictions stay in force for PR review, PR comment response, and CI autofix
- GitHub-side Codex remains responsible for PR review, PR comment response, CI triage, CI autofix, and maintenance summaries
- local Codex remains responsible for implementation, local reproduction, release execution, and final integration quality
- persistent `codex-home` is optional for self-hosted runner setups, not a required repository contract
- `OPENAI_API_KEY` remains a GitHub Actions secret requirement

Use [docs/ops/codex-automation.md](ops/codex-automation.md) for the runner, token, and webhook relay contract.

## 7. Release Tagging

- tags use `vMAJOR.MINOR.PATCH`
- template-only changes still cut a patch release
- printer-profile compatibility breaks require at least a minor release
- production-printer adapter additions must list validated hardware in release notes
- pushing a `v*` tag triggers `.github/workflows/release.yml`
- the `Release` workflow builds `apps/desktop-shell` on `windows-latest` and attaches the installer artifact

## 8. Security And Audit

- security reports go through `SECURITY.md`
- dependency updates land by pull request
- changes that break audit-log semantics require explicit print-core review
