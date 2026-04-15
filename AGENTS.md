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

- UI 先行ではなく印刷コア、ラベル製作コア先行
- JAN の正規化と検証は Rust 側を基準とする
- バーコード描画は Zint を前提とし、手作り描画ロジックを追加しない
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
- printer adapter を触った場合は `docs` と `packages/fixtures` を確認する
- `apps/admin-web` の submit 経路を触った場合は `packages/job-schema` と `apps/desktop-shell` の bridge を同時確認する
- `packages/job-schema` の dispatch 契約を触った場合は `apps/admin-web` と `apps/desktop-shell` の request / warning / retry を同時確認する
- `allowWithoutProof` / `sourceProofJobId` を触った場合は desktop-shell 側ポリシーと proof artifact 実在確認を同時確認する
- `packages/templates` を触った場合は `crates/render`、`scripts/validate-fixtures.mjs`、関連 docs を同時更新する
- `apps/admin-web` または `apps/desktop-shell` の実行接続を触った場合は `pnpm --filter @label/admin-web build` と `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml` を確認する

## 5. 今の主戦場

- `crates/barcode`
- `crates/render`
- `crates/printer-adapters`
- `apps/admin-web`
- `apps/desktop-shell`
- `packages/templates`
- `packages/job-schema`
- `packages/fixtures`

## 6. 開発体制（Sub-Agent 運用）

- Codex が本体責任者（主担当）として、計画、優先度、依存関係、最終統合、衝突解消、コミット前レビューを担当する
- Sub-Agent は必要領域ごとに起動し、`worker` 単位で単一責務を持つ（UI、pipeline、printer、テスト、ドキュメント、CI）
- Sub-Agent 起動条件は「同一モジュールの大きな変更」「大きな回帰試験が必要」「同時進行で独立領域を加速」または「緊急デバッグの局所対処」
- Sub-Agent のモデルは `gpt-5.3-codex-spark` を第一優先、利用不可時のみ `gpt-5.3-codex` `gpt-5.4-mini`にフォールバック。両モデルを常にログ化
- Sub-Agent 間の役割境界は対象ファイルの重複を避ける。原則同一ファイルの同時編集は禁止
- 実装変更を行う Sub-Agent は差分要点と検証結果を短く報告。状態変更は `docs/handoff/current-state.md`、優先順位変更は `docs/todo/active.md`、罠は `docs/known-issues.md`、新判断は `docs/adr/` へ反映
- Sub-Agent の数に固定上限は置かない。足りない場合は Codex が即時追加
- リポジトリ内の情報同期は `docs/todo/active.md` の状態管理を起点にし、状態更新と検証結果を `docs/handoff/current-state.md` に一元記録

## 7. GitHub 側 Codex 連携

- ローカル Codex が主導者。GitHub 側は実行支援として以下を担当する
  - PR レビュー（コメント、inline feedback）
  - PR コメントや Issue への指摘対応
  - CI 判定トリアージ
  - メンテナンス（定型修正・依存更新）
  - Autofix（合意済みの決定のみ）
- ハンドオフ手順（最小）
  1. GitHub 側の指摘や提案は、Task ID（例 `T-XXX`）と影響範囲、根拠を添えて受け取る
  2. ローカル側は受領後、`docs/todo/active.md` へステータスを反映し、重複排他を明示する
  3. 実施可能な修正はローカル Sub-Agent が対応し、結果は `docs/handoff/current-state.md` とテスト結果で報告する
  4. GitHub 側は PR/レビュー更新後に最新結果を返信し、状態一致を確認したら Close/Done
- ローカル Sub-Agent と GitHub 側の役割境界
  - ローカル Sub-Agent: 実装、設計判断、統合、最終的な品質責任
  - GitHub 側: 検証補助、レビュー運用、CI 分析、反復修正（autofix）
- 同期規律
  - 重要な状態変更は `docs/todo/active.md` と `docs/handoff/current-state.md` へ同日更新
  - 未処理の警告・再現手順・失敗事例は `docs/known-issues.md` に追記
  - 重要判断は `docs/adr/` へ残す
