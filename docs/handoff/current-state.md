# current-state

- Updated: 2026-04-14
- Branch: `main`
- HEAD: `bb85455`
- Remote: `origin/main`

## 1. 完了済み

- Rust workspace と pnpm workspace の土台
- `domain` / `importer` / `render` / `print-agent` などの最小 crate
- React 管理 UI の最小骨格
- GitHub Actions
  `CI`, `Pull Request Labeler`, `Sync Labels`, `Release`
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

- `CI` run `24396410009`

## 3. 未完了

- Zint 実接続
- PDF adapter 実装
- Windows spooler adapter 実装
- admin-web のジョブ作成 UI
- importer の行単位バリデーション
- audit lineage / reprint の詳細化
- 実機プリンタの検証記録

対応する初期 GitHub issues:

- `#1` Zint CLI barcode adapter
- `#2` PDF output path in render crate
- `#3` row-level importer validation
- `#4` admin-web job creation form
- `#5` audit lineage and reprint history
- `#6` PDF printer adapter and proof flow

## 4. 次の安全な一手

1. `crates/barcode` で Zint CLI adapter を実装する
2. `crates/render` で PDF 出力ルートを追加する
3. `packages/fixtures` に barcode / pdf の fixture を足す
4. `apps/admin-web` にジョブ作成フォームを作る

## 5. 触る時の注意

- UI 側に JAN 正規化を実装しない
- printer adapter に render 責務を混ぜない
- fixture 変更だけで済ませず docs を更新する
- 監査ログを単純な成功履歴に縮退させない

## 6. 現在の制約

- GitHub branch protection / ruleset は current plan 制約で未適用
- GitHub environments はまだ未作成
- release tag はまだ未発行
- Zint は repo / CI にまだ組み込んでいない
