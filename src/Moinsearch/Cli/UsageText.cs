namespace Moinsearch.Cli;

/// <summary>
/// ヘルプ・使い方メッセージ。
/// </summary>
internal static class UsageText
{
    public const string Text =
        """
        使い方: moinsearch <検索語>

        MoinMoin Wiki (1.9.11) の本文を認証付きで全文検索し、結果をページ名順に
        「ページ名<TAB>URL」の1行1件形式で標準出力へ出力します。

        引数:
          検索語        検索するキーワード（空白を含む場合はシェルの引用符で囲んでください）

        オプション:
          -h, --help    この使い方を表示して終了します（サーバーへの通信は行いません）

        設定:
          接続先・認証情報は ~/.moinsearch.toml で指定します。
          詳細は README.md を参照してください。
        """;
}
