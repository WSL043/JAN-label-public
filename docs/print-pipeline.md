# print-pipeline

## 1. 基本フロー

1. UI で parent SKU / SKU / JAN / 数量 / brand / template / printer profile を選ぶ
2. `job-schema` に沿ってドラフトを作る
   `admin-web` は canonical な 13 桁 JAN だけを preview に載せ、12 桁補完は Rust 側へ委譲する
3. `print-agent` が JAN と importer 正規化ルールを検証する
4. `barcode` が Zint へ描画依頼する
5. `render` が printer profile に応じて SVG/PDF を生成する
6. `printer-adapters` が proof file や Windows spool staging file へ送信する
7. `audit-log` が lineage / parent job / reason を含む実行結果を記録する

## 2. MVP の出力優先順位

- 第 1 段階
  SVG
- 第 2 段階
  PDF proof
- 第 3 段階
  Windows spool staging
- 第 4 段階
  ZPL / TSPL / QZ

## 3. 擬似コード

```text
draft = ui.create_job()
normalized = domain.normalize_jan(draft.jan)
validated = importer.validate_columns(input_headers)
validated_row = importer.validate_row(row_number, row_values)
barcode_artifact = zint.render(normalized)
label_artifact = render.by_profile(template_version, printer_profile)
receipt = adapter.submit(label_artifact)
audit.record(job_id, lineage_id, parent_job_id, actor, reason, timestamp)
```

## 4. ゴールデンテスト

- `packages/fixtures/golden/*.svg`
  SVG の期待出力
- `packages/fixtures/golden/*.pdf`
  PDF の期待出力
- `render` crate のテストで fixture と完全一致比較
- 現在の PDF は deterministic な最小 writer を使うため完全一致比較する
- 将来メタデータや外部ライブラリ由来の差分が入る場合は canonical compare を導入する

## 5. 100% スケール固定の検証手順

1. PDF 仮想プリンタで A4 に出力する
2. 印刷ダイアログで拡大縮小を `100%` 固定にする
3. 出力したラベルの幅と高さを定規とノギスで測る
4. 物理ラベルプリンタで同じテンプレートを印刷する
5. バーコードリーダーで読取確認する
6. 測定結果を `docs/printer-matrix/` に残す

## 6. 将来の adapter 拡張原則

- adapter はレンダリングロジックを持たない
- adapter は job ではなく render 済み artifact を受け取る
- adapter 追加時は fixture、golden、printer profile、docs を同時更新する
