namespace Moinsearch.Cli;

/// <summary>
/// ヘルプ・使い方メッセージ。
/// </summary>
internal static class UsageText
{
    public const string Text =
        """
        使い方:
          moinsearch search <検索語>
          moinsearch get <ページURL>

        search は MoinMoin Wiki (1.9.11) の本文を認証付きで全文検索し、結果をページ名順に
        「ページ名<TAB>URL」の1行1件形式で標準出力へ出力します。
        get は指定ページの Wiki 記法を含む原文だけを標準出力へ出力します。

        引数:
          search        検索するキーワードを指定します
          get           検索結果のページURLを指定します

        オプション:
          -h, --help    この使い方を表示して終了します（サーバーへの通信は行いません）

        従来の moinsearch <検索語> 形式は廃止されました。

        設定:
          接続先・認証情報は ~/.moinsearch.toml で指定します。
          詳細は README.md を参照してください。
        """;
}
