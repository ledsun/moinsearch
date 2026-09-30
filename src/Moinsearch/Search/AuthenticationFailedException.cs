namespace Moinsearch.Search;

/// <summary>
/// Wiki 認証に失敗したことが明確に判別できたことを表す（終了コード3）。
/// HTTP 403 だけでは判定しない。getAuthToken が空トークンを返した場合や、
/// system.multicall 内で applyAuthToken が失敗した場合にのみスローする。
/// </summary>
internal sealed class AuthenticationFailedException(string message) : Exception(message)
{
}
