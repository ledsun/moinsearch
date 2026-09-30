namespace Moinsearch;

/// <summary>
/// moinsearch の終了コード一覧。
/// </summary>
internal static class ExitCode
{
    /// <summary>成功・0件・ヘルプ表示。</summary>
    public const int Success = 0;

    /// <summary>通信・サーバー・応答解析などの実行エラー。</summary>
    public const int ExecutionError = 1;

    /// <summary>引数・設定エラー。</summary>
    public const int UsageOrConfigurationError = 2;

    /// <summary>明確に判別できた Wiki 認証失敗。</summary>
    public const int AuthenticationFailure = 3;

    /// <summary>ユーザーによるキャンセル（Ctrl+C）。</summary>
    public const int Cancelled = 130;
}
