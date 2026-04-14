# desktop-shell

Tauri 2 を採用する場合にのみ初期化する配布シェルです。  
製品の中心はここではなく `crates/print-agent` に置きます。

## 初期化タイミング

- Windows 向けのインストーラ配布が必要になった時
- WebView UI とローカル印刷エージェントの橋渡しが必要になった時
- WebView2 / VC++ Build Tools を導入済みの時

それまでは `admin-web` と `print-agent` を先に前進させます。

