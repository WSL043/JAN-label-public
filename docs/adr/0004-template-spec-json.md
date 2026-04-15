# 0004 Template Spec JSON

- Status: Accepted
- Date: 2026-04-15

## Context

BarTender 的な最小ラベル製作機能を追加するには、ラベルレイアウトを UI の一時状態ではなく、
版管理可能な仕様アセットとして持つ必要があります。  
このリポジトリは `print-core-first` と `svg-pdf-first` を採っているため、
レイアウトの正は `admin-web` ではなく `render` / `packages` 側に置くべきです。

## Decision

ラベルテンプレートは `packages/templates/*.json` の versioned JSON spec として保持する。  
`template-manifest.json` で利用可能な版を列挙し、`render` は manifest 経由でテンプレートを解決する。

最初の label authoring は次の境界で実装する。

- `packages/templates`
  仕様ファイルと manifest を保持する
- `crates/render`
  JSON spec を正として SVG/PDF を描画する
- `apps/admin-web`
  軽量テンプレート編集、import/export、preview/proof 導線を担当する

## Consequences

- テンプレートは repo 内で差分管理しやすくなる
- fixture / golden / template spec を同時に検証できる
- UI は設計補助に留まり、印刷レイアウトの正を持たない
- 将来の複数テンプレート対応では manifest と schema の厳密検証が重要になる

## Rejected Alternatives

- UI ローカル状態をラベル仕様の正にする
- Rust コードへレイアウト定数を埋め込み続ける
- 最初から自由配置 WYSIWYG/DTP を主軸にする
