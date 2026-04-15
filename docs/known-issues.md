# known-issues

## K-001 branch protection 未設定
- status: open
- 影響: `main` への保護が弱く、review / CI を経ずに更新できる
- 対策: GitHub 側で branch protection / ruleset を設定する

## K-004 実機プリンタ検証が未完了
- status: open
- 影響: 現在の baseline は PDF proof 中心で、実機 100% scale / scan / mm 測定は揃っていない
- 対策: `docs/printer-matrix/` に実測 commit を追加する

## K-005 ローカル Windows で `cargo test --workspace` が不安定
- status: open
- 影響: `print-agent` 実行時に `os error 5` で止まることがある。コード不整合ではなく実行環境要因の可能性が高い
- 対策: desktop-shell 単体テストと compile は並行で確認し、workspace 全体の再実行可否を別途観察する

## K-008 GitHub Actions の `OPENAI_API_KEY` 未設定
- status: open
- 影響: cloud-side Codex automation が fallback のみになる
- 対策: repository secret を設定する

## K-009 browser preview mode は submit 不可
- status: open
- 影響: `admin-web` をブラウザ単体で開くと preview-only。submit / bridge status / audit search は Tauri 未接続エラーになる
- 対策: 実行系は desktop-shell から開く

## K-010 bridge env fallback は設定漏れを隠しやすい
- status: open
- 影響: `desktop-shell` は safe default に落とすため、運用設定の未入力が見えづらい
- 対策: `print_bridge_status` の `warningDetails` を起動前チェックに含める

## K-011 XLSX import は main chunk から切り離しているがサイズはまだ大きい
- status: watch
- 影響: `xlsx` は lazy-load だが、import chunk 自体は大きい
- 対策: 体感遅延が問題化したら worker 化または分割を再検討する

## K-012 proof / audit は local filesystem ledger 前提
- status: open
- 影響: multi-host / shared storage / retention 設計は未完成
- 対策: `T-028` と `T-029` で retention / export / 運用ルールを整理する

## K-013 XLSX 数値セル JAN は完全には安全でない
- status: watch
- 影響: admin-web は scientific / decimal / ambiguous 12-digit numeric JAN を block し、13-digit numeric JAN は warning を出すが、Excel 側で text 保存した方が安全
- 対策: 現場運用では JAN 列を text で保持する。必要なら stricter mode を追加する

## K-014 local audit ledger の retention / backup が未整備
- status: open
- 影響: `dispatch-ledger.json` / `proof-ledger.json` は増え続ける
- 対策: rotation / export / archive を `T-028` で追加する

## K-015 legacy proof seed 前提の gap
- status: resolved
- 原因: ledger 導入前の proof は approval ledger を持たず、本印刷に使えなかった
- 対策: `validate_legacy_proof_seed` / `seed_legacy_proofs` と admin-web seed UI を追加済み。pending seed 後に通常 approve する

## K-016 approved proof と print 対象の厳密一致
- status: resolved
- 原因: approved proof の existence check だけでは誤印刷を止めきれなかった
- 対策: `templateVersion + sku + brand + jan(normalized) + qty + lineage` を strict match するように更新済み

## K-017 一時 `target-*` ディレクトリが formatter を汚す
- status: watch
- 影響: workspace 配下に作った一時 target を Biome が走査し、`pnpm format:check` が落ちる
- 対策: 一時 target は作業後に削除する

