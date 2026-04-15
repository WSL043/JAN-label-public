# current-state

- Updated: 2026-04-15
- Branch: `codex/release-bridge-proof-hardening`
- Release base: `v0.1.1` (`0b95f41`)
- Active PR: `#25`

## 1. いま動いているもの

- Rust core:
  - `domain` の JAN 正規化
  - `barcode` の Zint CLI adapter
  - `render` の SVG / PDF 出力と golden 検証
  - `print-agent` の dispatch / proof gate / lineage
  - `printer-adapters` の PDF と Windows spool staging
  - `audit-log` の lineage / proof status / audit search 型
- `admin-web`:
  - manual draft / batch queue / retry
  - template asset export / import
  - CSV / XLSX import と alias mapping
  - proof inbox / audit search / approved proof pinning
  - legacy proof seed UI
  - bridge status と構造化 warning 表示
- `desktop-shell`:
  - `dispatch_print_job`
  - `print_bridge_status`
  - `search_audit_log`
  - `approve_proof` / `reject_proof`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`

## 2. 今回の追加分

- strict proof-to-print gate は `templateVersion + sku + brand + jan(normalized) + qty + lineage` を見る
- print 時の proof artifact 存在確認は、固定ファイル名ではなく approved proof ledger の `artifactPath` を使う
- bridge warning は `warningDetails[]` に `code / severity / message` を持つ
- `admin-web` は `warningDetails` があればそれを優先し、旧 `warnings: string[]` は fallback として扱う
- XLSX 数値セル JAN は release 安全側に倒した
  - scientific / decimal / ambiguous 12-digit numeric JAN は error
  - 13-digit numeric JAN は warning
- legacy proof は pending seed のみ許可
  - dispatch ledger と proof ledger を同時に投入する
  - 既存 lineage / proofJobId と衝突する seed は拒否する

## 3. 検証状況

通過:

- `pnpm fixture:validate`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- `cargo fmt --all --check`
- `cargo clippy --workspace --all-targets -- -D warnings`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`

注意:

- `pnpm format:check` は generated `target-*` ディレクトリが workspace 配下にあると失敗する。作業用 target を作った場合は削除してから実行する
- `cargo test --workspace` はローカル Windows で `print-agent` 実行時に `os error 5` が出ることがある。desktop-shell 単体テストと compile は通っているが、workspace 全体の実行は環境要因で不安定

## 4. 次の主タスク

1. `T-028`: audit retention / export / 運用導線の整備
2. `T-032`: template authoring core の強化
3. `T-033`: preview / proof 設計の仕上げ
4. `T-029`: 停止 / 再開 / エラー時運用の文書化

## 5. 外部依存の blocker

- `T-030`: GitHub repository secret `OPENAI_API_KEY`
- `T-031`: 実機プリンタ測定と `docs/printer-matrix/` 反映

