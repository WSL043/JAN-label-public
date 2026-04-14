# github-governance

## 1. ブランチ保護 / ruleset

`main` に対して次を必須にします。

- 直 push 禁止
- PR 経由のみ
- required status checks 必須
- stale review dismiss 有効
- merge queue はチーム規模拡大後に検討
- force push 禁止
- branch deletion 禁止

## 現在の制約

2026-04-14 時点で、remote は `WSL043/JAN-label` の private repository です。  
`gh api repos/WSL043/JAN-label/branches/main/protection` は `HTTP 403` を返し、
「GitHub Pro へアップグレードするか public repository にする必要がある」という制約が確認できました。

そのため、このリポジトリでは次の 2 段階で進めます。

- repo 側
  `.github/workflows/ci.yml`、`CODEOWNERS`、PR template、issue forms を先に整備する
- remote 側
  GitHub Pro / Team 以上へ切り替えた後に branch protection / ruleset を有効化する

## 2. required status checks

GitHub ruleset には次の job 名をそのまま登録します。

- `rust-format`
- `rust-lint`
- `rust-test`
- `golden-tests`
- `fixture-validation`
- `web-format-lint`
- `web-typecheck`
- `desktop-shell-windows`
- `docs-guard`

`golden-tests` が失敗した PR は merge 不可です。

## 3. CODEOWNERS

初期は少人数前提で `@WSL043` をデフォルト owner にしつつ、印刷中核パスを明示的に強化します。

- `crates/render/**`
- `crates/printer-adapters/**`
- `crates/print-agent/**`
- `packages/fixtures/**`
- `docs/**`

## 4. PR ルール

- 1 PR 1 目的
- printer adapter 変更時は `area:printer-adapters` ラベル必須
- fixture 変更時は golden test か importer validation のどちらかを同時更新
- コード変更で docs 更新が不要なら PR template に理由を書く

## 5. labels 設計

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

## 6. CI 基本設計

- Rust
  `fmt`, `clippy`, `test`
- Web
  `biome`, `typecheck`
- Fixtures
  `node scripts/validate-fixtures.mjs`
- Docs
  プロダクトコード変更時に docs 同時更新があるか確認

## 6.5 Codex automation

- GitHub 上の Codex 連携は event-driven を基本とする
- 第 1 段階では `openai/codex-action@v1` を使い、same-repo PR の自動レビューと `@codex` PR コメント応答を有効にする
- 第 2 段階では `workflow_run` で `CI` failure を拾い、失敗ログを添えて Codex が PR に triage を返す
- 第 3 段階では `schedule` / `workflow_dispatch` の `Codex Maintenance` で unresolved CI と release blocker を要約する
- 第 4 段階では same-repo PR の failed `CI` から `Codex CI Autofix` が fix branch と draft PR を起こす
- 第 5 段階では `Codex Maintenance` の結果を GitHub issue に集約し、release 前確認の恒久ログにする
- GitHub 上の Codex は bug-finding / debugging の一次対応を担当する
  PR review、`@codex` コメント応答、CI triage、CI autofix、maintenance summary をここに寄せる
- ローカル Codex は実装、手元の再現、tag / release 実行を担当する
- `Codex PR Review` と `Codex PR Comment` は `refs/pull/*/merge` に依存せず、PR の head SHA と base/head branch fetch で動かす
- `OPENAI_API_KEY` は GitHub Actions secret として管理する
- GitHub-hosted Linux runner では `sandbox: read-only` を基本にし、レビュー / triage を優先する
- 自動修正 PR は same-repo PR のみに限定し、fork PR は triage comment のみに留める
- self-hosted runner / webhook 化は次段階で追加する
- self-hosted runner を使う場合は persistent な `codex-home` を検討してよいが、最初から必須にはしない

## 7. release tagging

- tag 形式は `vMAJOR.MINOR.PATCH`
- テンプレート変更だけでも patch を切る
- printer profile の互換性破壊は minor 以上で扱う
- 本番プリンタ向け adapter 追加は release notes に検証機種を明記する
- `v*` タグ push 時は `.github/workflows/release.yml` が GitHub Release を自動作成する
- `Release` workflow は `apps/desktop-shell` を `windows-latest` で build し、Windows installer を release asset に添付する

## 8. セキュリティと監査

- セキュリティ報告先は `SECURITY.md`
- 依存更新は PR ベースで実施
- 監査ログ仕様を壊す変更は `print-core` owner の review を必須にする
