namespace Moinsearch.Search;

/// <summary>
/// 検索結果1件（ページ名とURL）。抜粋は仕様上使用しないため保持しない。
/// </summary>
internal sealed record SearchResult(string PageName, string Url);
