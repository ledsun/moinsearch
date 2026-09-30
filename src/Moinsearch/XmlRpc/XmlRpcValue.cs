namespace Moinsearch.XmlRpc;

/// <summary>
/// XML-RPC の値を表す判別共用体。今回の通信に必要な最小限の型のみを扱う
/// （汎用的なRPCフレームワークは作らない）。
/// </summary>
internal abstract record XmlRpcValue
{
    public static XmlRpcValue String(string value) => new XmlRpcString(value);

    public static XmlRpcValue Int(int value) => new XmlRpcInt(value);

    public static XmlRpcValue Boolean(bool value) => new XmlRpcBoolean(value);

    public static XmlRpcValue Array(IReadOnlyList<XmlRpcValue> items) => new XmlRpcArray(items);

    public static XmlRpcValue Struct(IReadOnlyDictionary<string, XmlRpcValue> members) => new XmlRpcStruct(members);
}

internal sealed record XmlRpcString(string Value) : XmlRpcValue;

internal sealed record XmlRpcInt(int Value) : XmlRpcValue;

internal sealed record XmlRpcBoolean(bool Value) : XmlRpcValue;

internal sealed record XmlRpcArray(IReadOnlyList<XmlRpcValue> Items) : XmlRpcValue;

internal sealed record XmlRpcStruct(IReadOnlyDictionary<string, XmlRpcValue> Members) : XmlRpcValue;

/// <summary>
/// <see cref="XmlRpcValue"/> から必要な型を安全に取り出すためのヘルパー。
/// 想定外の型が来た場合は <see cref="CommunicationException"/> として扱う（応答解析エラー）。
/// </summary>
internal static class XmlRpcValueExtensions
{
    public static string AsString(this XmlRpcValue value) => value switch
    {
        XmlRpcString s => s.Value,
        _ => throw new CommunicationException("サーバー応答の型が string ではありません。"),
    };

    public static IReadOnlyList<XmlRpcValue> AsArray(this XmlRpcValue value) => value switch
    {
        XmlRpcArray a => a.Items,
        _ => throw new CommunicationException("サーバー応答の型が array ではありません。"),
    };
}
