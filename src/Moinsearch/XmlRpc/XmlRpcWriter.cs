using System.Globalization;
using System.Text;
using System.Xml;

namespace Moinsearch.XmlRpc;

/// <summary>
/// XML-RPC の methodCall を <see cref="XmlWriter"/> で安全に組み立てる（文字列連結は行わない）。
/// </summary>
internal static class XmlRpcWriter
{
    public static string WriteMethodCall(string methodName, IReadOnlyList<XmlRpcValue> parameters)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
        };

        var stringWriter = new Utf8StringWriter();
        using (var writer = XmlWriter.Create(stringWriter, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("methodCall");
            writer.WriteElementString("methodName", methodName);
            writer.WriteStartElement("params");
            foreach (var parameter in parameters)
            {
                writer.WriteStartElement("param");
                WriteValue(writer, parameter);
                writer.WriteEndElement(); // param
            }

            writer.WriteEndElement(); // params
            writer.WriteEndElement(); // methodCall
            writer.WriteEndDocument();
        }

        return stringWriter.ToString();
    }

    private static void WriteValue(XmlWriter writer, XmlRpcValue value)
    {
        writer.WriteStartElement("value");
        switch (value)
        {
            case XmlRpcString s:
                writer.WriteElementString("string", s.Value);
                break;
            case XmlRpcInt i:
                writer.WriteElementString("int", i.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case XmlRpcBoolean b:
                writer.WriteElementString("boolean", b.Value ? "1" : "0");
                break;
            case XmlRpcArray a:
                writer.WriteStartElement("array");
                writer.WriteStartElement("data");
                foreach (var item in a.Items)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndElement(); // data
                writer.WriteEndElement(); // array
                break;
            case XmlRpcStruct st:
                writer.WriteStartElement("struct");
                foreach (var (name, member) in st.Members)
                {
                    writer.WriteStartElement("member");
                    writer.WriteElementString("name", name);
                    WriteValue(writer, member);
                    writer.WriteEndElement(); // member
                }

                writer.WriteEndElement(); // struct
                break;
            default:
                throw new NotSupportedException($"未対応の XML-RPC 値です: {value.GetType()}");
        }

        writer.WriteEndElement(); // value
    }

    /// <summary>
    /// XmlWriter に「utf-8」の XML 宣言を出力させるための StringWriter。
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
