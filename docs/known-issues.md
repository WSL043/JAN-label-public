# known-issues

## K-001 branch protection 未設定

- 状態: open
- 影響: `main` への直接 push が可能で、レビューと CI の通過ルートを飛び越せる。
- 対策: GitHub 側で branch protection / ruleset を有効化し、少なくとも PR + 1 Approvals + 必須チェックを必須化する。

## K-002 Node 実行環境依存

- 状態: open
- 影響: 旧来アクション (`pnpm/action-setup`, `dorny/paths-filter`, `actions/upload-artifact`) の Node 20 対応が一貫しない。
- 対策: `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` を有効にするなど、workflow 側での明示的なランタイム固定を検討する。

## K-003 Zint 実体連携

- 状態: open
- 影響: テスト・E2E で実バイナリ `zint` 代わりにモック実装に依存しているため、実環境との差分を取りこぼす。
- 対策: `barcode` 側の zint バイナリ導入手順を整備し、CI/ローカルの双方で実体実行を確認する。

## K-004 PDF proof ベースラインの網羅不足

- 状態: open
- 影響: `v0.1.1` の baseline は PDF proof 比較までで、物理プリンタ実測と barcode scan は未完了のため、機種・環境ごとの差分再現が不足している。
- 対策: `docs/printer-matrix/` に機種追加時の定例実機サンプルと再測定結果を積み上げる。

## K-005 local Windows で `cargo test --workspace` が不安定

- 状態: open
- 影響: `os error 5` / `link.exe` 系の権限制約で full test が断続的に失敗することがある。
- 対策: 問題再現時は `cargo test -p render` / `cargo test -p print-agent` など分割実行へ切替え、CI 成果を基準に判断する。Windows Build Tools あり環境での再現手順を `windows-bootstrap` 側で再確認する。

## K-006 Codex CI Autofix の PR 境界

- 状態: open
- 影響: fork PR 以外で draft autofix を作成すると、レビュー負荷が増える。
- 対策: same-repo PR では基本 autofix を行わず、triggered コメント/triage 指摘ルートへ集約する。

## K-007 Windows 配布シェルは CI が先行

- 状態: open
- 影響: `windows-latest` では問題なく通るが、ローカル Windows では Build Tools 不備や `link.exe` 不在で bundle が再現しない。
- 対策: `desktop-shell-windows` と Release workflow を真の green 条件とし、ローカルは手順化された CI 比較テストでのみ検証する。

## K-008 GitHub Actions の `OPENAI_API_KEY`

- 状態: open
- 影響: cloud-side Codex の一部 preflight が skip され、レビュー・保守の自動化が遅延する。
- 対策: リポジトリ secret に `OPENAI_API_KEY` を追加し、関連 workflow が参照可能であることを事前確認する。

## K-009 browser 単体は submit 非対応

- 状態: open
- 影響: `apps/admin-web` はブラウザで起動した場合、`preview-only` でしか使えず、`dispatchPrintJob` / `fetchPrintBridgeStatus` は `Tauri` 未接続エラーで失敗する。
- 対策: 運用時は desktop-shell 経由に統一し、Web プレビューはあくまで確認用として扱う。  
  最低限、起動ログ/画面上に「desktop mode only」の案内を残し、submit ボタンは bridge 未接続時に明示的に無効化する。

## K-010 bridge の env fallback 依存

- 状態: open
- 影響: `desktop-shell` の `print_bridge_status` は既定値へフォールバックするため、設定漏れが実運用時の adapter / 出力先 / printer 名のずれを生む。高リスク warning は UI で block されるが、設定自体の修正は運用側で必要。
- 対策: 運用前は `print_bridge_status` を最初に確認し、`warnings` が空かを必須チェックにする。特に `JAN_LABEL_PRINT_ADAPTER` / `JAN_LABEL_ZINT_BINARY_PATH` / `JAN_LABEL_PRINT_OUTPUT_DIR` / `JAN_LABEL_SPOOL_OUTPUT_DIR` / `JAN_LABEL_WINDOWS_PRINTER_NAME` は明示設定する。

## K-011 admin-web の spreadsheet 依存は lazy-load 前提

- 状態: watch
- 影響: `xlsx` は lazy-load 化済みだが、Excel import を含む chunk 自体は依然として大きい。
- 対策: spreadsheet 導線を維持したまま、将来リリースでは worker 化や更なる chunk 分割を検討する。現状は main chunk warning は解消済み。

## K-012 proof 承認の実体は filesystem ベース

- 状態: open
- 影響: `sourceProofJobId` は desktop-shell で proof PDF の実在確認まで行うが、承認メモ、却下状態、失効、再作成履歴はまだ永続化されていない。
- 対策: `T-027` / `T-028` で proof 承認メタデータと監査永続化を導入し、`sourceProofJobId` を audit-log と結びつける。

## K-013 XLSX の数値セル JAN は text 前提

- 状態: open
- 影響: Excel 側で JAN を数値セルとして保存すると、先頭ゼロ喪失や表示形式依存の崩れを招きうる。現状の admin-web は digits-only で弾くが、誤った 12/13 桁として見えてしまう値までは区別できない。
- 対策: `T-026e` で XLSX の型付きセル情報を保持し、JAN 列の数値セルを reject するか text 前提で明示警告する。
