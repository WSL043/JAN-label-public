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
- 恒久対応: `pnpm/action-setup`, `dorny/paths-filter`, `actions/upload-artifact` の後継バージョンを追う

## K-003 Zint adapter は実装済みだが実バイナリ導入が未完了

- 状態: open
- 影響: `barcode` crate は fake executable テストで安定化したが、実 Zint を使う E2E / CI はまだ未接続
- 回避: 外部注入の `zint` バイナリパスでローカル確認し、CI は fake executable テストを維持する
- 恒久対応: Windows / CI で Zint を導入し render / proof 経路まで結線する

## K-004 物理プリンタの実測データがまだない

- 状態: open
- 影響: `v0.1.1` は PDF proof baseline で release 済みだが、物理プリンタでの 100% scale / scan 検証は未完了
- 回避: `docs/printer-matrix/2026-04-15-pdf-proof-baseline.md` を baseline として維持する
- 恒久対応: `docs/printer-matrix/` に物理プリンタ実測を追加する

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

## K-007 Windows 配布シェルは CI / release で build できるがローカル bundle は未検証

- 状態: open
- 影響: GitHub-hosted `windows-latest` では `v0.1.1` release まで通っても、一部ローカル Windows では `link.exe` 不在により bundle を再現できない
- 回避: PR では `desktop-shell-windows` job を正とし、tag release は GitHub-hosted Windows runner に任せる
- 恒久対応: Build Tools 入りの Windows で `pnpm --filter @label/desktop-shell build` を通し、ローカルでも installer を再現できるようにする

## K-008 GitHub Actions の `OPENAI_API_KEY` secret が未設定

- 状態: open
- 影響: `Codex PR Review`, `Codex Maintenance`, `Codex CI Autofix` などの cloud-side Codex 実行は preflight で skip される
- 回避: ローカル Codex で実装を進め、GitHub 側は ledger / triage の fallback 出力を使う
- 恒久対応: repository secret に `OPENAI_API_KEY` を設定する
