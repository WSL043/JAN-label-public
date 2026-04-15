# print-pipeline

## 1. 現在の主フロー

1. `admin-web` で parent SKU / SKU / JAN / qty / brand / template / printer profile を入力する
2. CSV / XLSX を読み込み、alias mapping で列を合わせる
3. row を `ready / pending / error` に仕分ける
4. ready row を snapshot して manual / batch submit に送る
5. `desktop-shell` の `dispatch_print_job` を呼ぶ
6. `desktop-shell` で packaged template catalog、proof gate、audit writable を確認する
7. Rust 側で JAN 正規化、lineage 正規化、printer route、adapter、audit 記録を処理する

## 2. print gate

print 実行時の必須条件:

- `sourceProofJobId` がある
- approved proof ledger に対象 proof がある
- proof dispatch ledger に対応する proof dispatch がある
- packaged template catalog に対象 `template_version` がある
- `templateVersion + sku + brand + jan(normalized) + qty + lineage` が approved proof と一致する
- approved proof ledger の `artifactPath` が proof output dir 配下の non-empty PDF として実在し、PDF header を読める
- audit ledger が writable である

lineage ルール:

- `jobLineageId` 未指定なら `desktop-shell` が approved proof lineage を補完する
- explicit `jobLineageId` は approved proof lineage と一致しない限り reject する
- explicit `reprintOfJobId` も approved proof lineage と一致しない限り reject する

`allowWithoutProof` は bypass 用には使わない。

## 3. proof フロー

- proof submit は PDF route
- `desktop-shell` は proof dispatch を `dispatch-ledger.json` に記録する
- 同時に proof record を `proof-ledger.json` に `pending` で登録する
- `admin-web` の proof inbox から `approve` / `reject` を行う
- approved proof は UI から print form に pin できる

## 4. audit export / retention

- `export_audit_ledger` は `all / dispatch / proof` scope の snapshot を返す
- `admin-web` はこれを JSON download にする
- `trim_audit_ledger` は `maxAgeDays` / `maxEntries` / `dryRun` を受ける
- trim は proof record と proof dispatch の依存を壊さないように keep set を補強する
- actual trim 時は removed records を `audit/backups/` 配下の single JSON bundle として先に保存する

## 5. legacy proof seed フロー

1. `admin-web` の legacy proof seed UI で CSV / XLSX を読み込む
2. `validate_legacy_proof_seed` で row 単位の妥当性を確認する
3. `seed_legacy_proofs` で pending proof と proof dispatch を同時に seed する
4. seed 後は通常の proof inbox で approve する

制約:

- `artifactPath` は configured proof output dir 配下の PDF のみ許可
- 既存 `proofJobId` / `jobLineageId` と衝突する seed は拒否
- seed で approved にはしない

## 6. bridge status

`print_bridge_status` は以下を返す:

- available adapters
- resolved zint path
- proof / print / spool output dirs
- audit log / audit backup dirs
- active print adapter
- windows printer name
- `warningDetails[]`
  - `code`
  - `severity`
  - `message`
- 互換用の `warnings[]`

`admin-web` は `warningDetails` を正とし、`severity === "error"` の warning があれば submit を block する。

## 7. template authoring / preview

現在の authoring 導線:

1. `admin-web` は desktop-shell から packaged template catalog を読み、選択可能な template を同期する
2. `admin-web` の structured template editor で page / border / field を編集する
3. local canvas preview で近似確認する
4. 必要なら `preview_template_draft` を呼んで Rust renderer の SVG preview を確認する
5. live JSON の `template_version` が packaged catalog に無い場合は mismatch を表示する
6. 問題がなければ template asset / JSON を export して引き継ぐ

重要:

- local canvas preview は近似表示
- Rust preview は live template JSON を描画する
- desktop template catalog が dispatch で使える `template_version` の正になる
- unknown live `template_version` がある draft は queue / manual / batch submit を止める
- ただし proof / print dispatch はまだ packaged manifest の `template_version` を使う
- つまり authoring preview と本番 dispatch の write-back はまだ未接続

## 8. Excel / CSV 取り込みの方針

- CSV / XLSX は strict DB 連携なしで使えるようにする
- header は alias mapping で吸収する
- XLSX 数値セル JAN は release 安全側に倒す
  - scientific / decimal / ambiguous 12-digit numeric JAN は error
  - 13-digit numeric JAN は warning
- JAN の最終正規化と検証は Rust 側で行う

## 9. まだ未接続の部分

- 実機プリンタの printer matrix
- authored template の manifest write-back
- authored template を proof / print dispatch に反映する経路
