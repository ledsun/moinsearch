# moinsearch

MoinMoin Wiki (1.9.11) を認証付きで検索する、Windows x64向けのコマンドラインツールです。

## 使用例

```powershell
# 標準出力はTSV形式（ページ名<TAB>URL）
moinsearch "議事録"
# => 議事録2024年度<TAB>https://wiki.example.com/議事録2024年度
# => 議事録2025年度<TAB>https://wiki.example.com/議事録2025年度
```

## exeの配置と起動方法

1. `moinsearch.exe` を任意のフォルダ（例: `C:\Tools\moinsearch\`）に
   配置します。
2. PowerShell から実行する場合はフルパスまたは相対パスで起動します。

```powershell
C:\Tools\moinsearch\moinsearch.exe "検索語"
```

## 設定ファイル（`.moinsearch.toml`）

設定ファイルはユーザーのホームディレクトリ直下の `.moinsearch.toml` です。
Windowsでは `%USERPROFILE%\.moinsearch.toml`、通常は `C:\Users\<ユーザー名>\.moinsearch.toml` です。
カレントディレクトリの設定ファイルは読み込みません。

```toml
url = "https://wiki.example.com/"
username = "YourWikiName"
password = "your-password"
```

- `url` は Wiki のベースURL（HTTPS必須）です。
  - サブディレクトリ配置（例: `https://example.com/wiki/mywiki/`）にも対応しています。
  - ユーザー名・パスワードの埋め込み、クエリ文字列、フラグメント (`#...`) を含む
  URLは設定エラーになります。
- `username` は WikiName
- `password` はパスワード。パスワードは平文で保存されます。

3項目すべて必須です。

## 対応環境

- 対応OS: Windows x64。
- 配布された `moinsearch.exe`（Native AOTでビルドしたシングルファイルexe）を使う
  **利用者は .NET ランタイムを別途インストールする必要はありません**。
- 本リポジトリではビルド済みのexeやリリースは配布していません。exeが必要な場合は
  「9. ビルド・テスト・発行コマンド」に従って各自ビルドしてください。

### 終了コード・トラブルシューティング

| 終了コード | 意味 |
| --- | --- |
| 0 | 成功（0件ヒットを含む）、または `--help` 表示 |
| 1 | 通信・サーバー応答・応答解析などの実行エラー |
| 2 | 引数エラー・設定エラー |
| 3 | 明確に判別できたWiki認証失敗 |
| 130 | ユーザーによるキャンセル（Ctrl+C） |

- **HTTP 403 が返る場合**: 403は必ずしもパスワード間違いを意味しません
  （Wiki側のアクセス制御や別の要因の可能性があります）。まずは `url` の設定と
  Wiki側の権限設定を確認してください。moinsearchは403だけを理由に認証失敗と
  断定しません。
- **認証失敗（終了コード3）の場合**: `MOINSEARCH_USERNAME` /
  `MOINSEARCH_PASSWORD`（または `.moinsearch.toml` の該当項目）を確認してください。
- **TLSエラーの場合**: moinsearchはTLS証明書検証を無効化しません。証明書が正しい
  ホスト名・信頼された認証局のものか、サーバー側の設定を確認してください。
- **タイムアウトの場合**: 1リクエストあたり20秒で打ち切られます
  （終了コード1）。ネットワーク経路やサーバーの応答性を確認してください。
  自動リトライは行いません。

## 開発

### 前提条件

- .NET SDK: `global.json` で固定しているバージョン（本リポジトリでは
  `10.0.401`、.NET 10 / C# 14 系のGA版。Preview・RC版は使用していません）。
- Windows 向け Native AOT ビルドを行うには、Windows環境に加えて
  「C++によるデスクトップ開発」ワークロード（Visual Studio Build Tools に含まれる
  MSVC ツールチェーンとWindows SDK）が必要です。
  ([参考: .NET Native AOTの前提条件](https://learn.microsoft.com/dotnet/core/deploying/native-aot/))

### ビルド・テスト・発行コマンド

restore / build / test は Windows・Linux・macOS のどこでも実行できます。

```bash
dotnet restore
dotnet build
dotnet test
```

#### Windows向け Native AOT 発行

Windows向け Native AOT の単一exe発行は、Windowsで次のコマンドを実行して
ください。

```powershell
dotnet publish src/Moinsearch -c Release -r win-x64 --self-contained -p:PublishAot=true
```

Linuxからのクロスコンパイルはサポートされていません。

生成された `moinsearch.exe` は
`src/Moinsearch/bin/Release/net10.0/win-x64/publish/` 配下に出力されます。


### 動作確認

次のコマンドで `--help` が通信なしに表示されることを確認できます。

```powershell
.\moinsearch.exe --help
```

### MoinMoin Wiki側のXML-RPCの制約

- moinsearchの動作には対象WikiでXML-RPC (`?action=xmlrpc2`) の有効化が必要
- 検索結果は指定したWikiNameの閲覧権限に従う
