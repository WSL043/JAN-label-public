# known-issues

継続開発で再発しやすい問題や、現時点で残している制約を記録します。

## K-001 branch protection が未適用

- 状態: open
- 影響: `main` への直接 push を GitHub 側で強制できない
- 回避: 運用で PR ベースを維持し、CI green を確認してから反映する
- 恒久対応: GitHub Pro / Team 以上で ruleset を適用

## K-002 third-party Actions が Node 20 target 警告を出す

- 状態: open
- 影響: 今は通るが将来の runner 変更で壊れる可能性がある
- 回避: `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24=true` を workflow に設定
- 恒久対応: `pnpm/action-setup` と `dorny/paths-filter` の後継バージョンを追う

## K-003 Zint がまだ開発ループに未接続

- 状態: open
- 影響: barcode crate は現在 stub に近い
- 回避: `T-001` を優先して CLI adapter を実装する
- 恒久対応: Windows / CI で Zint を導入し fixture と結線する

## K-004 実機プリンタの測定データがまだない

- 状態: open
- 影響: 100% スケール検証が PDF proof に偏っている
- 回避: PDF proof を先に維持する
- 恒久対応: `docs/printer-matrix/` に最低 1 機種分の実測を記録する

