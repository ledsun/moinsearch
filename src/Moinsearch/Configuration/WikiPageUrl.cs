namespace Moinsearch.Configuration;

/// <summary>
/// 検索結果URLから、設定済みWikiのページ名を復元する。
/// </summary>
internal static class WikiPageUrl
{
    public static bool TryGetPageName(Uri wikiUrl, string pageUrl, out string pageName)
    {
        pageName = string.Empty;
        if (!Uri.TryCreate(pageUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != wikiUrl.Scheme ||
            !string.Equals(uri.DnsSafeHost, wikiUrl.DnsSafeHost, StringComparison.OrdinalIgnoreCase) ||
            uri.Port != wikiUrl.Port ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var basePath = wikiUrl.AbsolutePath;
        if (!basePath.EndsWith('/'))
        {
            basePath += "/";
        }

        if (!uri.AbsolutePath.StartsWith(basePath, StringComparison.Ordinal) ||
            uri.AbsolutePath.Length == basePath.Length)
        {
            return false;
        }

        var encodedName = uri.AbsolutePath[basePath.Length..].Trim('/');
        if (encodedName.Length == 0)
        {
            return false;
        }

        try
        {
            pageName = Uri.UnescapeDataString(encodedName);
        }
        catch (UriFormatException)
        {
            return false;
        }

        return !string.IsNullOrEmpty(pageName) && !pageName.Contains('\0');
    }
}
