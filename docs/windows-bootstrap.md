# windows-bootstrap

## 1. 必要ツール一覧

- Git
- Rust stable
- Node.js LTS
- pnpm
- Visual Studio C++ Build Tools
- WebView2 Runtime
  Tauri を採用する場合のみ必須
- PDF 仮想プリンタ
- 物理ラベルプリンタ

## 2. インストール順

1. Git
2. Rust stable
3. Node.js LTS
4. pnpm
5. Visual Studio C++ Build Tools
6. WebView2 Runtime
   Tauri 採用時のみ
7. PDF 仮想プリンタ
8. 物理ラベルプリンタのドライバ

## 3. 用途説明

- Git
  ソース管理、PR ベース運用、差分監査。
- Rust stable
  `print-agent`、`domain`、`render`、`printer-adapters` のビルド。
- Node.js LTS
  管理 UI、fixture 検証スクリプト、CI 補助。
- pnpm
  monorepo の依存解決とワークスペース管理。
- Visual Studio C++ Build Tools
  Windows 向け Rust ネイティブビルド、将来の Tauri シェル。
- WebView2 Runtime
  Tauri の Windows shell 実行基盤。
- PDF 仮想プリンタ
  実機に触る前の倍率・余白確認。
- 物理ラベルプリンタ
  本命の寸法検証。

## 4. 動作確認コマンド

```powershell
git --version
rustup show
cargo --version
node --version
pnpm --version
pwsh -File .\scripts\windows\verify-bootstrap.ps1
pwsh -File .\scripts\windows\verify-bootstrap.ps1 --with-tauri
```

## 5. トラブルシュート

### Rust が MSVC を見つけない

- Visual Studio C++ Build Tools に `Desktop development with C++` を入れる
- PowerShell を再起動する

### pnpm が見つからない

- `corepack enable`
- `corepack prepare pnpm@latest --activate`

### Tauri だけ起動しない

- WebView2 Runtime の有無を確認する
- `verify-bootstrap.ps1 --with-tauri` で確認する

### 印字倍率がずれる

- OS の印刷ダイアログで `100%` 固定以外を禁止する
- PDF 仮想プリンタで 50mm x 30mm の実測を先に行う
- 物理プリンタ側の縮小拡大設定を無効化する

## 6. ハードウェア検証環境

- 物理ラベルプリンタ 1 台
- A4 普通紙プリンタまたは PDF 仮想プリンタ
- 実運用ラベル用紙
- 定規
- ノギス
- バーコードリーダー

## 7. Tauri を使う場合と使わない場合の差分

- 使う場合
  Windows 配布がしやすい。ローカルブリッジを同梱しやすい。VC++ Build Tools と WebView2 が必要。
- 使わない場合
  開発は軽い。ブラウザ UI とローカル `print-agent` の組み合わせで MVP を進めやすい。
- 共通
  印刷コアの責務は変わらない。印字精度は UI 技術ではなく template と adapter で決まる。

