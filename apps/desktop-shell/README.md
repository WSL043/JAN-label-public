# desktop-shell

`admin-web` の実行フローを Tauri bridge 経由で起動する Windows 向けランナーです。  
`admin-web` 単体は UI のみで、印刷実行（`print-agent` 連携）は `desktop-shell` 起動時のみ有効になります。

## 利用可能な invoke コマンド

- `dispatch_print_job`
  - 入力: `DispatchRequest`
  - 出力: `PrintDispatchResult`
  - `execution.mode` が `proof` の場合は PDF 出力、`print` の場合は設定に応じたアダプタで出力します。
- `print_bridge_status`
  - 入力: なし
  - 出力: `PrintBridgeStatus`（JSON）

```ts
type PrintBridgeStatus = {
  availableAdapters: string[]
  resolvedZintPath: string
  proofOutputDir: string
  printOutputDir: string
  spoolOutputDir: string
  printAdapterKind: string
  windowsPrinterName: string
  warnings: string[]
};
```

## 必要な環境変数

- `JAN_LABEL_PRINT_OUTPUT_DIR`  
  proof と print の PDF 出力先（未指定なら `temp/jan-label/proofs`）
- `JAN_LABEL_SPOOL_OUTPUT_DIR`  
  Windows spooler アダプタ時のステージング先（未指定なら `temp/jan-label/spool`）
- `JAN_LABEL_ZINT_BINARY_PATH`  
  Zint CLI の実行パス（未指定なら `zint`）
- `JAN_LABEL_PRINT_ADAPTER`  
  `pdf` / `windows-spooler`（未指定なら `windows-spooler`）
- `JAN_LABEL_WINDOWS_PRINTER_NAME`  
  Windows spooler 用プリンタ名（未指定なら `Default Printer`）

`print_bridge_status` の `warnings` には、未設定によるフォールバックや危険な選択内容が文字列で返ります。

## proof / print の出力ルール

- proof: `JAN_LABEL_PRINT_OUTPUT_DIR` 配下に `<jobId>-proof.pdf`
- print:  
  - `JAN_LABEL_PRINT_ADAPTER=pdf` のとき: `JAN_LABEL_PRINT_OUTPUT_DIR` 配下に `<jobId>-print.pdf`
  - `JAN_LABEL_PRINT_ADAPTER=windows-spooler` のとき: `JAN_LABEL_SPOOL_OUTPUT_DIR` 配下に `<jobId>-print.svg`

## 実行コマンド

```powershell
pnpm --filter @label/desktop-shell dev
```

`dev` では `admin-web`（Vite）を起動して Tauri shell に接続します。  
ブラウザ版（`pnpm --filter @label/admin-web dev`）はこの bridge 経由の印刷実行は未接続です。

## 備考

- Windows のみで運用を想定しています（WebView2 / Windows print 環境を前提）。
- 開発・検証時は `print_bridge_status` を先に呼び、`warnings` を確認してから dispatch を実行するのが安全です。
