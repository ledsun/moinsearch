using System.Xml;
using System.Xml.Linq;

namespace Moinsearch.XmlRpc;

/// <summary>
/// XML-RPC の methodResponse を安全に解析する。
/// DTD・外部エンティティは解決しない。今回必要な型（string/int/boolean/array/struct）のみ対応する。
/// </summary>
internal static class XmlRpcReader
{
    public static XmlRpcValue ReadMethodResponse(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
        };

        XDocument document;
        try
        {
            using var stringReader = new StringReader(xml);
            using var xmlReader = XmlReader.Create(stringReader, settings);
            document = XDocument.Load(xmlReader);
        }
        catch (XmlException)
        {
            throw new CommunicationException("サーバーの応答が有効な XML ではありません。");
        }

        var methodResponse = document.Root;
        if (methodResponse is null || methodResponse.Name.LocalName != "methodResponse")
        {
            throw new CommunicationException("サーバーの応答が XML-RPC の methodResponse ではありません。");
        }

        var fault = methodResponse.Element("fault");
        if (fault is not null)
        {
            var faultValueElement = fault.Element("value")
                ?? throw new CommunicationException("サーバーの fault 応答に value がありません。");
            var faultValue = ParseValue(faultValueElement);
            var (faultCode, faultString) = ExtractFault(faultValue);
            throw new XmlRpcFaultException(faultCode, faultString);
        }

        var paramValueElement = methodResponse.Element("params")?.Element("param")?.Element("value");
        if (paramValueElement is null)
        {
            throw new CommunicationException("サーバーの応答に params/param/value がありません。");
        }

        return ParseValue(paramValueElement);
    }

    private static (int FaultCode, string FaultString) ExtractFault(XmlRpcValue faultValue)
    {
        if (faultValue is not XmlRpcStruct faultStruct)
        {
            throw new CommunicationException("サーバーの fault 応答の形式が不正です。");
        }

        var faultCode = 0;
        if (faultStruct.Members.TryGetValue("faultCode", out var codeValue) && codeValue is XmlRpcInt intValue)
        {
            faultCode = intValue.Value;
        }

        var faultString = string.Empty;
        if (faultStruct.Members.TryGetValue("faultString", out var stringValue) && stringValue is XmlRpcString sv)
        {
            faultString = sv.Value;
        }

        return (faultCode, faultString);
    }

    private static XmlRpcValue ParseValue(XElement valueElement)
    {
        var typeElement = valueElement.Elements().FirstOrDefault();
        if (typeElement is null)
        {
            // XML-RPC の仕様上、型タグの無い <value> は string として扱う。
            return new XmlRpcString(valueElement.Value);
        }

        return typeElement.Name.LocalName switch
        {
            "string" => new XmlRpcString(typeElement.Value),
            "int" or "i4" => new XmlRpcInt(ParseInt(typeElement.Value)),
            "boolean" => new XmlRpcBoolean(ParseBoolean(typeElement.Value)),
            "array" => ParseArray(typeElement),
            "struct" => ParseStruct(typeElement),
            _ => throw new CommunicationException($"未対応の XML-RPC 型です: {typeElement.Name.LocalName}"),
        };
    }

    private static XmlRpcArray ParseArray(XElement arrayElement)
    {
        var data = arrayElement.Element("data")
            ?? throw new CommunicationException("array 応答に data がありません。");
        var items = data.Elements("value").Select(ParseValue).ToList();
        return new XmlRpcArray(items);
    }

    private static XmlRpcStruct ParseStruct(XElement structElement)
    {
        var members = new Dictionary<string, XmlRpcValue>();
        foreach (var member in structElement.Elements("member"))
        {
            var name = member.Element("name")?.Value
                ?? throw new CommunicationException("struct member に name がありません。");
            var valueElement = member.Element("value")
                ?? throw new CommunicationException("struct member に value がありません。");
            members[name] = ParseValue(valueElement);
        }

        return new XmlRpcStruct(members);
    }

    private static int ParseInt(string text)
    {
        if (!int.TryParse(text, out var value))
        {
            throw new CommunicationException("応答の int 値を解析できません。");
        }

        return value;
    }

    private static bool ParseBoolean(string text) => text.Trim() switch
    {
        "1" => true,
        "0" => false,
        _ => throw new CommunicationException("応答の boolean 値を解析できません。"),
    };
}
