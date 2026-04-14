# release-handoff

## 1. 前提

- `CI` が green
- `docs/todo/active.md` の release 対象が完了
- 実機または PDF proof の確認が終わっている

## 2. タグ方針

- `vMAJOR.MINOR.PATCH`
- 初回は `v0.1.0`
- テンプレートや printer profile の後方互換破壊は minor 以上で扱う

## 3. 手順

```powershell
git fetch origin
git checkout main
git pull --ff-only
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
git tag v0.1.0
git push origin v0.1.0
```

## 4. smoke check

- release ノートが自動生成されたか
- 添付の差分が想定どおりか
- `docs/known-issues.md` に未解決高優先度が残っていないか
- printer profile 変更がある場合、検証機種がノートに明記されているか

## 5. ロールバック

- 誤タグなら tag を削除する前に原因を `docs/known-issues.md` に残す
- main の commit を巻き戻すのではなく、修正 commit と新タグで是正する

