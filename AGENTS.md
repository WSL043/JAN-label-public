# AGENTS

このリポジトリで作業する人間と Codex / クラウドエージェント向けの実務メモです。

## 1. 最初に読む順番

1. `docs/handoff/current-state.md`
2. `docs/todo/active.md`
3. `docs/architecture.md`
4. `docs/domain-model.md`
5. `docs/print-pipeline.md`
6. `docs/github-governance.md`
7. `docs/known-issues.md`
8. `docs/adr/`

## 2. 変えてはいけない前提

- UI 先行ではなく印刷コア先行
- JAN の正規化と検証は Rust 側を正とする
- バーコード描画は自前主導にしない。Zint を前提にする
- 最初の正規出力は `SVG/PDF`
- printer 差異は `crates/printer-adapters` に閉じ込める
- fixture / render / docs は同時更新を基本とする

## 3. 変更時の最低確認

```powershell
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
```

## 4. 作業ルール

- 新しい判断は `docs/adr/` に残す
- 引き継ぎが必要な状態変更は `docs/handoff/current-state.md` を更新する
- 次に着手すべき順番は `docs/todo/active.md` を更新する
- 再発しそうな罠は `docs/known-issues.md` に残す
- printer adapter に手を入れたら docs と fixtures を確認する

## 5. 今の主戦場

- `crates/barcode`
- `crates/render`
- `crates/printer-adapters`
- `apps/admin-web`
- `packages/fixtures`

