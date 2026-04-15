# ADR 0007: Template Preview Via Rust Renderer

- Status: accepted
- Date: 2026-04-15

## Context

`admin-web` の structured template editor は operator 体験として必要だが、local canvas だけでは実 renderer との差異が残る。特に border / color / placeholder / text layout は proof / PDF 側の真実と一致している必要がある。

一方で、現時点の proof / print dispatch は packaged manifest の `template_version` を使っており、editor の生 JSON をそのまま本番 dispatch に流してはいない。

## Decision

- local canvas preview は近似表示として維持する
- それとは別に `desktop-shell` 経由で Rust renderer に live template JSON を渡す `preview_template_draft` を追加する
- operator が release 判断や parity 確認を行うときは Rust preview / proof を正とする
- proof / print dispatch が editor の生 JSON を使うまでは、UI に release 境界を明示する

## Consequences

- template authoring の確認精度は上がる
- local canvas と Rust preview の 2 段構えになる
- authored template の write-back / dispatch 反映は別タスクとして残る
