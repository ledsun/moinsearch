namespace Moinsearch.Configuration;

/// <summary>
/// 設定の読み込みに失敗したことを表す例外（引数・設定エラー = 終了コード2）。
/// メッセージには設定値そのものを含めない。
/// </summary>
internal class ConfigurationException(string message) : Exception(message)
{
}

internal sealed class MissingCredentialsException(string message) : ConfigurationException(message)
{
}
