using Moinsearch.XmlRpc;

namespace Moinsearch.Tests.XmlRpc;

public class XmlRpcWriterReaderTests
{
    [Fact]
    public void WriteMethodCall_ProducesWellFormedXmlWithMethodNameAndParams()
    {
        var xml = XmlRpcWriter.WriteMethodCall(
            "getAuthToken",
            [XmlRpcValue.String("user"), XmlRpcValue.String("pass")]);

        Assert.Contains("<methodName>getAuthToken</methodName>", xml);
        Assert.Contains("utf-8", xml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteMethodCall_EscapesSpecialXmlCharacters()
    {
        var xml = XmlRpcWriter.WriteMethodCall(
            "search",
            [XmlRpcValue.String("A & B < C > \"quoted\"")]);

        Assert.DoesNotContain("A & B < C", xml);
        Assert.Contains("A &amp; B &lt; C", xml);
    }

    [Fact]
    public void WriteMethodCall_HandlesJapaneseText()
    {
        var xml = XmlRpcWriter.WriteMethodCall("search", [XmlRpcValue.String("日本語のページ")]);

        Assert.Contains("日本語のページ", xml);
    }

    [Fact]
    public void ReadMethodResponse_ParsesStringParam()
    {
        const string response =
            """
            <?xml version="1.0"?>
            <methodResponse><params><param><value><string>TOKEN123</string></value></param></params></methodResponse>
            """;

        var value = XmlRpcReader.ReadMethodResponse(response);

        Assert.Equal("TOKEN123", value.AsString());
    }

    [Fact]
    public void ReadMethodResponse_RoundTripsJapaneseAndSpecialCharacters()
    {
        var requestXml = XmlRpcWriter.WriteMethodCall(
            "search",
            [XmlRpcValue.String("日本語 & <タグ> \"引用符\"")]);

        // methodCall と methodResponse は同じ <value> 構造を使うため、往復確認に流用する。
        var responseXml = requestXml
            .Replace("methodCall", "methodResponse")
            .Replace("<methodName>search</methodName>", string.Empty);

        var value = XmlRpcReader.ReadMethodResponse(responseXml);

        Assert.Equal("日本語 & <タグ> \"引用符\"", value.AsString());
    }

    [Fact]
    public void ReadMethodResponse_ParsesArrayOfTriples()
    {
        const string response =
            """
            <?xml version="1.0"?>
            <methodResponse>
              <params><param><value><array><data>
                <value><array><data>
                  <value><string>PageA</string></value>
                  <value><string>excerpt</string></value>
                  <value><string>https://example.com/PageA</string></value>
                </data></array></value>
              </data></array></value></param></params>
            </methodResponse>
            """;

        var value = XmlRpcReader.ReadMethodResponse(response);
        var items = value.AsArray();

        Assert.Single(items);
        var triple = items[0].AsArray();
        Assert.Equal("PageA", triple[0].AsString());
        Assert.Equal("https://example.com/PageA", triple[2].AsString());
    }

    [Fact]
    public void ReadMethodResponse_ThrowsFaultException_ForTopLevelFault()
    {
        const string response =
            """
            <?xml version="1.0"?>
            <methodResponse>
              <fault><value><struct>
                <member><name>faultCode</name><value><int>1</int></value></member>
                <member><name>faultString</name><value><string>Bad login</string></value></member>
              </struct></value></fault>
            </methodResponse>
            """;

        var ex = Assert.Throws<XmlRpcFaultException>(() => XmlRpcReader.ReadMethodResponse(response));
        Assert.Equal(1, ex.FaultCode);
        Assert.Equal("Bad login", ex.FaultString);
    }

    [Fact]
    public void ReadMethodResponse_RejectsDoctype()
    {
        const string response =
            """
            <?xml version="1.0"?>
            <!DOCTYPE methodResponse [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <methodResponse><params><param><value><string>&xxe;</string></value></param></params></methodResponse>
            """;

        Assert.Throws<CommunicationException>(() => XmlRpcReader.ReadMethodResponse(response));
    }

    [Fact]
    public void ReadMethodResponse_ThrowsCommunicationException_ForMalformedXml()
    {
        Assert.Throws<CommunicationException>(() => XmlRpcReader.ReadMethodResponse("not xml at all"));
    }
}
