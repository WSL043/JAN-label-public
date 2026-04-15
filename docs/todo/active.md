# active-todo

次に着手すべき順番と、release 直前までの主戦場をまとめる。

## Done

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-026 | P1 | done | Codex + Sub-Agent | `admin-web` と `desktop-shell` bridge を実運用導線まで接続 | manual / batch submit、retry、bridge status、proof gate が UI から扱える |
| T-026b | P1 | done | Codex + Sub-Agent | manual / batch / import submit の状態遷移を揃える | `ready / submitting / submitted / failed` と retry lineage が揃う |
| T-026c | P1 | done | Codex + Sub-Agent | bridge status と submit block UX を改善 | bridge unavailable と failed row が UI で判断できる |
| T-026d | P1 | done | Codex + Sub-Agent | bridge warning を構造化する | `warningDetails` を `code / severity / message` で返し、UI block 判定も移行 |
| T-026e | P1 | done | Codex + Sub-Agent | XLSX 数値セル JAN の hardening | scientific / decimal / ambiguous 12-digit numeric JAN を block、13-digit numeric は warning |
| T-026f | P1 | done | Codex + Sub-Agent | batch retry の重複再送を止める | failed / ready のみ再送し、submitted 行は再印刷しない |
| T-026g | P1 | done | Codex + Sub-Agent | template catalog mismatch を queue / submit blocker にする | unknown live `template_version` では manual / batch / queued retry を止める |
| T-027a | P1 | done | Codex + Sub-Agent | proof ledger と review API を `desktop-shell` に追加 | `approve_proof` / `reject_proof` / `search_audit_log` が使える |
| T-027b | P1 | done | Codex + Sub-Agent | `admin-web` に proof inbox / review UI を追加 | pending proof の approve / reject と approved proof pinning ができる |
| T-027c | P1 | done | Codex + Sub-Agent | approved proof と print payload の strict match を実装 | `templateVersion + sku + brand + jan(normalized) + qty + lineage` を満たさない print を拒否 |
| T-027d | P1 | done | Codex + Sub-Agent | proof lineage authority を backend に移す | approved proof lineage と不一致の explicit lineage / `reprintOfJobId` を拒否し、未指定 lineage は backend で補完する |
| T-027e | P1 | done | Codex + Sub-Agent | approved proof artifact validation を PDF 妥当性まで引き上げる | missing / empty / bad-header proof artifact で print を拒否する |
| T-028a | P1 | done | Codex + Sub-Agent | dispatch / proof の local ledger 永続化 | `dispatch-ledger.json` / `proof-ledger.json` に保存される |
| T-028b | P1 | done | Codex + Sub-Agent | `admin-web` に audit search UI を追加 | local ledger の検索、proof status 表示、proof pinning ができる |
| T-028c | P2 | done | Codex + Sub-Agent | legacy proof seed / migration を実装 | `validate_legacy_proof_seed` / `seed_legacy_proofs` と seed UI で pending seed ができる |
| T-028d | P1 | done | Codex + Sub-Agent | audit persistence failure を release blocker にする | dispatch 前に ledger writable を preflight し、dispatch 後の audit persistence failure も fatal にする |
| T-028e | P1 | done | Codex + Sub-Agent | audit export / retention / backup を運用導線に載せる | scoped export、dry-run trim、backup bundle、UI 操作が揃う |
| T-032c | P1 | done | Codex + Sub-Agent | packaged template catalog を desktop-shell から配布する | `admin-web` が desktop catalog を読み、unknown `template_version` を submit 前に明示する |
| T-032b | P1 | done | Codex + Sub-Agent | structured template editor の UX と preview 基線を実装 | workbench CSS、local canvas、Rust preview 導線が揃う |
| T-033b | P1 | done | Codex + Sub-Agent | render parity を改善 | border visible、background / border / field color が SVG / PDF に反映される |
| T-034 | P1 | done | Codex + Sub-Agent | template schema / manifest と manifest 駆動 render | `packages/templates` と `crates/render` の schema 契約が揃う |
| T-035 | P1 | done | Codex + Sub-Agent | template asset export / import | form state / mapping / draft snapshot を asset に同梱できる |
| T-037 | P1 | done | Codex + Sub-Agent | dispatch request / result と Tauri bridge | `dispatch_print_job` / `print_bridge_status` の request / response 契約が揃う |
| T-038 | P1 | done | Codex + Sub-Agent | printer profile route と proof gate hardening | adapter route と proof gate を bridge 側で検証する |
| T-039 | P1 | done | Codex + Sub-Agent | import / retry / batch UX 改善 | `enabled`、retry lineage、lazy XLSX import、snapshot queue が安定する |
| T-040 | P1 | done | Codex + Sub-Agent | render / schema の hardening | SVG attribute 安全化と template color 制約が入る |

## Now

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-029 | P2 | pending | Codex + Operator | 運用 runbook / 停止・再開・エスカレーション整備 | 現場向けの手順書と障害時の判断基準が揃う |
| T-032 | P1 | in_progress | Codex + Sub-Agent | label authoring core を BarTender 最低線まで上げる | schema / asset / preview / proof が一連の authoring flow になる |
| T-032a | P1 | pending | Codex + Sub-Agent | template catalog write-back / manifest 連携 | editor の生 JSON が packaged template と同期される |
| T-033 | P1 | in_progress | Codex + Sub-Agent | preview / proof parity を上げる | preview と proof の差異が operator に説明可能な状態になる |
| T-033a | P1 | pending | Codex + Sub-Agent | authored template の proof dispatch 連携 | preview で確認した template が proof / print に繋がる |

## Next

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-028f | P2 | pending | Codex + Sub-Agent | audit backup list / restore 運用を整える | backup bundle の一覧・復元手順が docs と実装で固まる |
| T-012 | P3 | pending | Codex + Infra | self-hosted runner / webhook 運用 | GitHub 側 Codex の自動運用を固定化する |
| T-016 | P3 | pending | Codex | release notes 補助生成 | maintenance ledger から release note を半自動生成できる |

## Blocked

| id | priority | status | owner | task | unblock condition |
| --- | --- | --- | --- | --- | --- |
| T-030 | P1 | blocked | Operator + Codex | GitHub Actions に `OPENAI_API_KEY` を設定 | repository secret が設定される |
| T-031 | P1 | blocked | Operator + Codex | 実機プリンタ測定を `docs/printer-matrix/` に反映 | 実機で 100% scale / scan / mm 測定を commit する |
