namespace Moinsearch.Configuration;

/// <summary>
/// 検証済みの moinsearch 設定。
/// </summary>
/// <param name="Url">Wiki のベースURL（末尾のクエリ・フラグメントなし）。</param>
/// <param name="XmlRpcEndpoint">XML-RPC エンドポイント（<c>?action=xmlrpc2</c> 付与済み）。</param>
/// <param name="Username">WikiName。</param>
/// <param name="Password">パスワード（Trimしない）。</param>
internal sealed record MoinsearchConfig(Uri Url, Uri XmlRpcEndpoint, string Username, string Password);
