# github-governance

## 1. ブランチ保護 / ruleset

`main` に対して次を必須にします。

- 直 push 禁止
- PR 経由のみ
- required status checks 必須
- stale review dismiss 有効
- merge queue はチーム規模拡大後に検討
- force push 禁止
- branch deletion 禁止

## 2. required status checks

GitHub ruleset には次の job 名をそのまま登録します。

- `rust-format`
- `rust-lint`
- `rust-test`
- `golden-tests`
- `fixture-validation`
- `web-format-lint`
- `web-typecheck`
- `docs-guard`

`golden-tests` が失敗した PR は merge 不可です。

## 3. CODEOWNERS

初期は少人数前提で `@WSL043` をデフォルト owner にしつつ、印刷中核パスを明示的に強化します。

- `crates/render/**`
- `crates/printer-adapters/**`
- `crates/print-agent/**`
- `packages/fixtures/**`
- `docs/**`

## 4. PR ルール

- 1 PR 1 目的
- printer adapter 変更時は `area:printer-adapters` ラベル必須
- fixture 変更時は golden test か importer validation のどちらかを同時更新
- コード変更で docs 更新が不要なら PR template に理由を書く

## 5. labels 設計

- `type:bug`
- `type:feature`
- `type:task`
- `type:printer-profile`
- `area:print-core`
- `area:admin-web`
- `area:printer-adapters`
- `area:docs`
- `priority:p0`
- `priority:p1`
- `priority:p2`
- `status:blocked`

## 6. CI 基本設計

- Rust
  `fmt`, `clippy`, `test`
- Web
  `biome`, `typecheck`
- Fixtures
  `node scripts/validate-fixtures.mjs`
- Docs
  プロダクトコード変更時に docs 同時更新があるか確認

## 7. release tagging

- tag 形式は `vMAJOR.MINOR.PATCH`
- テンプレート変更だけでも patch を切る
- printer profile の互換性破壊は minor 以上で扱う
- 本番プリンタ向け adapter 追加は release notes に検証機種を明記する

## 8. セキュリティと監査

- セキュリティ報告先は `SECURITY.md`
- 依存更新は PR ベースで実施
- 監査ログ仕様を壊す変更は `print-core` owner の review を必須にする

