# current-state

- Updated: 2026-04-15
- Branch: `codex/release-bridge-proof-hardening`
- Release base: `v0.1.1` (`0b95f41`)
- Active PR: `#25`

## 1. いま動いているもの

- Rust core
  - `domain` の JAN 正規化
  - `barcode` の Zint CLI adapter
  - `render` の SVG / PDF 出力
  - `print-agent` の dispatch / proof gate / lineage
  - `printer-adapters` の PDF と Windows spool staging
  - `audit-log` の lineage / proof status / audit search
- `admin-web`
  - manual draft / batch queue / retry
  - template asset export / import
  - CSV / XLSX import と alias mapping
  - proof inbox / audit search / approved proof pinning
  - legacy proof seed UI
  - bridge status と structured warning 表示
  - structured template editor
  - local canvas preview
  - Rust renderer preview button
- `desktop-shell`
  - `dispatch_print_job`
  - `print_bridge_status`
  - `search_audit_log`
  - `approve_proof` / `reject_proof`
  - `validate_legacy_proof_seed` / `seed_legacy_proofs`
  - `preview_template_draft`

## 2. 今回追加したもの

- `render`
  - inline template source を parse して preview できる
  - `border.visible` を SVG / PDF に反映
  - background color / border color / field color を SVG / PDF に反映
  - golden SVG / PDF を再生成
- `desktop-shell`
  - `preview_template_draft` で live template JSON を Rust renderer に通せる
- `admin-web`
  - structured template editor の CSS と workbench レイアウトを実装
  - local canvas と Rust preview を並べて確認できる
  - `parent_sku` は preview-only と明示
  - template editor は packaged manifest にはまだ書き戻らないことを明示
  - batch retry が `submitted` 行を再送してしまう不具合を修正
  - template validation に duplicate / out-of-bounds / unsupported placeholder / preview-only placeholder を追加

## 3. 現在の release 境界

- strict proof-to-print gate は `templateVersion + sku + brand + jan(normalized) + qty + lineage`
- print 時の proof artifact 確認は approved proof ledger の `artifactPath` を使う
- `warningDetails[]` の `code / severity / message` を UI が正として扱う
- Rust preview は live template JSON を描画する
- ただし proof / print dispatch はまだ packaged manifest の `template_version` を使う

## 4. 検証状況

通過:

- `pnpm fixture:validate`
- `pnpm format:check`
- `pnpm lint`
- `pnpm typecheck`
- `pnpm --filter @label/admin-web build`
- `cargo fmt --all --check`
- `cargo clippy --workspace --all-targets -- -D warnings`
- `cargo test --workspace`
- `cargo test --manifest-path apps/desktop-shell/src-tauri/Cargo.toml`

補足:

- `cargo test --workspace` はローカル Windows で `os error 5` が稀に揺れる既知事象がある

## 5. 次の主タスク

1. `T-028`: audit retention / export / backup
2. `T-032`: template authoring core の write-back / catalog 連携
3. `T-033`: preview / proof parity を上げる
4. `T-029`: 運用 runbook / 停止・再開・エスカレーション整備

## 6. 明確な blocker

- `T-030`: GitHub repository secret `OPENAI_API_KEY`
- `T-031`: 実機プリンタ測定と `docs/printer-matrix/` 更新
