# AGENTS

このリポジトリで作業する人間と Codex / クラウド Agent 向けの実務メモです。

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
- proof を bypass するために `allowWithoutProof` を使わない
- legacy proof の移行は pending seed まで。直接 approved で投入しない

## 3. 変更時の最低確認

```powershell
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
pnpm --filter @label/admin-web build
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml
```

補足:
- `cargo test --workspace` はローカル Windows で `print-agent` 実行時に `os error 5` が出ることがある。既知事項は `docs/known-issues.md` を参照。
- 一時的に `target-*` ディレクトリを作った場合、`pnpm format:check` 前に削除する。Biome が生成物まで走査する。

## 4. 作業ルール

- 新しい判断は `docs/adr/` に残す
- 引き継ぎが必要な状態変更は `docs/handoff/current-state.md` を更新する
- 次に着手すべき順番は `docs/todo/active.md` を更新する
- 再発しそうな罠は `docs/known-issues.md` に残す
- printer adapter に手を入れたら docs と fixtures を確認する
- `apps/admin-web` の submit / import / proof inbox を触ったら `apps/desktop-shell` と `packages/job-schema` の契約差分を確認する
- `apps/desktop-shell` の proof / audit / bridge warning を触ったら `docs/print-pipeline.md` と `docs/known-issues.md` を同期する
- proof gate を変えた場合は `sourceProofJobId` の検証条件を docs に明記する

## 5. 今の主戦場

- `apps/admin-web`
- `apps/desktop-shell`
- `crates/render`
- `crates/printer-adapters`
- `crates/audit-log`
- `packages/templates`
- `packages/job-schema`
- `packages/fixtures`

## 6. 主任 Codex と Sub-Agent の運用

- 主任はローカル Codex。タスク分解、優先順位、最終統合、検証、docs 更新、GitHub 同期を担当する
- Sub-Agent は部門別に並列で使う。役割は探索、攻撃的レビュー、限定実装、テスト観点整理に分ける
- Sub-Agent の第一候補モデルは `gpt-5.3-codex-spark`
- `gpt-5.3-codex-spark` が上限または利用不可なら `gpt-5.3-codex` にフォールバックする
- Sub-Agent の成果は必ず主任がレビューし、そのまま鵜呑みにしない
- Sub-Agent が終わったら待たずに次タスクを再配分する

## 7. GitHub 側 Codex との連携

- ローカル Codex は実装主体
- GitHub 側 Codex は PR review、inline comment、CI triage、autofix、maintenance を担当
- 状態同期の基準は `docs/todo/active.md` と `docs/handoff/current-state.md`
- PR / issue / review comment に対応したら、ローカル側 docs も同期する

## 8. 今回の release で重視すること

- PDF 出力を release 品質まで上げる
- template 編集 / 保存 / proof 導線を現場運用レベルまで仕上げる
- Excel / CSV 取り込みは DB 前処理なしでも使えるようにする
- proof 承認、legacy proof seed、audit search を一体運用できるようにする
- Bridge warning は `code / severity / message` を持つ構造化 warning を正とする

