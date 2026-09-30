using System.Net;
using Moinsearch.Search;
using Moinsearch.Tests.TestSupport;
using Moinsearch.XmlRpc;

namespace Moinsearch.Tests.Search;

public class MoinMoinSearchClientTests
{
    private static readonly Uri Endpoint = new("https://wiki.example.com/?action=xmlrpc2");

    private const string TokenResponse =
        """
        <?xml version="1.0"?>
        <methodResponse><params><param><value><string>TOKEN123</string></value></param></params></methodResponse>
        """;

    private const string EmptyTokenResponse =
        """
        <?xml version="1.0"?>
        <methodResponse><params><param><value><string></string></value></param></params></methodResponse>
        """;

    private const string TopLevelFaultResponse =
        """
        <?xml version="1.0"?>
        <methodResponse><fault><value><struct>
          <member><name>faultCode</name><value><int>1</int></value></member>
          <member><name>faultString</name><value><string>Invalid login</string></value></member>
        </struct></value></fault></methodResponse>
        """;

    private const string DeleteTokenResponse =
        """
        <?xml version="1.0"?>
        <methodResponse><params><param><value><boolean>1</boolean></value></param></params></methodResponse>
        """;

    private static string BuildMulticallResponse(bool authSuccess, bool searchFault, params (string Page, string Excerpt, string Url)[] results)
    {
        var authEntry = authSuccess
            ? "<value><array><data><value><string>SUCCESS</string></value></data></array></value>"
            : "<value><struct><member><name>faultCode</name><value><int>2</int></value></member>" +
              "<member><name>faultString</name><value><string>bad token</string></value></member></struct></value>";

        string searchEntry;
        if (searchFault)
        {
            searchEntry = "<value><struct><member><name>faultCode</name><value><int>3</int></value></member>" +
                          "<member><name>faultString</name><value><string>search failed</string></value></member></struct></value>";
        }
        else
        {
            var items = string.Concat(results.Select(r =>
                $"<value><array><data><value><string>{r.Page}</string></value>" +
                $"<value><string>{r.Excerpt}</string></value>" +
                $"<value><string>{r.Url}</string></value></data></array></value>"));
            // multicall の成功エントリは「戻り値1件を含む array」なので、
            // 実際の searchPagesEx の戻り値（三つ組の配列）をもう一段 <value> で包む。
            searchEntry = $"<value><array><data><value><array><data>{items}</data></array></value></data></array></value>";
        }

        return $"""
            <?xml version="1.0"?>
            <methodResponse><params><param><value><array><data>{authEntry}{searchEntry}</data></array></value></param></params></methodResponse>
            """;
    }

    private static MoinMoinSearchClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var xmlRpcClient = new XmlRpcClient(httpClient, Endpoint);
        return new MoinMoinSearchClient(xmlRpcClient);
    }

    [Fact]
    public async Task SearchAsync_SuccessfulFlow_ReturnsResultsSortedByPageName()
    {
        var multicall = BuildMulticallResponse(
            authSuccess: true,
            searchFault: false,
            ("PageB", "excerpt", "https://wiki.example.com/PageB"),
            ("PageA", "excerpt", "https://wiki.example.com/PageA"));

        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, multicall),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, DeleteTokenResponse));

        var client = CreateClient(handler);
        var results = await client.SearchAsync("user", "pass", "query", CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.Equal("PageA", results[0].PageName);
        Assert.Equal("PageB", results[1].PageName);
        Assert.Equal(3, handler.ReceivedRequestBodies.Count);
        Assert.Contains("deleteAuthToken", handler.ReceivedRequestBodies[2]);
    }

    [Fact]
    public async Task SearchAsync_ZeroResults_ReturnsEmptyList()
    {
        var multicall = BuildMulticallResponse(authSuccess: true, searchFault: false);

        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, multicall),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, DeleteTokenResponse));

        var client = CreateClient(handler);
        var results = await client.SearchAsync("user", "pass", "query", CancellationToken.None);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_EmptyAuthToken_ThrowsAuthenticationFailedException()
    {
        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, EmptyTokenResponse));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => client.SearchAsync("user", "pass", "query", CancellationToken.None));

        // トークンが得られていないので削除は試みない。
        Assert.Single(handler.ReceivedRequestBodies);
    }

    [Fact]
    public async Task SearchAsync_TopLevelFaultOnGetAuthToken_ThrowsAuthenticationFailedException()
    {
        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TopLevelFaultResponse));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => client.SearchAsync("user", "pass", "query", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_MulticallAuthFault_ThrowsAuthenticationFailedException_AndNeverReturnsSearchResults()
    {
        var multicall = BuildMulticallResponse(
            authSuccess: false,
            searchFault: false,
            ("SecretPage", "excerpt", "https://wiki.example.com/SecretPage"));

        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, multicall),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, DeleteTokenResponse));

        var client = CreateClient(handler);

        var ex = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => client.SearchAsync("user", "pass", "query", CancellationToken.None));

        Assert.DoesNotContain("SecretPage", ex.Message);
    }

    [Fact]
    public async Task SearchAsync_SearchFaultInMulticall_ThrowsCommunicationException()
    {
        var multicall = BuildMulticallResponse(authSuccess: true, searchFault: true);

        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, multicall),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, DeleteTokenResponse));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<CommunicationException>(
            () => client.SearchAsync("user", "pass", "query", CancellationToken.None));
    }

    [Fact]
    public async Task SearchAsync_DeleteAuthTokenFails_StillReturnsSuccessfulResults_AndWritesWarningToStderr()
    {
        var multicall = BuildMulticallResponse(
            authSuccess: true,
            searchFault: false,
            ("PageA", "excerpt", "https://wiki.example.com/PageA"));

        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, multicall),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.InternalServerError, "boom"));

        var client = CreateClient(handler);

        var originalError = Console.Error;
        var stderr = new StringWriter();
        Console.SetError(stderr);
        try
        {
            var results = await client.SearchAsync("user", "pass", "query", CancellationToken.None);
            Assert.Single(results);
        }
        finally
        {
            Console.SetError(originalError);
        }

        Assert.Contains("警告", stderr.ToString());
    }

    [Fact]
    public async Task SearchAsync_TimeoutDuringSearch_StillAttemptsSessionCleanup()
    {
        var handler = FakeHttpMessageHandler.Sequential(
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, TokenResponse),
            _ => throw new TaskCanceledException("simulated timeout"),
            _ => FakeHttpMessageHandler.XmlResponse(HttpStatusCode.OK, DeleteTokenResponse));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<CommunicationException>(
            () => client.SearchAsync("user", "pass", "query", CancellationToken.None));

        Assert.Equal(3, handler.ReceivedRequestBodies.Count);
        Assert.Contains("deleteAuthToken", handler.ReceivedRequestBodies[2]);
    }
}
