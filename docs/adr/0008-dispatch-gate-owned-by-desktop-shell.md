# ADR 0008: Dispatch Gate Owned By Desktop Shell

- Status: accepted
- Date: 2026-04-15

## Context

`admin-web` は operator 体験として lineage 補完、template 選択、bridge warning 表示を持てるが、release 安全性を UI 推論だけに依存させると drift が起きる。  
特に次の 3 点は backend が正でないと再印刷事故や ledger 欠落を防げない。

- approved proof lineage と print request lineage の整合
- packaged `template_version` の存在確認
- dispatch 成功後に audit persistence failure を見逃さないこと

## Decision

- proof / print dispatch の最終 gate は `apps/desktop-shell` が持つ
- `desktop-shell` は packaged template catalog を公開し、dispatch 前にも `template_version` の存在確認を行う
- approved proof を使う print request は、`desktop-shell` が lineage を検証し、未指定なら approved proof lineage を補完する
- audit ledger は dispatch 前に writable を preflight し、dispatch 後の persistence failure も fatal にする
- `admin-web` は operator 向けに catalog mismatch や bridge 状態を先に表示するが、最終判断は backend に委ねる

## Consequences

- lineage drift や unknown template route を UI 検索状態に依存せずに防げる
- audit 欠落を success 扱いにしない release 境界が明確になる
- browser preview mode はさらに preview-only の位置づけが明確になる
- authored template の write-back / manifest 反映は別タスクとして残る
