# ADR 0009: Audit Retention Uses Backup Bundles

- Status: accepted
- Date: 2026-04-15

## Context

`desktop-shell` の audit ledger は `dispatch-ledger.json` と `proof-ledger.json` の 2 ファイルに分かれている。  
この状態で retention を単純に別々に削除すると、approved proof と proof dispatch の依存や lineage の追跡が壊れる。  
一方で次リリースでは、外部 DB を導入する前に local JSON ledger のまま export / trim / backup を運用可能にする必要がある。

## Decision

- audit export は scoped snapshot を返し、`admin-web` 側で JSON download にする
- audit retention は `maxAgeDays` / `maxEntries` を受ける
- retention 実行時は proof record と proof dispatch、dispatch parent chain の依存を保つよう keep set を補強する
- trim 実行前に、removed records は `audit/backups/` 配下へ single JSON bundle として保存する
- restore/list UI は別タスクとし、今回の release では backup bundle の生成までを成立条件にする

## Consequences

- local desktop mode だけで audit export と retention を運用できる
- proof gate を壊す trim を避けられる
- backup は 1 ファイル単位で残るため、保守・持ち出し・手動復元がしやすい
- ただし backup の一覧表示と復元 UI はまだ未実装で、現時点では手動運用になる
