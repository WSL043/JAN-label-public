# print-pipeline

## 1. 現在の実装済みフロー

1. `admin-web` で parent SKU / SKU / JAN / 数量 / brand / template / printer profile を選ぶ
2. UI は template schema / template asset を扱い、review queue を組み立てて `Ready` と `Pending` を分ける
   `admin-web` は 12 桁と 13 桁の JAN を受け取り、最終正規化と検証は Rust 側へ委譲する
3. CSV / XLSX は厳格 alias match で列マッピングされ、業務ヘッダ揺れ、`enabled=true/false`、行ごとの `template` / `printer_profile` 上書きを扱う
4. snapshot を作り、proof route / live route を明示した payload preview を確認する
5. `desktop-shell` 上では Tauri invoke で `dispatch_print_job` を呼び、manual draft / queuedRows の submit 結果と bridge status を受け取れる。job ごとの `printerProfile` を優先し、print 時は source proof PDF 実在確認と approved proof ledger 照合を行う
6. `desktop-shell` は local audit store に `dispatch-ledger.json` と `proof-ledger.json` を保持し、proof status を `pending / approved / rejected / superseded` で管理する
7. `admin-web` は proof inbox / audit search から pending proof の approve / reject と、approved proof の `sourceProofJobId` 反映を行える
8. `print-agent` / `render` / `printer-adapters` / `audit-log` は crate 単位で個別テストされている

現時点で submit は `desktop-shell` 経由なら接続済みです。  
一方で browser 単体は preview-only のままで、proof と print 対象の厳密整合、audit ledger の長期運用、bridge warning の構造化は未完成です。

## 2. 接続後の目標フロー

1. `admin-web` で review 済み job を submit する
2. `desktop-shell` が bridge status を返し、Tauri invoke で `print-agent` へ request を渡す
3. `desktop-shell` が requested `printerProfile` と proof artifact 実在を確認し、必要時のみ `allowWithoutProof` policy を許可する
4. `print-agent` が JAN と importer 正規化ルール、proof gate を検証する
5. `barcode` が Zint へ描画依頼する
6. `render` が template version と printer route に応じて SVG/PDF を生成する
7. `printer-adapters` が proof file や Windows spool staging file へ送信する
8. `audit-log` が lineage / parent job / reason を含む実行結果を永続化する
9. `admin-web` が submit / completed / failed / reprinted を検索・再印刷できるようにする

## 3. MVP の出力優先順位

- 第 1 段階
  SVG
- 第 2 段階
  PDF proof
- 第 3 段階
  Windows spool staging
- 第 4 段階
  ZPL / TSPL / QZ

現状で接続済みなのは `PDF proof` と `Windows spool staging` の骨格です。  
`ZPL / TSPL / QZ` は printer profile 列挙上の将来候補であり、`print-agent` ではまだ reject します。

## 4. 擬似コード

```text
queue_item = ui.review_and_mark_ready()
template_asset = ui.load_template_asset_or_schema()
draft = ui.snapshot_ready_jobs(template_asset)
validated_row = importer.validate_row(row_number, row_values)
dispatch_request = ui.to_dispatch_request(draft)
bridge_status = tauri.print_bridge_status()
normalized = domain.normalize_jan(dispatch_request.jan)
barcode_artifact = zint.render(normalized)
label_artifact = render.by_profile(template_version, printer_profile)
receipt = adapter.submit(label_artifact)
audit.record(job_id, lineage_id, parent_job_id, actor, reason, timestamp)
```

現在は `dispatch_request = ui.to_dispatch_request(draft)` と `desktop-shell` 側の submit までは接続済みです。  
それ以降の proof 承認状態遷移、audit 永続化、browser 単体の bridge 代替は未接続です。

## 5. proof / 承認フローの現状

- `admin-web` は review queue と snapshot を持つ
- PDF proof route は UI 上で明示される
- `admin-web` と `print-agent` は `sourceProofJobId` / `allowWithoutProof` の gate を共有する
- `desktop-shell` は proof dispatch 成功時に pending proof を ledger 登録し、print 時は source proof PDF の実在と approved proof ledger 記録を確認する
- `admin-web` は proof inbox から pending proof の approve / reject、approved proof の print form 反映を行える
- `allowWithoutProof` は proof 承認ワークフロー完了まで拒否する
- まだ未実装のもの:
  - print 対象と approved proof の lineage / template / 対象整合の強制
  - 承認履歴の多段保持
  - legacy proof PDF の ledger 移行
  - 却下 / 再作成専用 UI と再印刷 UI

そのため現在の運用前提は「proof 承認の最小導線まで接続済み」だが、  
本番向けの厳密照合と長期監査運用は次段で仕上げる。

## 6. ゴールデンテスト

- `packages/fixtures/golden/*.svg`
  SVG の期待出力
- `packages/fixtures/golden/*.pdf`
  PDF の期待出力
- `render` crate のテストで fixture と完全一致比較する
- 追加で PDF の header / xref / MediaBox / content stream / 文字列エスケープを検証する
- 将来メタデータや外部ライブラリ由来の差分が入る場合は canonical compare を導入する

## 7. 100% スケール固定の検証手順

1. PDF 仮想プリンタで A4 に出力する
2. 印刷ダイアログで拡大縮小を `100%` 固定にする
3. 出力したラベルの幅と高さを定規とノギスで測る
4. 物理ラベルプリンタで同じテンプレートを印刷する
5. バーコードリーダーで読取確認する
6. 測定結果を `docs/printer-matrix/` に残す

## 8. 監査ログの現状

- `print-agent` は lineage / parent job / reason を含む event record を生成できる
- `desktop-shell` は local JSON ledger として `dispatch-ledger.json` / `proof-ledger.json` を保持する
- `search_audit_log` は `searchText` / `limit` で recent dispatch と proof 状態を返す
- `admin-web` は proof inbox / audit search UI で local ledger を検索できる
- 未実装:
  - 再印刷 UI
  - ledger のローテーション / バックアップ
  - multi-host / shared storage 前提の同期
  - approved proof と print 対象の厳密整合追跡

## 9. 将来の adapter 拡張原則

- adapter はレンダリングロジックを持たない
- adapter は job ではなく render 済み artifact を受け取る
- adapter 追加時は fixture、golden、printer profile、docs を同時更新する

## 10. ラベル製作機能の最小スライス

- BarTender 的なラベル製作機能はこのリポジトリでも必要とみなす
- ただし最初の実装は自由配置 DTP ではなく、`packages` 配下の template schema / template asset と `render` の仕様駆動化から始める
- `admin-web` はレイアウトロジックを所有せず、軽量テンプレート編集 UI、asset export / import、preview / proof 導線を担当する
- したがって label authoring の主責務は `render` / `domain` / `packages` に置き、UI はその編集・確認レイヤに留める
