# 0006. legacy proof seed は pending-only で投入する

- Status: accepted
- Date: 2026-04-15

## Context

proof approval ledger 導入前に生成した PDF proof は、`sourceProofJobId` に使っても approval record と proof dispatch record が無いため本印刷できなかった。

同時に、strict proof-to-print gate により以下が必要になった。

- approved proof ledger record
- proof dispatch ledger record
- `templateVersion + sku + brand + jan(normalized) + qty + lineage` の整合

legacy proof を直接 approved で投入すると、review 履歴と運用責任の線が曖昧になる。

## Decision

legacy proof は次の方式で移行する。

- `validate_legacy_proof_seed` で row 単位に dry-run 検証する
- `seed_legacy_proofs` で proof ledger と dispatch ledger を同時に投入する
- seed される proof status は常に `pending`
- seed 後の approve / reject は既存の proof inbox を使う
- `artifactPath` は configured proof output dir 配下の PDF のみ許可する
- 既存 `proofJobId` / `jobLineageId` と衝突する seed は拒否する

## Consequences

- 既存 proof を本印刷に再利用できる
- 直接 approved seed を禁止することで監査線を維持できる
- 移行時の operator 入力は増えるが、誤投入による誤印刷リスクを下げられる

