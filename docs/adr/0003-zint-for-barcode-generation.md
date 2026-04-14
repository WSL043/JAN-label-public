# 0003 Zint For Barcode Generation

- Status: Accepted
- Date: 2026-04-14

## Context

JAN/EAN-13 の検証ロジック自体は比較的小さいですが、描画ロジックまで自前で持つと保守負債になりやすいです。  
今回必要なのはバーコード理論の研究ではなく、安定して読めるラベルを出すことです。

## Decision

JAN の入力正規化と checksum 検証は自前で持つ。  
バーコード描画は Zint を前提にし、`crates/barcode` から呼び出す。

## Consequences

- 描画品質を成熟部品に委ねられる
- Zint CLI / library 境界のテストが必要になる
- 開発環境と CI に Zint 導入方針を追加する必要がある

## Rejected Alternatives

- 描画ロジックの完全自前実装
- UI 側ライブラリへの依存

