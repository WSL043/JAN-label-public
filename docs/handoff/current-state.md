# current-state

- Updated: 2026-04-15
- Branch: `main`
- HEAD: `6e6ec4e`
- Remote: `origin/main`

## 1. 完了済み

- Rust workspace と pnpm workspace の土台
- `domain` / `importer` / `render` / `print-agent` などの最小 crate
- React 管理 UI の最小骨格
- GitHub Actions
  `CI`, `Pull Request Labeler`, `Sync Labels`, `Release`
- issue forms, labels, PR template, CODEOWNERS
- Windows 開発環境の bootstrap スクリプト
- `crates/barcode` の Zint CLI adapter と fake executable テスト
- `crates/render` の PDF 出力ルートと golden fixture
- `crates/importer` の行単位バリデーション
- `apps/admin-web` のジョブ作成フォーム
- `crates/audit-log` / `crates/print-agent` の lineage / reprint モデル
- `crates/printer-adapters` の PDF proof adapter と Windows spooler skeleton
- GitHub 上の Codex 連携
  `Codex PR Review`, `Codex PR Comment`, `Codex CI Triage`, `Codex Maintenance`

## 2. 直近で使っている確認セット

```powershell
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
```

GitHub Actions の最新成功 run:

- `CI` run `24411813836` on `main`
- ローカル Windows で `link.exe` がない場合、`cargo test --workspace` の最終判定は CI を正とする

GitHub 側の整理:

- rollup PR `#11` を 2026-04-14 に `main` へ merge 済み
- superseded された stacked draft PR `#8`, `#9`, `#10` は close 済み

## 3. 未完了

- `docs/printer-matrix/` に最低 1 機種分の実測を記録
- 初回 `v0.1.0` tag / release を発行
- phase 3 の Codex 自動化
  CI failure の自動修正 PR, self-hosted runner / webhook

## 4. 次の安全な一手

1. `docs/printer-matrix/template.md` を複製し、実機計測を 1 件記録する
2. 必要なら `Codex Maintenance` を `workflow_dispatch` で実行し、release blocker を再確認する
3. `main` の green CI と release handoff 条件を確認して `v0.1.0` tag を切る

## 5. 触る時の注意

- UI 側に JAN 正規化を実装しない
- printer adapter に render 責務を混ぜない
- fixture 変更だけで済ませず docs を更新する
- 監査ログを単純な成功履歴に縮退させない
- 実機測定値が入るまでは PDF proof を release 判定の補助に留める

## 6. 現在の制約

- GitHub branch protection / ruleset は current plan 制約で未適用
- GitHub environments はまだ未作成
- Zint は repo / CI にまだ組み込んでいない
- 一部のローカル Windows 環境では `link.exe` 不在により `cargo test --workspace` が失敗する
