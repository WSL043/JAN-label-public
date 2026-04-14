# known-issues

継続開発で再発しやすい問題や、現時点で残している制約を記録します。

## K-001 branch protection が未適用

- 状態: open
- 影響: `main` への直接 push を GitHub 側で強制できない
- 回避: 運用で PR ベースを維持し、CI green を確認してから反映する
- 恒久対応: GitHub Pro / Team 以上で ruleset を適用

## K-002 third-party Actions が Node 20 target 警告を出す

- 状態: open
- 影響: 今は通るが将来の runner 変更で壊れる可能性がある
- 回避: `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` を workflow に設定
- 恒久対応: `pnpm/action-setup` と `dorny/paths-filter` の後継バージョンを追う

## K-003 Zint adapter は実装済みだが実バイナリ導入が未完了

- 状態: open
- 影響: `barcode` crate は fake executable テストで安定化したが、実 Zint を使う E2E / CI はまだ未接続
- 回避: 外部注入の `zint` バイナリパスでローカル確認し、CI は fake executable テストを維持する
- 恒久対応: Windows / CI で Zint を導入し render / proof 経路まで結線する

## K-004 実機プリンタの測定データがまだない

- 状態: open
- 影響: 100% スケール検証が PDF proof に偏っており、`v0.1.0` release 条件もまだ満たせない
- 回避: PDF proof を先に維持する
- 恒久対応: `docs/printer-matrix/` に最低 1 機種分の実測を記録する

## K-005 ローカル Windows に `link.exe` がないと `cargo test --workspace` が失敗する

- 状態: open
- 影響: ローカルでは full test を完走できなくても、CI 上は green という差が出る
- 回避: `cargo check --workspace --tests` を補助で回しつつ、GitHub Actions の `rust-test` / `golden-tests` を正として確認する
- 恒久対応: Windows Build Tools を導入し、`link.exe` が解決できる開発環境手順を整える

## K-006 Codex CI Autofix は same-repo PR 前提

- 状態: open
- 影響: fork PR や GitHub Actions の PR 作成権限が不足する repo では draft autofix PR まで完了しない
- 回避: same-repo PR では autofix を使い、fork PR では triage comment を正とする
- 恒久対応: 必要なら self-hosted runner / webhook と明示的な bot 権限で fork 対応経路を別に作る

## K-007 Windows 配布シェルは scaffold 済みだが build 未検証

- 状態: open
- 影響: `apps/desktop-shell` の Tauri shell は入ったが、この環境では `link.exe` 不在により bundle 生成まで確認できない
- 回避: `tauri info` と `admin-web` build で config を確認しつつ、当面は `admin-web` + `print-agent` を主開発経路として使う
- 恒久対応: Build Tools 入りの Windows で `pnpm --filter @label/desktop-shell build` を通し、配布フローを release docs と CI に組み込む

## K-008 GitHub Actions の `OPENAI_API_KEY` secret が未設定

- 状態: open
- 影響: `Codex PR Review`, `Codex Maintenance`, `Codex CI Autofix` などの cloud-side Codex 実行は preflight で skip される
- 回避: ローカル Codex で実装を進め、GitHub 側は ledger / triage の fallback 出力を使う
- 恒久対応: repository secret に `OPENAI_API_KEY` を設定する
