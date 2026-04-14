# current-state

- Updated: 2026-04-15
- Branch: `main`
- Release tag: `v0.1.1` (`0b95f41`)
- Remote: `origin/main`

## 1. 完了済み

- Rust workspace と pnpm workspace の土台
- `domain` / `importer` / `render` / `print-agent` などの最小 crate
- React 管理 UI の最小骨格
- GitHub Actions
  `CI`, `Pull Request Labeler`, `Sync Labels`, `Release`
- issue forms, labels, PR template, CODEOWNERS
- Windows 開発環境の bootstrap スクリプト
- `crates/barcode` の Zint CLI adapter と fake executable テスト
- `crates/render` の PDF 出力ルートと golden fixture
- `crates/importer` の行単位バリデーション
- `apps/admin-web` のジョブ作成フォーム
- `apps/desktop-shell` の最小 Tauri shell scaffold
- `desktop-shell-windows` CI と tag release 時の Windows installer build 経路
- `crates/audit-log` / `crates/print-agent` の lineage / reprint モデル
- `crates/printer-adapters` の PDF proof adapter と Windows spooler skeleton
- GitHub 上の Codex 連携
  `Codex PR Review`, `Codex PR Comment`, `Codex CI Triage`, `Codex Maintenance`, `Codex CI Autofix`
- `docs/printer-matrix/2026-04-15-pdf-proof-baseline.md` に初回 baseline を記録
- `v0.1.1` GitHub Release を発行し、Windows installer asset
  `JAN-Label_0.1.1_windows_x64-setup.exe`
  を添付済み

## 2. 直近で使っている確認セット

```powershell
pnpm fixture:validate
pnpm format:check
pnpm lint
pnpm typecheck
cargo fmt --all --check
cargo clippy --workspace --all-targets -- -D warnings
cargo test --workspace
```

GitHub Actions の最新成功 run:

- `CI` run `24419371752` on `main`
- `Release` run `24419902840` on `v0.1.1`
- GitHub Release
  `https://github.com/WSL043/JAN-label/releases/tag/v0.1.1`

ローカル Windows で `link.exe` がない場合、`cargo test --workspace` の最終判定は CI を正とする。

## 3. 未完了

- GitHub Actions repository secret `OPENAI_API_KEY` を設定し、cloud-side Codex 実行を有効化
- `docs/printer-matrix/` に物理プリンタ実測を追加
- phase 3 の Codex 自動化
  self-hosted runner / webhook

## 4. 次の安全な一手

1. repository secret に `OPENAI_API_KEY` を設定し、`Codex PR Review` / `Codex Maintenance` / `Codex CI Autofix` を cloud 実行へ切り替える
2. 実機プリンタ + スキャナで 1 件測定し、`docs/printer-matrix/` に物理実測を追記する
3. persistent な半常駐運用が必要なら `T-012` の self-hosted runner / webhook 化に進む

## 5. 触る時の注意

- UI 側に JAN 正規化を実装しない
- printer adapter に render 責務を混ぜない
- fixture 変更だけで済ませず docs を更新する
- 監査ログを単純な成功履歴に縮退させない
- `v0.1.1` は PDF proof baseline で出しているため、物理プリンタ検証は未完了の別作業として扱う

## 6. 現在の制約

- GitHub branch protection / ruleset は current plan 制約で未適用
- GitHub environments はまだ未作成
- Zint は repo / CI にまだ組み込んでいない
- 一部のローカル Windows 環境では `link.exe` 不在により `cargo test --workspace` が失敗する
- ローカル Windows に Build Tools がなくても、`desktop-shell-windows` と `Release` workflow は GitHub-hosted `windows-latest` を正とする
- `pnpm/action-setup@v4`, `dorny/paths-filter@v3`, `actions/upload-artifact@v4` は Node 20 deprecation 警告を出す
- GitHub Actions の `OPENAI_API_KEY` secret が未設定だと Codex automation は fallback のみになる
