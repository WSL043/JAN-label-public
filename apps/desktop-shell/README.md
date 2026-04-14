# desktop-shell

Windows 配布が必要になったため、`admin-web` を既存 frontend として包む最小 Tauri 2 shell をここに置く。

## 目的

- `admin-web` を再実装せずに Windows 配布経路を作る
- 製品の中心を `crates/print-agent` に置いたまま、配布だけを shell に閉じ込める
- 将来 `print-agent` とのローカルブリッジを追加できる土台を先に作る

## 使い方

```powershell
pnpm --filter @label/desktop-shell dev
pnpm --filter @label/desktop-shell build
```

- `dev` は `admin-web` の Vite 開発サーバーを先に起動し、Tauri shell から `http://127.0.0.1:5173` を開く
- `build` は `admin-web` を先に build し、その `dist/` を Tauri bundle に埋め込む

## 前提

- Windows では WebView2 Runtime が必要
- Windows で bundle まで作る場合は VC++ Build Tools が必要
- `print-agent` とのブリッジはまだ未接続で、現時点では `admin-web` の shell 化まで
