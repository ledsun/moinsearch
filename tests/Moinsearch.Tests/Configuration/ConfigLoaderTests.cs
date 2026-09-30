using Moinsearch.Configuration;

namespace Moinsearch.Tests.Configuration;

public class ConfigLoaderTests
{
    private const string ConfigPath = "/home/testuser/.moinsearch.toml";

    private static ConfigLoader CreateLoader(string? tomlContent)
    {
        return new ConfigLoader(() => tomlContent, ConfigPath);
    }

    [Fact]
    public void Load_AllFromToml_Succeeds()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "YourWikiName"
            password = "your-password"
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal("https://wiki.example.com/", config.Url.AbsoluteUri);
        Assert.Equal("YourWikiName", config.Username);
        Assert.Equal("your-password", config.Password);
        Assert.Equal("https://wiki.example.com/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }

    [Fact]
    public void Load_UsesTomlValues()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "TomlUser"
            password = "toml-password"
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal("TomlUser", config.Username);
        Assert.Equal("toml-password", config.Password);
    }

    [Fact]
    public void Load_EmptyField_ThrowsConfigurationError()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = ""
            password = "secret"
            """;

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(toml).Load());
        Assert.Contains("username", ex.Message);
    }

    [Fact]
    public void Load_MissingFieldEverywhere_ThrowsConfigurationError()
    {
        const string toml = "url = \"https://wiki.example.com/\"\nusername = \"user\"\n";

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(toml).Load());
        Assert.Contains("password", ex.Message);
    }

    [Fact]
    public void Load_NoConfigFile_ThrowsConfigurationError()
    {
        Assert.Throws<ConfigurationException>(() => CreateLoader(tomlContent: null).Load());
    }

    [Fact]
    public void Load_InvalidToml_ThrowsConfigurationErrorWithoutLeakingValues()
    {
        const string invalidToml = "password = supersecretvalue123\n";

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(invalidToml).Load());
        Assert.DoesNotContain("supersecretvalue123", ex.Message);
    }

    [Fact]
    public void Load_PasswordIsNotTrimmed()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "user"
            password = " secret with spaces "
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal(" secret with spaces ", config.Password);
    }

    [Fact]
    public void Load_HttpUrl_ThrowsConfigurationError()
    {
        AssertInvalidUrl("http://wiki.example.com/");
    }

    [Fact]
    public void Load_UrlWithEmbeddedCredentials_ThrowsConfigurationError()
    {
        var userInfo = string.Concat("nam", "e", ":", "tok", "en");
        AssertInvalidUrl($"https://{userInfo}@wiki.example.com/");
    }

    [Fact]
    public void Load_UrlWithQuery_ThrowsConfigurationError()
    {
        AssertInvalidUrl("https://wiki.example.com/?query=1");
    }

    [Fact]
    public void Load_UrlWithFragment_ThrowsConfigurationError()
    {
        AssertInvalidUrl("https://wiki.example.com/#fragment");
    }

    [Fact]
    public void Load_NonAbsoluteUrl_ThrowsConfigurationError()
    {
        AssertInvalidUrl("not-a-url");
    }

    private static void AssertInvalidUrl(string url)
    {
        var toml = $"""
            url = "{url}"
            username = "user"
            password = "secret"
            """;

        Assert.Throws<ConfigurationException>(() => CreateLoader(toml).Load());
    }

    [Fact]
    public void Load_SubdirectoryUrl_IsAccepted()
    {
        const string toml =
            """
            url = "https://example.com/wiki/mywiki/"
            username = "user"
            password = "secret"
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal("https://example.com/wiki/mywiki/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }
}
