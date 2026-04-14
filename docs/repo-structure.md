# repo-structure

## 1. 目的

モノレポを「印刷コア」「UI」「共有定義」「運用資産」に分け、将来 shell が増えてもコアの可読性が崩れない形にします。

## 2. 現在のツリー

```text
label-platform/
  apps/
    admin-web/
    desktop-shell/
  crates/
    audit-log/
    barcode/
    domain/
    importer/
    print-agent/
    printer-adapters/
    render/
  packages/
    fixtures/
    job-schema/
  scripts/
    windows/
  docs/
    architecture.md
    domain-model.md
    github-governance.md
    mvp-backlog.md
    print-pipeline.md
    repo-structure.md
    risk-register.md
    windows-bootstrap.md
    architecture/
    label-specs/
    ops/
    printer-matrix/
  .github/
    workflows/
    ISSUE_TEMPLATE/
```

## 3. 責務分割

- `apps/admin-web`
  React UI。SKU 検索、数量展開、プレビュー、再印刷、履歴閲覧。
- `apps/desktop-shell`
  Tauri を入れるならここ。未採用時は空のまま維持する。
- `crates/domain`
  SKU/JAN/数量/ブランド/テンプレート参照の正規ルール。
- `crates/importer`
  Excel/CSV の厳格バリデーションと正規化。
- `crates/barcode`
  Zint 呼び出し境界。
- `crates/render`
  SVG/PDF の安定出力とゴールデンテスト。
- `crates/printer-adapters`
  Windows spooler / PDF / ZPL / TSPL / QZ などの差分吸収。
- `crates/audit-log`
  印刷・失敗・再印刷の監査ログモデル。
- `crates/print-agent`
  ジョブを組み立てて adapter へ流すオーケストレータ。
- `packages/job-schema`
  UI と外部ツールの共有型。
- `packages/fixtures`
  サンプル CSV/JSON、期待 SVG/PDF。

## 4. 追加ルール

- 新しいプリンタ実装は `crates/printer-adapters` 配下に入れ、`render` へ分散させない。
- UI 専用の変換ロジックで JAN を正規化しない。正規化は Rust 側と schema 側で統制する。
- fixture を変えた PR は、対応する docs と golden test を同じ PR で更新する。

