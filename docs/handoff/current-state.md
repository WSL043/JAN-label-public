# current-state

- Updated: 2026-04-15
- Branch: `codex/release-bridge-proof-hardening`
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
- `apps/admin-web` を運用寄りコンソール化（単品入力＋複数行入力＋レビューキュー＋Ready/Pending 分離）
- `apps/admin-web` の template asset export / import、localStorage 復元、CSV/XLSX 読み込み
- `apps/admin-web` の desktop-shell bridge status 表示、manual / batch submit、行ごとの `enabled` / `template` / `printer_profile` 上書き
- `apps/admin-web` の importer 契約寄せ
  `enabled=true/false` 厳格化、JAN digits-only 化、XLSX lazy import、failed retry 時の jobId 再採番、batch submit の直列化
- `apps/desktop-shell` の最小 Tauri shell scaffold
- `apps/desktop-shell` の Tauri bridge
  `dispatch_print_job`, `print_bridge_status`
  と安全な環境変数 fallback
- `apps/desktop-shell` の proof gate hardening
  `printerProfile` ごとの adapter 解決、source proof PDF 実在確認、`allowWithoutProof` の実運用無効化
- `desktop-shell-windows` CI と tag release 時の Windows installer build 経路
- `crates/audit-log` / `crates/print-agent` の lineage / reprint モデル
- `crates/printer-adapters` の PDF proof adapter と Windows spooler skeleton
- `crates/importer` の `enabled` 文字列処理と実運用向け入力許容を強化
- `crates/importer` の business alias ヘッダ解決、match quality、業務 fixture を追加
- `crates/print-agent` の入力検証を実装（空値拒否、`qty==0` 拒否、JAN 正規化、未対応アダプタ拒否）
- `crates/print-agent` と `packages/job-schema` の dispatch request / result 契約を追加し、proof gate を強化
- `crates/render` の PDF 検証を構造検証へ拡張（ヘッダ、xref、MediaBox、stream geometry、文字列エスケープ）
- `crates/render` の SVG attribute 安全化と template color 制約を追加
- `packages/templates` の template schema / manifest schema と、manifest 駆動の template 解決を追加
- `crates/printer-adapters` の artifact/出力パス/メディアタイプのガードを追加
- `scripts/validate-fixtures.mjs` の fixture 検証強化（schema、dispatch 契約、template schema、業務 fixture、PDF 構造）
- GitHub 上の Codex 連携
  `Codex PR Review`, `Codex PR Comment`, `Codex CI Triage`, `Codex Maintenance`, `Codex CI Autofix`
- `docs/printer-matrix/2026-04-15-pdf-proof-baseline.md` に初回 baseline を記録
- `v0.1.1` GitHub Release を発行し、Windows installer asset
  `JAN-Label_0.1.1_windows_x64-setup.exe`
  を添付済み
- GitHub-side Codex との協働運用（クラウド側実行との役割分担）を前提に、`T-030` 以降の運用を進行中

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

- `admin-web` の submit は `desktop-shell` 経由の Tauri invoke を前提に接続済みだが、browser 単体では preview-only のまま
- bridge status / warning は UI で高リスク時に submit block できるが、warning code / severity の構造化と監査検索との連携は未実装
- proof 承認フロー（proof 保存、承認メモ、却下/再作成、承認なし本印刷の運用状態遷移）は未実装
- 監査ログの永続化、検索 UI、再印刷 UI は未接続
- ラベルソフトの基本価値に必要な「ラベル製作コア（テンプレート仕様、要素配置、データ紐付け、版管理）」が未実装
  `packages` / `render` 主導の label design / template workflow 最小セットとして扱う
- 実運用安全ルール（重複ジョブ防止、停止/再開、エラー時停止）は未実装
- GitHub Actions repository secret `OPENAI_API_KEY` を設定し、cloud-side Codex 実行を有効化
- `docs/printer-matrix/` に物理プリンタ実測を追加
- phase 3 の Codex 自動化
  self-hosted runner / webhook

## 4. 次の安全な一手

1. `admin-web` の queued row 実行状態を `ready / submitting / submitted / failed` からさらに監査検索へ繋がる形で仕上げる
2. proof ワークフローを実装し、snapshot・承認・保存・却下/再作成を運用可能にする
3. template schema / template asset を基準に、ラベル製作コアのデータ紐付け・版管理・要素配置を `packages` / `render` 主導で拡張する
4. 3 の成果を活用して、テンプレート編集画面からのプレビュー / proof 生成・差分比較を実装する
5. 監査ログの永続化と検索 UI を接続し、再印刷トレース・監査証跡を実運用で追跡できるようにする
6. 現場安全ルールを実装する（承認必須、重複ジョブ防止、エラー時停止、再試行制御）
7. repository secret に `OPENAI_API_KEY` を設定し、`Codex PR Review` / `Codex Maintenance` / `Codex CI Autofix` を cloud 実行へ切り替える
8. 実機プリンタ + スキャナで 1 件測定し、`docs/printer-matrix/` に物理実測を追記する

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
- `admin-web` は desktop-shell 経由なら submit できるが、browser 単体では preview-only であり、監査永続化と proof 運用は未完成
- `admin-web` は 12/13 桁 JAN を digits-only で submit し、最終正規化は Rust 前提とする。proof 承認と監査永続化が未完成のため運用制約は残る
- ラベルテンプレート作成は schema / asset の基礎まで進んだが、フィールドベースの本格的な label design、データソース binding、proof 承認は未完成
- `allowWithoutProof` は proof 承認ワークフローが実装されるまで無効化している。現状の `sourceProofJobId` 検証は proof PDF 実在確認までで、承認メモ/却下状態の永続化は未実装
- XLSX 取り込みは lazy-load 化済みだが、JAN を数値セルで持つ Excel は先頭ゼロや表示形式の崩れを招くため、現時点では text 化前提で運用する
- `print-agent` は `Pdf` と `WindowsSpooler` のみ接続済みで、`Zpl` / `Tspl` / `Qz` は現状 reject する
- 一部のローカル Windows 環境では `link.exe` 不在により `cargo test --workspace` が失敗する
- ローカル Windows に Build Tools がなくても、`desktop-shell-windows` と `Release` workflow は GitHub-hosted `windows-latest` を正とする
- `pnpm/action-setup@v4`, `dorny/paths-filter@v3`, `actions/upload-artifact@v4` は Node 20 deprecation 警告を出す
- GitHub Actions の `OPENAI_API_KEY` secret が未設定だと Codex automation は fallback のみになる
