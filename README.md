# moinsearch

MoinMoin Wiki (1.9.11) を認証付きで検索する、Windows x64向けのコマンドラインツールです。

## 1. 用途と最小版の機能範囲

`moinsearch` は MoinMoin Wiki の XML-RPC API を使って、指定したキーワードで本文を
全文検索し、結果を「ページ名とURL」の一覧として標準出力に表示するだけの最小限の
CLI です。

- 実行できるのは認証付きの本文検索のみです（`moinsearch "検索語"`）。
- 結果はページ名順に全件表示します。1行1件、「ページ名\<TAB\>URL」形式で、
  ヘッダー・抜粋・件数・成功メッセージなどの余計な出力はしません。
- 検索語はそのまま MoinMoin の検索APIに渡されます。moinsearch 独自の検索構文や
  変換処理は追加していないため、**Wikiサーバー側の検索構文（`title:`、正規表現の
  可否など）がそのまま適用されます**。詳しくは検索対象の MoinMoin Wiki のドキュメ
  ントを参照してください。
- 次のような機能は最小版には含まれません: タイトル検索・prefix・limit指定、
  JSON出力、抜粋表示、ページ取得・更新、添付ファイル操作、ローカル索引、
  設定変更コマンド、複数プロファイルの切り替え、標準入力からの対話的な入力。

## 2. 対応環境

- 対応OS: Windows x64。
- 配布された `moinsearch.exe`（Native AOTでビルドしたシングルファイルexe）を使う
  **利用者は .NET ランタイムを別途インストールする必要はありません**。
- 本リポジトリではビルド済みのexeやリリースは配布していません。exeが必要な場合は
  「9. ビルド・テスト・発行コマンド」に従って各自ビルドしてください。

## 3. exeの配置と起動方法

1. ビルドした `moinsearch.exe` を任意のフォルダ（例: `C:\Tools\moinsearch\`）に
   配置します。
2. PowerShell から実行する場合はフルパスまたは相対パスで起動します。

   ```powershell
   C:\Tools\moinsearch\moinsearch.exe "検索語"
   ```

3. 毎回フルパスを打たずに `moinsearch` コマンドとして呼び出したい場合は、配置先の
   フォルダを `PATH` 環境変数に追加してください（任意）。

   ```powershell
   $env:Path += ";C:\Tools\moinsearch"
   # 永続化する場合（現在のユーザーのみ）
   $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
   $toolPath = "C:\Tools\moinsearch"
   if (($userPath -split ';') -notcontains $toolPath) {
       $newUserPath = if ([string]::IsNullOrEmpty($userPath)) { $toolPath } else { "$userPath;$toolPath" }
       [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
   }
   ```

## 4. 設定ファイル（`.moinsearch.toml`）

設定ファイルはユーザーのホームディレクトリ直下の `.moinsearch.toml` です
（Windowsでは `%USERPROFILE%\.moinsearch.toml`、通常 `C:\Users\<ユーザー名>\.moinsearch.toml`）。
カレントディレクトリの設定ファイルは読み込みません。`--config` オプションや、
パスワードをコマンドライン引数で渡す方法は用意していません。

```toml
url = "https://wiki.example.com/"
username = "YourWikiName"
password = "your-password"
```

- `url` は Wiki のベースURL（HTTPS必須）です。サブディレクトリ配置
  （例: `https://example.com/wiki/mywiki/`）にも対応しています。
  ユーザー名・パスワードの埋め込み、クエリ文字列、フラグメント (`#...`) を含む
  URLは設定エラーになります。
- `username` は WikiName、`password` はそのままのパスワードです（自動でTrimしません）。
- 3項目すべて必須です。

## 5. 使用例

```powershell
# 空白を含まない検索語
moinsearch 議事録

# 空白を含む検索語（引用符で囲む）
moinsearch "定例会議 議事録"

# 標準出力はTSV形式（ページ名<TAB>URL）
moinsearch "議事録"
# => 議事録2024年度<TAB>https://wiki.example.com/議事録2024年度
# => 議事録2025年度<TAB>https://wiki.example.com/議事録2025年度

# ファイルにリダイレクトしても日本語がそのまま書き出される
moinsearch "議事録" > result.tsv

# 該当なし（0件）の場合は標準出力が空のまま正常終了する（終了コード0）
moinsearch "存在しないはずのキーワードxyz123"
```

## 6. 設定ファイルの取り扱いに関する注意

`.moinsearch.toml` にはパスワードが平文で保存されます。次の点に注意してください。

- ファイルのアクセス権限を自分のアカウントのみに制限してください。
  PowerShellの例:

  ```powershell
  icacls "$env:USERPROFILE\.moinsearch.toml" /inheritance:r /grant:r "$env:USERNAME:F"
  ```

- **Gitなどのバージョン管理には絶対に登録しないでください。** リポジトリ直下で
  作業する場合は `.gitignore` に `.moinsearch.toml` を追加することを推奨します。

## 7. 開発の前提条件

- .NET SDK: `global.json` で固定しているバージョン（本リポジトリでは
  `10.0.401`、.NET 10 / C# 14 系のGA版。Preview・RC版は使用していません）。
- Windows 向け Native AOT ビルドを行うには、Windows環境に加えて
  「C++によるデスクトップ開発」ワークロード（Visual Studio Build Tools に含まれる
  MSVC ツールチェーンとWindows SDK）が必要です。
  ([参考: .NET Native AOTの前提条件](https://learn.microsoft.com/dotnet/core/deploying/native-aot/))

## 8. ビルド・テスト・発行コマンド

restore / build / test は Windows・Linux・macOS のどこでも実行できます。

```bash
dotnet restore
dotnet build
dotnet test
```

Windows向け Native AOT の単一exe発行は、**Windows環境上で**次のコマンドを実行して
ください（Linuxからのクロスコンパイルはサポートされていません）。

```powershell
dotnet publish src/Moinsearch -c Release -r win-x64 --self-contained -p:PublishAot=true
```

生成された `moinsearch.exe` は
`src/Moinsearch/bin/Release/net10.0/win-x64/publish/` 配下に出力されます。
発行後は次のコマンドで `--help` が通信なしに表示されることを確認してください。

```powershell
.\moinsearch.exe --help
```

## 9. 終了コード・トラブルシューティング

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
- **認証失敗（終了コード3）の場合**: `.moinsearch.toml` の username /
  password を確認してください。
- **TLSエラーの場合**: moinsearchはTLS証明書検証を無効化しません。証明書が正しい
  ホスト名・信頼された認証局のものか、サーバー側の設定を確認してください。
- **タイムアウトの場合**: 1リクエストあたり20秒で打ち切られます
  （終了コード1）。ネットワーク経路やサーバーの応答性を確認してください。
  自動リトライは行いません。

## 10. XML-RPCと権限について

moinsearchの動作には対象WikiでXML-RPC (`?action=xmlrpc2`) が有効になっている
ことが必要です。検索結果は指定したWikiNameの閲覧権限に従うため、権限のない
ページは結果に含まれません。
