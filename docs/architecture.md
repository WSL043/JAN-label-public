# architecture

## 1. 方針

最初に固定するのは UI ではなく印刷コアの境界です。  
このリポジトリでは、Windows 向け MVP を最短で成立させつつ、同じ印刷コアを後から macOS / Linux / mobile から再利用できるよう、責務を次の 3 層に分けます。

1. `crates/*`
   印刷コア。業務ルール、JAN 検証、テンプレート解決、レンダリング、監査ログ、プリンタアダプタ。
2. `apps/admin-web`
   ジョブ作成、検索、再印刷指示、履歴確認、設定管理。
3. `apps/desktop-shell`
   必要になった時だけ追加する Windows 配布シェル。中心ではない。

## 2. コンポーネント境界

```text
[Admin Web / Mobile Client]
           |
           v
[Job API / Local Bridge]
           |
           v
[print-agent]
  |-> [domain]
  |-> [importer]
  |-> [barcode -> Zint]
  |-> [render -> SVG/PDF]
  |-> [printer-adapters]
  `-> [audit-log]
```

## 3. 実装原則

- UI は「何を印刷するか」を作る。印刷そのものの正しさは Rust 側で担保する。
- レンダリングは最初に `SVG/PDF` を正とし、プリンタ専用言語は adapter で追加する。
- バーコードは自前ロジックを主役にしない。JAN の正規化と検証だけを自前で持ち、描画は Zint に委譲する。
- `printer profile` を導入し、プリンタ固有差分はテンプレートに混ぜない。
- 監査ログは印刷成功だけでなく、作成、失敗、再印刷も同じ `job_id` 系譜に残す。

## 4. 主要インターフェース

```rust
pub struct DispatchRequest {
    pub job_id: String,
    pub sku: String,
    pub jan: String,
    pub qty: u32,
    pub template_version: String,
    pub actor_user_id: String,
    pub requested_at: String,
}

pub trait PrinterAdapter {
    fn submit(&self, artifact: &PrintArtifact) -> Result<SubmissionReceipt, AdapterError>;
}
```

```ts
export type PrintJobDraft = {
  jobId: string;
  parentSku: string;
  sku: string;
  jan: { raw: string; normalized: string; source: "manual" | "import" };
  qty: number;
  brand: string;
  template: { id: string; version: string };
  printerProfile: { id: string; adapter: string; paperSize: string; dpi: number };
  actor: string;
  requestedAt: string;
};
```

## 5. 初期デプロイ形態

- Windows ローカル開発: `admin-web` + `print-agent` + PDF 仮想プリンタ + 物理ラベルプリンタ
- Windows 配布: `admin-web` を Tauri で包む、またはブラウザ UI + ローカル常駐エージェント
- 将来の mobile: ジョブ作成と再印刷指示を担当し、直接印刷は後回し

## 6. 今のリポジトリで固定したこと

- 12 桁 JAN は 13 桁へ自動補完する
- 13 桁 JAN はチェックディジット不整合なら reject する
- ゴールデンテストは `render` crate の SVG 出力から始める
- importer の正規列は `parent_sku, sku, jan, qty, brand, template, printer_profile, enabled`

