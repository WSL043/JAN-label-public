# active-todo

継続開発で今見るべき作業キューです。  
長期計画は `docs/mvp-backlog.md`、日々の実行順はこのファイルを正とします。

## Done

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-001 | `#1` | P0 | done | Codex | Zint CLI adapter を `crates/barcode` に実装 | 実バイナリパスを受けて render 呼び出しをテストできる |
| T-002 | `#2` | P0 | done | Codex | `render` に PDF 出力ルートを追加 | SVG と並んで PDF fixture を比較できる |
| T-003 | `#3` | P1 | done | Codex | `importer` の行単位バリデーションを実装 | 列だけでなくセル値エラーを返せる |
| T-004 | `#4` | P1 | done | Codex | `admin-web` にジョブ作成フォームを追加 | parent_sku, sku, jan, qty, brand を入力できる |
| T-005 | `#5` | P1 | done | Codex | `audit-log` に lineage / reprint 情報を追加 | 再印刷の系譜を表現できる |
| T-006 | `#6` | P1 | done | Codex | `printer-adapters` に PDF adapter を追加 | print-agent から proof 出力できる |
| T-007 | `-` | P2 | done | Codex | Windows spooler adapter の骨格実装 | printer profile 経由で submit できる |
| T-008 | `-` | P2 | done | Codex | `docs/printer-matrix` に baseline 記録を追加 | 初回 release gate を満たす記録が commit される |
| T-009 | `-` | P2 | done | Codex | 初回 release tag 発行 | `v0.1.1` Release workflow が成功し Windows installer が添付される |
| T-010 | `-` | P2 | done | Codex | CI failure 時の Codex triage workflow を追加 | same-repo PR の失敗 CI に Codex の診断コメントが付く |
| T-011 | `-` | P3 | done | Codex | Codex maintenance schedule / release-prep automation を追加 | schedule か workflow_dispatch で unresolved CI / release blocker を要約できる |
| T-013 | `-` | P3 | done | Codex | CI failure から自動修正 PR を起こす workflow を追加 | Codex が fix branch を作り draft PR まで出せる |
| T-014 | `-` | P3 | done | Codex | release prep の結果を issue / discussion に定期集約する | schedule 実行結果が GitHub 上の恒久的なスレッドに残る |
| T-015 | `-` | P2 | done | Codex | `apps/desktop-shell` の Windows 配布シェルを初期化 | `desktop-shell-windows` CI と Release workflow で Windows bundle 経路を検証できる |

## Now

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-017 | `-` | P1 | blocked | Operator + Codex | GitHub Actions に `OPENAI_API_KEY` secret を設定 | `Codex PR Review`, `Codex Maintenance`, `Codex CI Autofix` が fallback ではなく cloud 実行される |
| T-018 | `-` | P1 | blocked | Operator + Codex | `docs/printer-matrix` に物理プリンタ実測を追加 | mm 実測値と barcode scan 結果が 1 機種分 commit される |

## Next

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-012 | `-` | P3 | pending | Codex + Infra | self-hosted runner または webhook ベースの Codex agent 化 | persistent な `CODEX_HOME` か外部 webhook で半常駐運用できる |

## Later

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-016 | `-` | P3 | pending | Codex | release prep と printer matrix の結果から `v0.1.x` release notes を補助生成する | maintenance ledger から release note 下書きを出せる |

## Blocked

| id | blocker | task | unblock condition |
| --- | --- | --- | --- |
| B-001 | GitHub plan 制約 | `main` branch protection / ruleset の本適用 | GitHub Pro / Team 以上へ変更 |
| B-002 | repository secret 値がこのローカル環境にない | T-017 | Operator が `OPENAI_API_KEY` を用意して GitHub repository secret に設定する |
| B-003 | 実機プリンタ / スキャナへのアクセスが未確保 | T-018 | 対象プリンタで 100% scale 印刷と scan を実施できる |
