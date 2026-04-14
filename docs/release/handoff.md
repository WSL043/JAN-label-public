# release-handoff

## 1. 前提

- `CI` が green
- `docs/todo/active.md` の release 対象が完了
- `docs/printer-matrix/` に最低 1 件の実測記録がある
- 実機または PDF proof の確認が終わっている

初回 `v0.1.0` では、`docs/printer-matrix/` の 1 件目は PDF proof baseline でもよい。  
ただし、これは物理プリンタ検証の代替完了ではなく、release 後も別途記録を追加する。

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

補足:

- ローカル Windows で `link.exe` がなく `cargo test --workspace` が失敗する場合は、そのまま green 扱いにしない
- その場合でも `cargo check --workspace --tests` は補助確認として回し、最終判定は `main` 上の GitHub Actions `CI` success を使う
- blocker が曖昧なら `Codex Maintenance` を `workflow_dispatch` で実行し、job summary を release 前確認に使う
- maintenance ledger issue がある場合は、最新コメントも release 前確認に含める
- `Release` workflow は tag push 後に GitHub-hosted `windows-latest` で `apps/desktop-shell` を build し、NSIS installer を release asset に添付する
- ローカル Windows に `link.exe` がない場合でも、release 前確認は `desktop-shell-windows` CI success を正としてよい

## 4. smoke check

- release ノートが自動生成されたか
- `JAN-Label_*_windows_*` の installer asset が添付されたか
- 添付の差分が想定どおりか
- `docs/known-issues.md` に未解決高優先度が残っていないか
- printer profile 変更がある場合、検証機種がノートに明記されているか

## 5. ロールバック

- 誤タグなら tag を削除する前に原因を `docs/known-issues.md` に残す
- main の commit を巻き戻すのではなく、修正 commit と新タグで是正する
