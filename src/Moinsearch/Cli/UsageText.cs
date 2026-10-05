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
          moinsearch auth set

        search は MoinMoin Wiki の本文を全文検索し、ページ名順に「ページ名<TAB>URL」の1行1件形式で出力します。
        get は指定ページの Wiki 記法を含む原文を出力します。
        auth set はパスワードを対話入力し、Wikiで認証できた場合にWindows Credential Managerへ保存します。

        引数:
          search        検索するキーワードを指定します
          get           検索結果のページURLを指定します
          auth set      パスワードを検証して保存します

        オプション:
          -h, --help    この使い方を表示して終了します（サーバーへの通信は行いません）

        設定:
          接続先・ユーザー名は ~/.moinsearch.toml で指定します。
          パスワードは Windows Credential Manager に保存します。
          詳細は README.md を参照してください。
        """;
}
