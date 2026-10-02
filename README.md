# moinsearch

MoinMoin Wiki (1.9.11) を認証付きで検索する、Windows x64向けのコマンドラインツールです。

## 使用例

### 検索

標準出力へTSV形式（ページ名<TAB>URL）で出力します。

```powershell
moinsearch search "議事録"
# => 議事録2024年度<TAB>https://wiki.example.com/議事録2024年度
# => 議事録2025年度<TAB>https://wiki.example.com/議事録2025年度
```

### ページ取得

ページ本文はWiki記法を含む原文を出力します。

```
moinsearch get "https://wiki.example.com/議事録2025年度" > page.txt
```

引数には、検索結果に含まれるWikiのページURLを指定してください。

## 対応環境

- 対応OS: Windows x64
- Native AOT ビルドしているため、実行に .NET ランタイムは必要はありません
- [GitHub Releases](https://github.com/ledsun/moinsearch/releases) からビルド済みexeをダウンロードできます


## 設定ファイル（`.moinsearch.toml`）

設定ファイルはユーザーのホームディレクトリ直下の `.moinsearch.toml` です。
Windowsでは `%USERPROFILE%\.moinsearch.toml`です。
通常は `C:\Users\<ユーザー名>\.moinsearch.toml` です。
カレントディレクトリの設定ファイルは読み込みません。

```toml
url = "https://wiki.example.com/"
username = "YourWikiName"
password = "your-password"
```

| 項目 | 説明 | 必須 |
| --- | --- | --- |
| `url` | Wiki のベースURL<br>HTTPS必須<br>ユーザー名・パスワードの埋め込み、クエリ文字列、フラグメント (`#...`) を含めない | はい |
| `username` | WikiName | はい |
| `password` | パスワード<br>平文で保存 | はい |

## 終了コード

| 終了コード | 意味 |
| --- | --- |
| 0 | 成功（0件ヒットを含む）、または `--help` 表示 |
| 1 | 通信・サーバー応答・応答解析などの実行エラー |
| 2 | 引数エラー・設定エラー |
| 3 | 明確に判別できたWiki認証失敗 |
| 130 | ユーザーによるキャンセル（Ctrl+C） |
