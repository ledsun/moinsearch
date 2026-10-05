namespace Moinsearch.Cli;

/// <summary>
/// コマンドライン引数の解析結果の種別。
/// </summary>
internal enum CommandMode
{
    /// <summary>検索を実行する。</summary>
    Search,

    /// <summary>指定ページの本文を取得する。</summary>
    Get,

    /// <summary>パスワードを確認してWindows Credential Managerに登録する。</summary>
    AuthSet,

    /// <summary>使い方を表示して正常終了する。</summary>
    Help,

    /// <summary>引数エラー。使い方を表示してエラー終了する。</summary>
    Error,
}

/// <summary>
/// コマンドライン引数の解析結果。
/// </summary>
internal sealed record ParsedArguments(
    CommandMode Mode,
    string? SearchTerm = null,
    string? PageUrl = null,
    string? ErrorMessage = null);

/// <summary>
/// moinsearch のコマンドライン引数を解析する。
/// 対応するコマンドは search / get / auth set で、オプションは --help / -h だけ。
/// </summary>
internal static class CommandLineParser
{
    public static ParsedArguments Parse(string[] args)
    {
        if (args.Any(a => a is "--help" or "-h"))
        {
            return new ParsedArguments(CommandMode.Help);
        }

        if (args.Length == 0)
        {
            return new ParsedArguments(CommandMode.Error, ErrorMessage: "コマンドと引数を指定してください。");
        }

        if (args.Length != 2)
        {
            return new ParsedArguments(CommandMode.Error, ErrorMessage: "コマンドの指定が正しくありません。");
        }

        if (args[0] == "auth" && args[1] == "set")
        {
            return new ParsedArguments(CommandMode.AuthSet);
        }

        if (args[0] == "search")
        {
            if (string.IsNullOrWhiteSpace(args[1]))
            {
                return new ParsedArguments(CommandMode.Error, ErrorMessage: "検索語が空です。空白だけの検索語は指定できません。");
            }

            return new ParsedArguments(CommandMode.Search, SearchTerm: args[1]);
        }

        if (args[0] == "get")
        {
            if (string.IsNullOrWhiteSpace(args[1]))
            {
                return new ParsedArguments(CommandMode.Error, ErrorMessage: "ページURLが空です。");
            }

            return new ParsedArguments(CommandMode.Get, PageUrl: args[1]);
        }

        return new ParsedArguments(
            CommandMode.Error,
            ErrorMessage: "コマンドは search、get、または auth set を指定してください。従来の moinsearch <検索語> 形式は廃止されました。");
    }
}
