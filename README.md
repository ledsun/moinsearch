# moinsearch

MoinMoin Wiki (1.9.11) を認証付きで検索する、Windows x64向けのコマンドラインツールです。

## 対応環境

- 対応OS: Windows x64
- Native AOTでビルドしているため利用者は .NET ランタイムをインストールする必要はありません
- 本リポジトリではビルド済みのexeやリリースは配布していません。exeが必要な場合は
  [開発者向け情報](CONTRIBUTING.md)に従って各自ビルドしてください。

## 使用例

```powershell
# 標準出力はTSV形式（ページ名<TAB>URL）
moinsearch search "議事録"
# => 議事録2024年度<TAB>https://wiki.example.com/議事録2024年度
# => 議事録2025年度<TAB>https://wiki.example.com/議事録2025年度

# ページ本文はWiki記法を含む原文だけを出力
moinsearch get "https://wiki.example.com/議事録2025年度" > page.txt
```

- `get` のURLは検索結果に含まれる、WikiのページURLを指定してください
- `get` で取得するページはMoinMoin XML-RPCの `getPage` が返すWiki記法を含む原文です。HTMLや装飾の除去は行いません。


## exeの配置と起動方法

1. `moinsearch.exe` を任意のフォルダ（例: `C:\Tools\moinsearch\`）に
   配置します。
2. PowerShell から実行する場合はフルパスまたは相対パスで起動します。

```powershell
C:\Tools\moinsearch\moinsearch.exe search "検索語"
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

| 項目 | 説明 | 必須 |
| --- | --- | --- |
| `url` | Wiki のベースURL（HTTPS必須）。ユーザー名・パスワードの埋め込み、クエリ文字列、フラグメント (`#...`) を含むURLは設定エラーになります。 | はい |
| `username` | WikiName | はい |
| `password` | パスワード。平文で保存されます。 | はい |

## 終了コード

| 終了コード | 意味 |
| --- | --- |
| 0 | 成功（0件ヒットを含む）、または `--help` 表示 |
| 1 | 通信・サーバー応答・応答解析などの実行エラー |
| 2 | 引数エラー・設定エラー |
| 3 | 明確に判別できたWiki認証失敗 |
| 130 | ユーザーによるキャンセル（Ctrl+C） |

### トラブルシューティング

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
