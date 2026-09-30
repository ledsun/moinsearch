using Moinsearch.XmlRpc;

namespace Moinsearch.Search;

/// <summary>
/// MoinMoin 1.9.11 の XML-RPC API に対して認証付き本文検索を行う。
/// 通信手順:
///   1. getAuthToken(username, password) でトークンを取得する（空トークンは認証失敗）。
///   2. system.multicall で applyAuthToken(token) と searchPagesEx(...) をまとめて呼び出す。
///      認証状態がHTTPリクエストをまたいで維持されるとは仮定しない。
///   3. applyAuthToken が "SUCCESS" であり、両方のサブ呼び出しが Fault でないことを確認してから
///      検索結果を返す。認証が正常でなければ検索結果は絶対に返さない。
///   4. 呼び出し後は必ず deleteAuthToken(token) を試みる（今回作成したセッションのみ破棄）。
/// </summary>
internal sealed class MoinMoinSearchClient(XmlRpcClient xmlRpcClient)
{
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string username,
        string password,
        string query,
        CancellationToken cancellationToken)
    {
        string? token = null;
        try
        {
            token = await GetAuthTokenAsync(username, password, cancellationToken).ConfigureAwait(false);
            return await SearchWithTokenAsync(token, query, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!string.IsNullOrEmpty(token))
            {
                await CleanupSessionAsync(token).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> GetAuthTokenAsync(string username, string password, CancellationToken cancellationToken)
    {
        XmlRpcValue result;
        try
        {
            result = await xmlRpcClient.CallAsync(
                "getAuthToken",
                [XmlRpcValue.String(username), XmlRpcValue.String(password)],
                cancellationToken).ConfigureAwait(false);
        }
        catch (XmlRpcFaultException)
        {
            throw new AuthenticationFailedException("認証に失敗しました。ユーザー名またはパスワードを確認してください。");
        }

        var token = result.AsString();
        if (string.IsNullOrEmpty(token))
        {
            throw new AuthenticationFailedException("認証に失敗しました。ユーザー名またはパスワードを確認してください。");
        }

        return token;
    }

    private async Task<IReadOnlyList<SearchResult>> SearchWithTokenAsync(
        string token,
        string query,
        CancellationToken cancellationToken)
    {
        var applyAuthCall = XmlRpcValue.Struct(new Dictionary<string, XmlRpcValue>
        {
            ["methodName"] = XmlRpcValue.String("applyAuthToken"),
            ["params"] = XmlRpcValue.Array([XmlRpcValue.String(token)]),
        });

        var searchCall = XmlRpcValue.Struct(new Dictionary<string, XmlRpcValue>
        {
            ["methodName"] = XmlRpcValue.String("searchPagesEx"),
            ["params"] = XmlRpcValue.Array(
            [
                XmlRpcValue.String(query),
                XmlRpcValue.String("text"),
                XmlRpcValue.Int(0),
                XmlRpcValue.Boolean(false),
                XmlRpcValue.Int(0),
                XmlRpcValue.Boolean(false),
            ]),
        });

        var multicallResult = await xmlRpcClient.CallAsync(
            "system.multicall",
            [XmlRpcValue.Array([applyAuthCall, searchCall])],
            cancellationToken).ConfigureAwait(false);

        var entries = multicallResult.AsArray();
        if (entries.Count != 2)
        {
            throw new CommunicationException("system.multicall の応答件数が不正です。");
        }

        var authOutcome = InterpretMulticallEntry(entries[0]);
        if (authOutcome.IsFault || authOutcome.Value!.AsString() != "SUCCESS")
        {
            // multicall は先行操作が Fault でも後続操作を実行し得るため、
            // 認証が正常に確認できない限り検索結果は絶対に読み取らず破棄する。
            throw new AuthenticationFailedException("認証セッションの確立に失敗しました。ユーザー名またはパスワードを確認してください。");
        }

        var searchOutcome = InterpretMulticallEntry(entries[1]);
        if (searchOutcome.IsFault)
        {
            throw new CommunicationException("検索処理でサーバーがエラーを返しました。");
        }

        return ParseSearchResults(searchOutcome.Value!);
    }

    private static (bool IsFault, XmlRpcValue? Value) InterpretMulticallEntry(XmlRpcValue entry)
    {
        switch (entry)
        {
            // 成功時は戻り値1件を含む array、失敗時は faultCode/faultString を持つ struct。
            case XmlRpcArray { Items.Count: 1 } array:
                return (false, array.Items[0]);
            case XmlRpcStruct:
                return (true, null);
            default:
                throw new CommunicationException("system.multicall の応答形式が不正です。");
        }
    }

    private static IReadOnlyList<SearchResult> ParseSearchResults(XmlRpcValue value)
    {
        var items = value.AsArray();
        var results = new List<SearchResult>(items.Count);
        foreach (var item in items)
        {
            var triple = item.AsArray();
            if (triple.Count < 3)
            {
                throw new CommunicationException("検索結果の形式が不正です。");
            }

            var pageName = triple[0].AsString();
            var url = triple[2].AsString();
            results.Add(new SearchResult(pageName, url));
        }

        return results.OrderBy(r => r.PageName, StringComparer.Ordinal).ToList();
    }

    private async Task CleanupSessionAsync(string token)
    {
        try
        {
            // 呼び出し元のキャンセル状態に関わらず、短いタイムアウトで破棄を試みる。
            using var cleanupCts = new CancellationTokenSource(CleanupTimeout);
            await xmlRpcClient.CallAsync(
                "deleteAuthToken",
                [XmlRpcValue.String(token)],
                cleanupCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is CommunicationException or XmlRpcFaultException or OperationCanceledException)
        {
            Console.Error.WriteLine("警告: 認証セッションの破棄に失敗しました（検索結果には影響ありません）。");
        }
    }
}
