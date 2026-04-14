# 0001 Print Core First

- Status: Accepted
- Date: 2026-04-14

## Context

この製品の難所は UI 共有ではなく、印刷寸法、JAN/EAN-13 の正確性、プリンタ差異、監査可能性です。  
UI フレームワークを製品の中心に置くと、後から shell を差し替えても印刷ロジックが散らばるリスクがあります。

## Decision

製品の中心は Rust 製の印刷コアに置く。  
UI はジョブ作成と監査を担当し、印刷そのものの正しさは `crates/*` に集約する。

## Consequences

- Windows 以外への拡張時に shell を差し替えやすい
- UI 側へ JAN 正規化ロジックを持ち込みにくくなる
- テストの重点は UI より `domain` / `render` / `printer-adapters` に置く

## Rejected Alternatives

- Tauri first
- 単一 UI フレームワーク中心
- ブラウザ直接印刷中心

