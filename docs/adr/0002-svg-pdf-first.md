# 0002 SVG/PDF First

- Status: Accepted
- Date: 2026-04-14

## Context

最初から ZPL / TSPL / QZ / Windows spooler に広げると、実装と検証の軸がぶれやすいです。  
まずは人間が目視で検証しやすく、golden test もしやすい正規出力が必要です。

## Decision

最初の正規出力は `SVG` と `PDF` に固定する。  
専用プリンタ言語や QZ は後から adapter として追加する。

## Consequences

- ゴールデンテストをシンプルに保てる
- PDF 仮想プリンタで 100% スケール検証がしやすい
- native printer adapter の導入時期を後ろにずらせる

## Rejected Alternatives

- 初期段階から ZPL / TSPL を主出力にする
- ブラウザ印刷を正規出力にする

