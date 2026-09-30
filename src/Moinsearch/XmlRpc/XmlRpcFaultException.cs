namespace Moinsearch.XmlRpc;

/// <summary>
/// XML-RPC の methodResponse がトップレベル Fault だったことを表す。
/// faultString はログ・画面表示にそのまま出さないこと（呼び出し側で安全なメッセージに変換する）。
/// </summary>
internal sealed class XmlRpcFaultException(int faultCode, string faultString)
    : Exception($"XML-RPC fault {faultCode}")
{
    public int FaultCode { get; } = faultCode;

    public string FaultString { get; } = faultString;
}
