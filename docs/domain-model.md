# domain-model

## 1. 業務モデル

### CatalogItem

| field | type | rule |
| --- | --- | --- |
| `parent_sku` | string | 商品グループの親 ID。必須 |
| `sku` | string | 子 SKU。必須 |
| `jan` | string | 12 桁入力可、保存時は 13 桁 |
| `qty` | integer | 1 以上 |
| `brand` | string | 空文字禁止 |
| `template` | string | ラベルテンプレート ID |
| `printer_profile` | string | 使用プリンタープロファイル ID |
| `enabled` | boolean | 印刷対象フラグ |

## 2. JAN / EAN-13 ポリシー

- 12 桁入力
  チェックディジットを自動補完して 13 桁に正規化する
- 13 桁入力
  チェックディジットを検証し、不一致なら reject
- 11 桁以下 / 14 桁以上 / 非数字
  reject

### 擬似コード

```text
if input is empty -> error
if contains non digit -> error
if len == 12 -> append checksum
if len == 13 -> verify checksum
otherwise -> error
```

## 3. テンプレートの版管理

- 識別子は `template_id@version` で扱う
- 例: `basic-50x30@v1`
- 既存版の上書き禁止
- 実寸、余白、バーコード配置は版ごとに固定
- 旧版再印刷のため、監査ログには `template_id` と `template_version` を保存する

## 4. printer profile abstraction

| field | example | note |
| --- | --- | --- |
| `id` | `pdf-a4-proof` | 一意 ID |
| `adapter` | `pdf` | 出力経路 |
| `paper_size` | `A4` | 実紙サイズ |
| `dpi` | `300` | ヘッド密度 |
| `scale_policy` | `fixed-100` | 常に 100% 固定 |

## 5. 監査ログモデル

- `job_id`
- `job_lineage_id`
  再印刷でも lineage は維持
- `actor_user_id`
- `actor_display_name`
- `event_kind`
  `created`, `submitted`, `completed`, `failed`, `reprinted`
- `template_version`
- `printer_profile_id`
- `occurred_at`
- `reason`

## 6. importer の正規列

```text
parent_sku,sku,jan,qty,brand,template,printer_profile,enabled
```

- Excel から来ても、一度この列順へ正規化してから処理する
- 余計な列は warning ではなく reject を基本とする
- 列名ゆれ吸収は importer 境界内で明示的に行い、暗黙マッピングを禁止する
- 行値の初期バリデーションは importer 境界で返す
- `parent_sku`, `sku`, `brand`, `template`, `printer_profile` は空文字 reject
- `jan` は Rust 側の JAN ルールで検証し、12 桁なら 13 桁へ正規化する
- `qty` は 1 以上の整数のみ許可する
- `enabled` は `true` / `false` のみ許可する
