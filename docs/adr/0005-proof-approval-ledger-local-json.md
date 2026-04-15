# 0005 Proof Approval Ledger Local JSON

- Status: Accepted
- Date: 2026-04-15

## Context

`allowWithoutProof` は無効化したが、`sourceProofJobId` を proof PDF の実在確認だけで扱う運用では、
承認状態、却下状態、proof 再作成、監査検索を安定して扱えない。  
一方で次リリースでは multi-host 対応の重量 backend より先に、desktop-shell 単体で
proof 承認と audit 検索を閉じる必要がある。

## Decision

proof 承認の正は、当面 `apps/desktop-shell` 配下の local JSON ledger に置く。  
保存先は `JAN_LABEL_AUDIT_LOG_DIR` 配下の次の 2 ファイルとする。

- `dispatch-ledger.json`
- `proof-ledger.json`

`desktop-shell` は次を担当する。

- proof dispatch 成功時の pending proof 登録
- `approve_proof` / `reject_proof` / `search_audit_log` の提供
- print 実行前の `sourceProofJobId` 検証
  proof PDF 実在 + approved ledger 記録

UI は `admin-web` から Tauri invoke でこれを操作し、proof inbox / audit search を構成する。

## Consequences

- desktop-shell 単体で proof 承認と監査検索の最小導線を成立できる
- proof 状態を `pending / approved / rejected / superseded` で扱える
- browser 単体にはない desktop mode 前提の運用境界が明確になる
- local JSON のため、rotation / backup / multi-host 同期 / 長期保持は別タスクになる
- approved proof と print 対象の厳密照合は次段の追加設計が必要になる

## Rejected Alternatives

- proof 承認を UI ローカル state だけで持つ
- いきなり外部 DB / SaaS を必須化する
- proof PDF の存在確認だけで本印刷を許可し続ける
