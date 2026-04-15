# active-todo

次に着手すべき順番と、release 直前までの実装順をまとめる。

## Done

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-026 | P1 | done | Codex + Sub-Agent | `admin-web` と `desktop-shell` bridge を実運用導線まで接続 | manual / batch submit、retry、bridge status、proof gate が UI から扱える |
| T-026b | P1 | done | Codex + Sub-Agent | manual / batch / import submit の状態遷移を揃える | `ready / submitting / submitted / failed` と retry lineage が一貫する |
| T-026c | P1 | done | Codex + Sub-Agent | bridge status と再試行 UX を仕上げる | submit block、bridge unavailable、failed row 再送が UI で分かる |
| T-026d | P1 | done | Codex + Sub-Agent | bridge warning を構造化する | `warningDetails` に `code / severity / message` を持ち、UI block 判定が regex 依存から外れる |
| T-026e | P1 | done | Codex + Sub-Agent | XLSX 数値セル JAN の hardening | scientific / decimal / ambiguous 12-digit numeric JAN を block し、13-digit numeric は warning 表示する |
| T-027a | P1 | done | Codex + Sub-Agent | proof ledger と review API を `desktop-shell` に追加 | `approve_proof` / `reject_proof` / `search_audit_log` が動く |
| T-027b | P1 | done | Codex + Sub-Agent | `admin-web` に proof inbox / review UI を接続 | pending proof の approve / reject と approved proof の pinning が UI で完結する |
| T-027c | P1 | done | Codex + Sub-Agent | approved proof と print payload の strict match を追加 | `templateVersion + sku + brand + jan(normalized) + qty + lineage` を満たさない print を拒否する |
| T-028a | P1 | done | Codex + Sub-Agent | dispatch / proof の local ledger 永続化 | `dispatch-ledger.json` / `proof-ledger.json` に local 保存される |
| T-028b | P1 | done | Codex + Sub-Agent | `admin-web` に audit search UI を追加 | local ledger の検索、proof status 表示、proof pinning ができる |
| T-028c | P2 | done | Codex + Sub-Agent | legacy proof seed / migration を追加 | `validate_legacy_proof_seed` / `seed_legacy_proofs` と admin-web seed UI で pending seed ができる |
| T-034 | P1 | done | Codex + Sub-Agent | template schema / manifest と manifest 駆動 render | `packages/templates` と `crates/render` の schema 契約が揃う |
| T-035 | P1 | done | Codex + Sub-Agent | template asset export / import | template asset に form state / mapping / draft snapshot を保持できる |
| T-037 | P1 | done | Codex + Sub-Agent | dispatch request / result と Tauri bridge | `dispatch_print_job` / `print_bridge_status` が request contract と同期する |
| T-038 | P1 | done | Codex + Sub-Agent | printer profile route と proof gate hardening | job ごとの adapter route と proof gate が bridge 側で検証される |
| T-039 | P1 | done | Codex + Sub-Agent | import / retry / batch UX 改善 | `enabled` 列、retry lineage、lazy XLSX import、snapshot queue が安定する |
| T-040 | P1 | done | Codex + Sub-Agent | render / schema の安全化 | SVG attribute と template color 制約が追加される |

## Now

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-028 | P1 | in_progress | Codex + Sub-Agent | audit 運用を release 品質まで上げる | review / search / retention / export の運用ルールが揃う |
| T-029 | P2 | pending | Codex + Operator | 停止 / 再開 / エラー時の運用ガイドを整備 | 現場向け停止判断、再送条件、エスカレーションを文書化する |
| T-032 | P1 | in_progress | Codex + Sub-Agent | ラベル制作コアを BarTender 最低線まで引き上げる | schema / asset / preview / proof が一連の authoring フローになる |
| T-032a | P1 | pending | Codex + Sub-Agent | template schema と asset drift 検知 | stale / invalid / mapping drift を authoring UI で検知する |
| T-033 | P1 | pending | Codex + Sub-Agent | preview / proof 設計を仕上げる | preview と proof の差分が operator に説明可能になる |
| T-033a | P1 | pending | Codex + Sub-Agent | printer route / proof route の可視化 | printer profile ごとの差異が preview 上で追える |

## Next

| id | priority | status | owner | task | done when |
| --- | --- | --- | --- | --- | --- |
| T-012 | P3 | pending | Codex + Infra | self-hosted runner / webhook 運用 | GitHub 側 Codex の自動運用を安定化する |
| T-016 | P3 | pending | Codex | release notes 補助生成 | maintenance ledger から release note を半自動で作る |

## Blocked

| id | priority | status | owner | task | unblock condition |
| --- | --- | --- | --- | --- | --- |
| T-030 | P1 | blocked | Operator + Codex | GitHub Actions に `OPENAI_API_KEY` を設定 | repository secret が設定される |
| T-031 | P1 | blocked | Operator + Codex | 実機プリンタ測定を `docs/printer-matrix/` に追加 | 実機で 100% scale / scan 結果 / mm 測定を commit する |

