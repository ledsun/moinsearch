using Moinsearch.Output;
using Moinsearch.Search;

namespace Moinsearch.Tests.Output;

public class ResultWriterTests
{
    [Fact]
    public void Write_NoResults_ProducesEmptyOutput()
    {
        var writer = new StringWriter { NewLine = "\n" };

        ResultWriter.Write(writer, []);

        Assert.Equal(string.Empty, writer.ToString());
    }

    [Fact]
    public void Write_MultipleResults_ProducesOneTabSeparatedLinePerResult()
    {
        var writer = new StringWriter { NewLine = "\n" };
        SearchResult[] results =
        [
            new SearchResult("PageA", "https://example.com/PageA"),
            new SearchResult("日本語ページ", "https://example.com/日本語ページ"),
        ];

        ResultWriter.Write(writer, results);

        Assert.Equal(
            "PageA\thttps://example.com/PageA\n日本語ページ\thttps://example.com/日本語ページ\n",
            writer.ToString());
    }

    [Fact]
    public void Sanitize_ReplacesControlCharactersWithReplacementCharacter()
    {
        var sanitized = ResultWriter.Sanitize("Page\tName\nWith\u0001Control");

        Assert.DoesNotContain('\t', sanitized);
        Assert.DoesNotContain('\n', sanitized);
        Assert.Contains('\uFFFD', sanitized);
    }

    [Fact]
    public void Write_PageNameWithTabAndNewline_DoesNotBreakLineStructure()
    {
        var writer = new StringWriter { NewLine = "\n" };
        SearchResult[] results = [new SearchResult("Evil\tPage\nName", "https://example.com/Evil")];

        ResultWriter.Write(writer, results);

        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Equal(1, lines[0].Count(c => c == '\t'));
    }
}
