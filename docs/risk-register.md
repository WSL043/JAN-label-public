# risk-register

| id | risk | impact | early signal | mitigation |
| --- | --- | --- | --- | --- |
| R-01 | UI 先行で進めて印刷コア境界が崩れる | 高 | UI 側に JAN 正規化ロジックが増える | `domain` crate を唯一の正規化点に固定 |
| R-02 | ラベル寸法がプリンタごとにずれる | 高 | PDF と実機で寸法差が出る | printer profile と 100% スケール検証を必須化 |
| R-03 | バーコード生成を自前化して保守負債化 | 高 | 独自描画ロジックが増える | Zint を adapter として固定 |
| R-04 | fixture 更新時に docs が追随しない | 中 | PR にコードだけ変更が入る | docs-guard を required check にする |
| R-05 | printer adapter 変更が main に無検証で入る | 高 | adapter 配下だけ変更される PR | auto-label + CODEOWNERS + golden tests |
| R-06 | Excel 列名ゆれが暗黙吸収されて事故る | 中 | importer に例外的マッピングが増える | canonical column を先に固定し reject 優先 |
| R-07 | 再印刷が監査不能になる | 高 | 同じ job が上書きされる | lineage ID と event log を保持 |
| R-08 | Tauri を中心に据えて shell 依存になる | 中 | shell に業務ロジックが入る | shell は optional と明文化 |

