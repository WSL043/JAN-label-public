# adr

重要な設計判断を時系列で残すためのディレクトリです。

## ルール

- 1 ファイル 1 判断
- 既存 ADR の本文を上書きして意味を変えない
- 状態は `Accepted` / `Superseded` / `Deprecated` を明記する
- 撤回や置換があった場合は、新しい ADR から古い ADR を参照する

## 既存 ADR

- `0001-print-core-first.md`
- `0002-svg-pdf-first.md`
- `0003-zint-for-barcode-generation.md`
- `0004-template-spec-json.md`
