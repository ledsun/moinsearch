namespace Moinsearch.Cli;

/// <summary>
/// コマンドライン引数の解析結果の種別。
/// </summary>
internal enum CommandMode
{
    /// <summary>検索を実行する。</summary>
    Search,

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
    string? ErrorMessage = null);

/// <summary>
/// moinsearch のコマンドライン引数を解析する。
/// 対応する引数は検索語1つのみで、オプションは --help / -h だけ。
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
            return new ParsedArguments(CommandMode.Error, ErrorMessage: "検索語を指定してください。");
        }

        string? searchTerm = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith('-'))
            {
                return new ParsedArguments(CommandMode.Error, ErrorMessage: $"不明なオプションです: {arg}");
            }

            if (searchTerm is not null)
            {
                return new ParsedArguments(CommandMode.Error, ErrorMessage: "引数が多すぎます。検索語は1つだけ指定してください。");
            }

            searchTerm = arg;
        }

        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return new ParsedArguments(CommandMode.Error, ErrorMessage: "検索語が空です。空白だけの検索語は指定できません。");
        }

        return new ParsedArguments(CommandMode.Search, SearchTerm: searchTerm);
    }
}
