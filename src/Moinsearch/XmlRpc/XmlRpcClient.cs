using System.Net;
using System.Text;

namespace Moinsearch.XmlRpc;

/// <summary>
/// XML-RPC の HTTP 通信を担当する。<see cref="HttpClient"/> を外部から注入できるため、
/// テストでは <see cref="HttpMessageHandler"/> を差し替えて実サーバー無しで検証できる。
/// </summary>
internal sealed class XmlRpcClient(HttpClient httpClient, Uri endpoint)
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    public async Task<XmlRpcValue> CallAsync(
        string methodName,
        IReadOnlyList<XmlRpcValue> parameters,
        CancellationToken cancellationToken)
    {
        var requestBody = XmlRpcWriter.WriteMethodCall(methodName, parameters);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(RequestTimeout);

        string responseText;
        try
        {
            using var requestContent = new StringContent(requestBody, Encoding.UTF8, "text/xml");
            using var response = await httpClient.PostAsync(endpoint, requestContent, timeoutCts.Token)
                .ConfigureAwait(false);

            if (IsRedirect(response.StatusCode))
            {
                throw new CommunicationException(
                    $"サーバーがリダイレクト応答を返しました (HTTP {(int)response.StatusCode})。" +
                    "接続先URLの設定 (.moinsearch.toml の url) を確認してください。");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new CommunicationException($"サーバーがエラー応答を返しました (HTTP {(int)response.StatusCode})。");
            }

            responseText = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CommunicationException("サーバーへの接続がタイムアウトしました（20秒）。");
        }
        catch (HttpRequestException)
        {
            throw new CommunicationException("サーバーへの接続に失敗しました。接続先URLやネットワークを確認してください。");
        }

        return XmlRpcReader.ReadMethodResponse(responseText);
    }

    private static bool IsRedirect(HttpStatusCode statusCode) => (int)statusCode is >= 300 and < 400;
}
