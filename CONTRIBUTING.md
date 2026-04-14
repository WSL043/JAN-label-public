# CONTRIBUTING

## 基本ルール

- `main` へ直 push しない
- 1 PR 1 目的
- 印刷ロジック変更は fixture と docs を同じ PR で更新する
- printer adapter 変更時は測定結果と影響範囲を明記する

## ローカル確認

```powershell
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
pnpm install
pnpm format:check
pnpm lint
pnpm typecheck
pnpm fixture:validate
```

## 変更時の期待値

- `crates/domain`
  JAN / SKU / qty の業務ルールを守る
- `crates/render`
  golden fixture を更新する
- `crates/printer-adapters`
  `area:printer-adapters` ラベルが付くこと
- `docs`
  設計と運用の差分が説明されていること

