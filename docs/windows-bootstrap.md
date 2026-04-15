# windows-bootstrap

## 必要環境
- Git
- Rust stable
- Node.js LTS
- pnpm
- Visual Studio C++ Build Tools
- WebView2 Runtime

## Windows bootstrap
```powershell
git --version
rustup show
cargo --version
node --version
pnpm --version
pnpm --filter @label/desktop-shell tauri info
pnpm --filter @label/desktop-shell build
```

## Desktop Shell / Bridge 運用
- desktop-shell 起動時: Tauri bridge が有効
- admin-web（ブラウザ単体）: bridge 未接続（印刷は preview のみ想定）

Tauri で使うコマンド:
- `dispatch_print_job`
- `print_bridge_status`

必要/任意の環境変数:
- `JAN_LABEL_PRINT_OUTPUT_DIR`
- `JAN_LABEL_SPOOL_OUTPUT_DIR`
- `JAN_LABEL_ZINT_BINARY_PATH`
- `JAN_LABEL_PRINT_ADAPTER`
- `JAN_LABEL_WINDOWS_PRINTER_NAME`

## 事前確認（最小）
1. `pnpm --filter @label/desktop-shell build`
2. `pnpm --filter @label/desktop-shell tauri info`
3. desktop-shell 起動後、`print_bridge_status` を呼び `print_adapter_kind` と `warnings` を確認

