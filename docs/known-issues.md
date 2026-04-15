# known-issues

## K-001 branch protection 未設定
- status: open
- 影響: `main` への直 push を review / CI なしで通してしまう
- 対応: GitHub 側で branch protection / ruleset を設定する

## K-004 実機プリンタ測定が未完了
- status: open
- 影響: baseline は PDF proof のみで、100% scale / scan / mm 測定がまだない
- 対応: `docs/printer-matrix/` に実機測定 commit を追加する

## K-005 ローカル Windows で `cargo test --workspace` が稀に揺れる
- status: open
- 影響: `print-agent` 実行時に `os error 5` が出ることがある
- 対応: desktop-shell 側テストも含めて再実行し、再現率を監視する

## K-008 GitHub Actions の `OPENAI_API_KEY` 未設定
- status: open
- 影響: cloud-side Codex automation の fallback が効かない
- 対応: repository secret を設定する

## K-009 browser preview mode は submit 不可
- status: open
- 影響: browser 単体では preview-only。submit / bridge status / audit search は使えない
- 対応: 実運用では desktop-shell から開く

## K-010 bridge env fallback は設定漏れを隠しやすい
- status: open
- 影響: safe default に寄せるため、環境設定不足が見えづらい
- 対応: `print_bridge_status` の `warningDetails` を監視し、運用 checklist に入れる

## K-011 XLSX import chunk は依然として大きい
- status: watch
- 影響: lazy-load 化したが import chunk 自体は大きい
- 対応: worker 化や更なる分離を検討する

## K-012 proof / audit は local filesystem ledger 前提
- status: open
- 影響: multi-host / shared storage / retention 設計が未完
- 対応: `T-028` と `T-029` で retention / export / backup を進める

## K-013 XLSX 数値セル JAN は Excel 側で text 保存が最善
- status: watch
- 影響: UI で block / warning はするが、入力時点での壊れは完全には防げない
- 対応: 現場では JAN 列を text で保存する

## K-014 local audit ledger の retention / backup 未整備
- status: open
- 影響: `dispatch-ledger.json` / `proof-ledger.json` が増え続ける
- 対応: rotation / export / archive を `T-028` で実装する

## K-015 legacy proof seed の gap
- status: resolved
- 解消: `validate_legacy_proof_seed` / `seed_legacy_proofs` と admin-web seed UI を追加し、pending seed 後に通常 approve できる

## K-016 approved proof と print 対象の厳密一致
- status: resolved
- 解消: `templateVersion + sku + brand + jan(normalized) + qty + lineage` の strict match を実装した

## K-017 一時 `target-*` ディレクトリが formatter を壊す
- status: watch
- 影響: workspace 直下の一時 target が Biome の対象に入り `pnpm format:check` が落ちる
- 対応: 一時 target は検証前に削除する

## K-018 approved proof lineage の drift 余地
- status: resolved
- 解消: `desktop-shell` が approved proof lineage を backend で補完し、explicit lineage / `reprintOfJobId` の不一致を reject するようにした

## K-019 print 成功でも audit persistence failure を non-fatal にしている
- status: resolved
- 解消: dispatch 前の ledger writable preflight と、dispatch 後の audit persistence failure の fatal 化を入れた

## K-020 template editor の write-back 未接続
- status: open
- 影響: structured editor と Rust preview は live JSON を見られるが、proof / print dispatch はまだ packaged manifest の `template_version` を使う
- 対応: template catalog write-back と authored template proof route を `T-032a` / `T-033a` で実装する
