using Moinsearch.Cli;

namespace Moinsearch.Tests.Cli;

public class CommandLineParserTests
{
    [Fact]
    public void Parse_SearchCommand_ReturnsSearchTerm()
    {
        var parsed = CommandLineParser.Parse(["search", "議事録"]);

        Assert.Equal(CommandMode.Search, parsed.Mode);
        Assert.Equal("議事録", parsed.SearchTerm);
    }

    [Fact]
    public void Parse_GetCommand_ReturnsPageUrl()
    {
        var parsed = CommandLineParser.Parse(["get", "https://wiki.example.com/ページ"]);

        Assert.Equal(CommandMode.Get, parsed.Mode);
        Assert.Equal("https://wiki.example.com/ページ", parsed.PageUrl);
    }

    [Fact]
    public void Parse_AuthSetCommand_ReturnsAuthSet()
    {
        var parsed = CommandLineParser.Parse(["auth", "set"]);

        Assert.Equal(CommandMode.AuthSet, parsed.Mode);
    }

    [Fact]
    public void Parse_LegacySearchForm_ReturnsError()
    {
        var parsed = CommandLineParser.Parse(["議事録"]);

        Assert.Equal(CommandMode.Error, parsed.Mode);
    }

    [Fact]
    public void Parse_Help_ReturnsHelp()
    {
        var parsed = CommandLineParser.Parse(["--help"]);

        Assert.Equal(CommandMode.Help, parsed.Mode);
    }
}
