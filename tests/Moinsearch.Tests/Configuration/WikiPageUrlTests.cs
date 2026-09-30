using Moinsearch.Configuration;

namespace Moinsearch.Tests.Configuration;

public class WikiPageUrlTests
{
    private static readonly Uri WikiUrl = new("https://wiki.example.com/moin/");

    [Theory]
    [InlineData("https://wiki.example.com/moin/議事録2025年度", "議事録2025年度")]
    [InlineData("https://wiki.example.com/moin/%E8%AD%B0%E4%BA%8B%E9%8C%B2%2F2025", "議事録/2025")]
    public void TryGetPageName_ValidUrl_ReturnsDecodedPageName(string url, string expected)
    {
        Assert.True(WikiPageUrl.TryGetPageName(WikiUrl, url, out var pageName));
        Assert.Equal(expected, pageName);
    }

    [Theory]
    [InlineData("https://other.example.com/moin/Page")]
    [InlineData("https://wiki.example.com/other/Page")]
    [InlineData("https://wiki.example.com/moin/")]
    [InlineData("https://wiki.example.com/moin/Page?x=1")]
    public void TryGetPageName_InvalidUrl_IsRejected(string url)
    {
        Assert.False(WikiPageUrl.TryGetPageName(WikiUrl, url, out _));
    }
}
