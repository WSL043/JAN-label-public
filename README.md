# label-platform

印刷コア先行で設計する SKU/JAN ラベル生成・印刷システムのモノレポです。  
Windows を最初の実行環境に据えつつ、将来的に macOS / Linux / iOS / Android から同じ印刷コアへ接続できる構成を前提にしています。

## Repository Layout

- `apps/admin-web`: React + TypeScript の管理 UI
- `apps/desktop-shell`: Tauri 2 を採用する場合の Windows 配布シェル
- `crates/*`: Rust 製の印刷コア、業務ルール、レンダリング、プリンタアダプタ
- `packages/job-schema`: UI と周辺ツールで共有するジョブ型
- `packages/fixtures`: ゴールデン出力、サンプル入力、検証用 CSV/JSON
- `docs`: アーキテクチャ、印刷パイプライン、GitHub 運用、リスク管理

## Quick Start

1. `rustup toolchain install stable`
2. `pnpm install`
3. `cargo test`
4. `pnpm fixture:validate`
5. `pnpm --filter @label/admin-web dev`

Windows の初期セットアップは [docs/windows-bootstrap.md](docs/windows-bootstrap.md) を参照してください。

## Project Memory

- [AGENTS.md](AGENTS.md)
- [docs/handoff/current-state.md](docs/handoff/current-state.md)
- [docs/todo/active.md](docs/todo/active.md)
- [docs/known-issues.md](docs/known-issues.md)
- [docs/adr/README.md](docs/adr/README.md)
