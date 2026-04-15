# print-pipeline

## 1. 現在のフロー

1. `admin-web` で parent SKU / SKU / JAN / qty / brand / template / printer profile を組む
2. CSV / XLSX を取り込む場合は alias mapping で列を結び、`enabled` / `template` / `printer_profile` の列上書きを吸収する
3. `admin-web` は row を `ready / pending / error` に振り分ける
4. ready row を snapshot して manual / batch submit に流す
5. desktop-shell 経由で `dispatch_print_job` を呼ぶ
6. Rust 側で JAN 正規化、proof gate、printer route、adapter 制約、audit 記録を処理する

## 2. print gate

print 実行時の条件:

- `sourceProofJobId` がある
- approved proof ledger に該当 proof がある
- proof dispatch ledger に該当 proof dispatch がある
- `templateVersion + sku + brand + jan(normalized) + qty + lineage` が approved proof と一致する
- approved proof ledger の `artifactPath` が実在する

`allowWithoutProof` は引き続き bypass 用には使わない。

## 3. proof フロー

- proof submit は PDF route で出す
- desktop-shell は proof dispatch を `dispatch-ledger.json` に記録する
- 同時に proof record を `proof-ledger.json` に `pending` で登録する
- `admin-web` の proof inbox から `approve` / `reject` を実行する
- approved proof は UI から print form に pin できる

## 4. legacy proof seed フロー

ledger 導入前の proof PDF 用:

1. `admin-web` の legacy proof seed UI で CSV / XLSX を読み込む
2. `validate_legacy_proof_seed` で row 単位の妥当性を確認する
3. `seed_legacy_proofs` で pending proof と proof dispatch を同時に seed する
4. seed 後は通常の proof inbox で approve する

制約:

- `artifactPath` は configured proof output dir 配下の PDF のみ許可
- 既存 `proofJobId` / `jobLineageId` と衝突する seed は拒否
- 直接 approved では投入しない

## 5. bridge status

`print_bridge_status` は次を返す:

- available adapters
- resolved zint path
- proof / print / spool output dirs
- active print adapter
- windows printer name
- `warningDetails[]`
  - `code`
  - `severity`
  - `message`
- 旧互換の `warnings[]`

`admin-web` は `warningDetails` を正とし、`severity === "error"` の warning がある間は submit を block する。

## 6. Excel / CSV 取り込みの扱い

- CSV / XLSX は strict DB 前処理なしで扱える
- header は alias mapping で吸収する
- XLSX 数値セル JAN は安全側に倒す
  - scientific / decimal / ambiguous 12-digit numeric JAN は error
  - 13-digit numeric JAN は warning
- JAN の最終正規化と検証は Rust 側で行う

## 7. まだ未完の部分

- audit retention / export / backup
- 実機プリンタでの printer matrix
- template authoring の preview / proof 設計の仕上げ
- multi-host / shared storage を前提にした audit / proof storage

