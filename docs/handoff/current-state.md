# current-state

- Updated: 2026-04-15
- Branch: `main`
- HEAD: `516dc1e`
- Remote: `origin/main`

## 1. 完了済み

- Rust workspace と pnpm workspace の土台
- `domain` / `importer` / `render` / `print-agent` などの最小 crate
- `crates/barcode` の Zint CLI adapter
  バイナリパス注入、終了コード / stderr を含むエラー、fake executable テスト
- `crates/render` の PDF 出力ルート
  deterministic な最小 PDF writer と golden fixture 比較
- `crates/importer` の行単位バリデーション
  canonical row ごとの cell error と JAN 正規化
- `apps/admin-web` のジョブ作成フォーム
  parent_sku / sku / jan / qty / brand 入力、template / printer profile 選択、`@label/job-schema` に沿った draft preview
- `crates/audit-log` / `crates/print-agent` の lineage / reprint モデル
  original job と reprint の系譜、parent job、reason を監査ログで表現
- GitHub Actions
  `CI`, `Pull Request Labeler`, `Sync Labels`, `Release`
- Codex event-driven workflow
  same-repo PR 自動レビューと `@codex` PR コメント応答
- issue forms, labels, PR template, CODEOWNERS
- Windows 開発環境の bootstrap スクリプト

## 2. 直近で通した確認

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

- `CI` run `24410728064`

## 3. 未完了

- PDF adapter 実装
- Windows spooler adapter 実装
- 実機プリンタの検証記録
- 開発環境 / CI への実 Zint バイナリ導入
- Codex による自動修正 PR / CI 修復 / schedule 巡回

対応する初期 GitHub issues:

- `#1` Zint CLI barcode adapter
- `#2` PDF output path in render crate
- `#3` row-level importer validation
- `#4` admin-web job creation form
- `#5` audit lineage and reprint history
- `#6` PDF printer adapter and proof flow

## 4. 次の安全な一手

1. `printer-adapters` に PDF adapter を追加する
2. 開発環境 / CI への実 Zint バイナリ導入方針を固める
3. Codex の自動修正 PR / CI 修復 / schedule 巡回を必要範囲で足す
4. 実機プリンタの測定記録を `docs/printer-matrix/` に残す

## 5. 触る時の注意

- UI 側に JAN 正規化を実装しない
- printer adapter に render 責務を混ぜない
- fixture 変更だけで済ませず docs を更新する
- 監査ログを単純な成功履歴に縮退させない

## 6. 現在の制約

- GitHub branch protection / ruleset は current plan 制約で未適用
- GitHub environments はまだ未作成
- release tag はまだ未発行
- Zint CLI adapter は実装済みだが、repo / CI に実バイナリはまだ組み込んでいない
