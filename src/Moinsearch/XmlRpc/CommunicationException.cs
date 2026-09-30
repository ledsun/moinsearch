namespace Moinsearch.XmlRpc;

/// <summary>
/// 通信・サーバー応答の解析に失敗したことを表す例外（実行エラー = 終了コード1）。
/// サーバーが返した Fault 文字列や HTML をそのまま含めず、安全な診断メッセージのみを保持する。
/// </summary>
internal sealed class CommunicationException(string message) : Exception(message)
{
}
