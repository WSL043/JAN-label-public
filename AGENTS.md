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
- `allowWithoutProof` は release 運用では使わない
- legacy proof の移行は pending seed まで。自動 approve はしない
- template editor のローカル canvas は近似表示。出力可否の判断は Rust preview / proof で行う
- 現時点の proof / print dispatch は packaged manifest の `template_version` を使う。editor の生 JSON は preview 用であり、本番反映は別タスク
- proof / print dispatch の最終 gate は `apps/desktop-shell` を正とする
- packaged template catalog の存在確認は `apps/desktop-shell` を正とする
- live template draft の `template_version` が desktop catalog に無い間は queue / proof / print submit に進めない

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

- 一時的な `target-*` ディレクトリを workspace 直下に残すと `pnpm format:check` が失敗することがある。不要なら削除してから実行する
- ローカル Windows では `cargo test --workspace` が稀に `os error 5` で揺れる。再実行し、必要なら `desktop-shell` 側のテストも個別に確認する

## 4. 作業ルール

- 新しい判断は `docs/adr/` に残す
- 引き継ぎが必要な状態変更は `docs/handoff/current-state.md` を更新する
- 次に着手すべき順番は `docs/todo/active.md` を更新する
- 再発しそうな罠は `docs/known-issues.md` に残す
- printer adapter に手を入れたら docs と fixtures を確認する
- `apps/admin-web` の submit / import / proof inbox を触ったら `apps/desktop-shell` と `packages/job-schema` の契約差分を確認する
- `apps/desktop-shell` の proof / audit / bridge warning を触ったら `docs/print-pipeline.md` と `docs/known-issues.md` を更新する
- proof gate を変えたら `sourceProofJobId` と lineage 条件を docs に明記する

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

- 主任はローカル Codex。タスク分解、優先度、統合、最終判断、docs 更新、GitHub 連携を持つ
- Sub-Agent は部門別に並列で使う。探索、攻撃的レビュー、実装、検証を分担する
- Sub-Agent の第一選択モデルは `gpt-5.3-codex-spark`
- `gpt-5.3-codex-spark` が使えない場合のみ `gpt-5.3-codex` にフォールバックする
- Sub-Agent が終わったら、主任が結果を統合し、次のタスクを即再配分する

## 7. GitHub 側 Codex との同期

- ローカル Codex が主管
- GitHub 側 Codex は PR review、inline comment、CI triage、autofix、maintenance を担当
- 状態同期の基準は `docs/todo/active.md` と `docs/handoff/current-state.md`
- PR / issue / review comment に対応したら、必要に応じて local docs も更新する

## 8. 次リリースで必須の到達点

- PDF 出力を release 品質まで上げる
- template authoring / preview / proof の基線を揃える
- Excel / CSV をそのまま実務投入できる import 導線を維持する
- proof review、legacy proof seed、audit search を運用できる状態にする
- bridge warning は `code / severity / message` を正とする
