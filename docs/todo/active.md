# active-todo

継続開発で今見るべき作業キューです。  
長期計画は `docs/mvp-backlog.md`、日々の実行順はこのファイルを正とします。

## Now

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-004 | `#4` | P1 | ready | Codex | `admin-web` にジョブ作成フォームを追加 | parent_sku, sku, jan, qty, brand を入力できる |

## Next

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-005 | `#5` | P1 | ready | Codex | `audit-log` に lineage / reprint 情報を追加 | 再印刷の系譜を表現できる |
| T-006 | `#6` | P1 | ready | Codex | `printer-adapters` に PDF adapter を追加 | print-agent から proof 出力できる |
| T-007 | `-` | P2 | pending | Codex | Windows spooler adapter の骨格実装 | printer profile 経由で submit できる |

## Later

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-008 | `-` | P2 | pending | Codex | `docs/printer-matrix` に実測表を追加 | 最低 1 機種分の測定値が入る |
| T-009 | `-` | P2 | pending | Codex | 初回 `v0.1.0` リリースタグ発行 | Release workflow が 1 回成功する |

## Done

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-001 | `#1` | P0 | done | Codex | Zint CLI adapter を `crates/barcode` に実装 | 実バイナリパスを受けて render 呼び出しをテストできる |
| T-002 | `#2` | P0 | done | Codex | `render` に PDF 出力ルートを追加 | SVG と並んで PDF fixture を比較できる |
| T-003 | `#3` | P1 | done | Codex | `importer` の行単位バリデーションを実装 | 列だけでなくセル値エラーを返せる |

## Blocked

| id | blocker | task | unblock condition |
| --- | --- | --- | --- |
| B-001 | GitHub plan 制約 | `main` branch protection / ruleset の本適用 | GitHub Pro / Team 以上へ変更 |
