using System.Net;

namespace Moinsearch.Tests.TestSupport;

/// <summary>
/// テストで HTTP 通信を差し替えるためのフェイクハンドラ。
/// 実サーバーの認証情報が無くても XML-RPC クライアントの挙動を検証できるようにする。
/// </summary>
internal sealed class FakeHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    : HttpMessageHandler
{
    public List<string> ReceivedRequestBodies { get; } = [];

    public static FakeHttpMessageHandler Sequential(params Func<string, HttpResponseMessage>[] responders)
    {
        var callIndex = 0;
        return new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var index = Math.Min(callIndex, responders.Length - 1);
            callIndex++;
            return responders[index](body);
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ReceivedRequestBodies.Add(body);
        }

        return await responder(request, cancellationToken).ConfigureAwait(false);
    }

    public static HttpResponseMessage XmlResponse(HttpStatusCode statusCode, string xmlBody) => new(statusCode)
    {
        Content = new StringContent(xmlBody, System.Text.Encoding.UTF8, "text/xml"),
    };
}
