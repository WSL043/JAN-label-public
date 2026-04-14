# mvp-backlog

## Epic 1. 印刷コア基盤

1. `domain` crate で JAN 正規化と SKU モデルを確定する
2. `importer` crate で正規列バリデーションを実装する
3. `render` crate で 1 テンプレート分の SVG 出力を安定化する
4. `barcode` crate で Zint CLI アダプタを実装する
5. `printer-adapters` crate で PDF 出力アダプタを実装する

## Epic 2. Windows MVP

1. `admin-web` で SKU 検索画面を作る
2. 数量展開 UI を作る
3. 印刷プレビュー画面を作る
4. 再印刷画面と履歴画面を作る
5. `print-agent` と UI のローカル接続方式を決める

## Epic 3. 品質保証

1. SVG ゴールデンテストを増やす
2. CSV fixture validation を厳格化する
3. docs-guard を CI 必須化する
4. 実機プリンタで 50mm x 30mm の測定表を作る
5. バーコード読取試験を定義する

## Epic 4. GitHub ガードレール

1. `CODEOWNERS` を導入する
2. PR template を導入する
3. issue forms を導入する
4. label sync と path-based labeler を導入する
5. required checks を ruleset に反映する

## 最初の 10 issue

1. `domain`: 12 桁 JAN 自動補完と 13 桁検証
2. `importer`: canonical column validator
3. `render`: basic-50x30 SVG fixture 追加
4. `barcode`: Zint CLI invocation 実装
5. `printer-adapters`: PDF adapter 実装
6. `audit-log`: lineage ID と reprint event 追加
7. `admin-web`: ジョブ作成フォーム
8. `admin-web`: プレビュー画面
9. `docs`: printer matrix 雛形作成
10. `.github`: docs-guard を ruleset 必須チェックへ登録

