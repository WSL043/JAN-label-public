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
| T-021 | `-` | P2 | done | Codex + Sub-Agent | admin-web を運用寄りコンソールへ拡張 | 単票入力＋行入力＋レビューキュー＋スナップショット導線を実装 |
| T-022 | `-` | P1 | done | Codex + Sub-Agent | print-core の実行前検証を強化 | JAN 正規化、空値拒否、artifact/パス、adapter 種別のガードを追加 |
| T-023 | `-` | P2 | done | Codex + Sub-Agent | importer / printer-adapters の不正耐性を強化 | TRUE/FALSE 読み込み許容、空 artifact、メディア種別チェックを追加 |
| T-024 | `-` | P1 | done | Codex + Sub-Agent | render の PDF 検証を構造ベースへ拡張 | xref、MediaBox、geometry、制御文字エスケープを追加検証 |
| T-025 | `-` | P2 | done | Codex + Sub-Agent | fixture 検証を強化 | schema、invalid fixture、PDF の整合検査を追加 |
| T-034 | `-` | P1 | done | Codex + Sub-Agent | template schema / manifest と manifest 駆動 render を追加 | `packages/templates` の schema v1 と `crates/render` の catalog 解決が固定化される |
| T-035 | `-` | P1 | done | Codex + Sub-Agent | admin-web に template asset export / import と submit 導線を追加 | template asset を再読込でき、manual / batch draft を desktop-shell bridge へ送れる |
| T-036 | `-` | P1 | done | Codex + Sub-Agent | importer の business alias と業務 fixture を追加 | 日本語業務ヘッダを検証でき、曖昧ヘッダ fixture で reject を固定化できる |
| T-037 | `-` | P1 | done | Codex + Sub-Agent | dispatch 契約と desktop-shell Tauri bridge を追加 | request / result schema、proof gate、`dispatch_print_job` / `print_bridge_status` が揃う |
| T-038 | `-` | P1 | done | Codex + Sub-Agent | printerProfile ごとの route と proof gate hardening を追加 | job ごとの adapter が desktop-shell へ届き、`sourceProofJobId` は実在 proof PDF で検証される。`allowWithoutProof` は proof 承認ワークフロー完了まで無効化し、承認メモ/却下状態の永続化は別途 `T-027` / `T-028` で扱う |
| T-039 | `-` | P1 | done | Codex + Sub-Agent | admin-web の import / retry / batch 実行を実運用向けに固める | `enabled` を厳格化し、XLSX を lazy-load し、failed retry は新 jobId / lineage 付きで直列再送できる |
| T-040 | `-` | P1 | done | Codex + Sub-Agent | render の SVG 安全化と template color 制約を追加 | SVG attribute 注入を避け、template schema で color を hex に固定できる |

## Now

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-026 | `-` | P1 | in_progress | Codex + Sub-Agent | admin-web と print-agent の実行経路を desktop-shell bridge 経由で仕上げる | manual / batch submit、browser preview-only 境界、row 状態遷移、再試行 UX に加え、監査検索連携と bridge warning 構造化まで揃う |
| T-026a | `-` | P1 | done | Codex + Sub-Agent | submit payload 契約を固定する | template/version、execution、actor、lineage、reason、printerProfile の送信条件が docs と UI で一致する |
| T-026b | `-` | P1 | pending | Codex + Sub-Agent | manual / batch / import の submit 条件を揃える | Ready→Submitted→Completed/Failed の状態遷移と 12/13 桁 JAN submit 条件を共通化できる |
| T-026c | `-` | P1 | done | Codex + Sub-Agent | bridge status と失敗時再試行 UX を仕上げる | `print_bridge_status` の high-risk warning を UI で block でき、failed row は新 jobId / lineage で再送できる |
| T-026d | `-` | P1 | pending | Codex + Sub-Agent | bridge warning を構造化する | warning が `code` / `severity` / `message` を持ち、UI 側の block 条件を文字列依存から外せる |
| T-026e | `-` | P1 | pending | Codex + Sub-Agent | XLSX の型付き JAN 取り込みを厳格化する | 数値セルの JAN を text 前提で reject または型付きに扱い、先頭ゼロ喪失や表示形式崩れを運用前に検知できる |
| T-027 | `-` | P1 | in_progress | Codex + Sub-Agent | proof 承認ワークフローを実装 | pdf 保存・承認メモ・却下/再作成・承認なし本印刷のブロックを実装 |
| T-027a | `-` | P1 | pending | Codex + Sub-Agent | proof route を PDF 専用として固定する | proof-only で非 PDF adapter を拒否し、proof submit 結果を UI で確認できる |
| T-028 | `-` | P1 | pending | Codex + Sub-Agent | 監査ログの永続化と検索 UI を実装 | 検索・再印刷履歴・理由付き再印刷を監査可能にする |
| T-032 | `-` | P1 | in_progress | Codex + Sub-Agent | ラベル製作コア（Bartender 基礎）を実装 | template schema / asset、版管理、データソース紐付け、要素配置を一貫仕様で扱える |
| T-032a | `-` | P1 | pending | Codex + Sub-Agent | template schema v1 と template asset の drift 検知を固める | stale / invalid 判定、再読込手順、`enabled` と business alias の境界が固まる |
| T-033 | `-` | P1 | pending | Codex + Sub-Agent | ラベルテンプレートのプレビュー / proof 設計を実装 | `render` 駆動で SVG/PDF 即時プレビューとジョブ前 proof を作成し、軽量テンプレート編集 UI を接続 |
| T-033a | `-` | P1 | pending | Codex + Sub-Agent | printer route と proof route の可視化を揃える | SVG/PDF / Windows spool staging と printer profile の紐付けを 1 画面で確認できる |
| T-030 | `-` | P1 | blocked | Operator + Codex | GitHub Actions に `OPENAI_API_KEY` secret を設定 | `Codex PR Review`, `Codex Maintenance`, `Codex CI Autofix` が fallback ではなく cloud 実行される |
| T-031 | `-` | P1 | blocked | Operator + Codex | `docs/printer-matrix` に物理プリンタ実測を追加 | mm 実測値と barcode scan 結果が 1 機種分 commit される |

## Next

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-029 | `-` | P2 | pending | Codex + Operator | 実運用安全ガイドと停止/再開ルールを整備 | 重複ジョブ防止、停止/再開、例外停止時の手順を運用化 |
| T-012 | `-` | P3 | pending | Codex + Infra | self-hosted runner または webhook ベースの Codex agent 化 | persistent な `CODEX_HOME` か外部 webhook で半常駐運用できる |

## Later

| id | issue | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- | --- |
| T-016 | `-` | P3 | pending | Codex | release prep と printer matrix の結果から `v0.1.x` release notes を補助生成する | maintenance ledger から release note 下書きを出せる |

## Blocked

| id | blocker | task | unblock condition |
| --- | --- | --- | --- |
| B-001 | GitHub plan 制約 | `main` branch protection / ruleset の本適用 | GitHub Pro / Team 以上へ変更 |
| B-002 | repository secret 値がこのローカル環境にない | T-030 | Operator が `OPENAI_API_KEY` を用意して GitHub repository secret に設定する |
| B-003 | 実機プリンタ / スキャナへのアクセスが未確保 | T-031 | 対象プリンタで 100% scale 印刷と scan を実施できる |
