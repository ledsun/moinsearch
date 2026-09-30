using Moinsearch.Configuration;

namespace Moinsearch.Tests.Configuration;

public class ConfigLoaderTests
{
    private const string ConfigPath = "/home/testuser/.moinsearch.toml";

    private static ConfigLoader CreateLoader(
        IReadOnlyDictionary<string, string> environment,
        string? tomlContent)
    {
        return new ConfigLoader(
            name => environment.TryGetValue(name, out var value) ? value : null,
            () => tomlContent,
            ConfigPath);
    }

    [Fact]
    public void Load_AllFromEnvironmentVariables_Succeeds()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/",
            ["MOINSEARCH_USERNAME"] = "YourWikiName",
            ["MOINSEARCH_PASSWORD"] = "your-password",
        };

        var config = CreateLoader(env, tomlContent: null).Load();

        Assert.Equal("https://wiki.example.com/", config.Url.AbsoluteUri);
        Assert.Equal("YourWikiName", config.Username);
        Assert.Equal("your-password", config.Password);
        Assert.Equal("https://wiki.example.com/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }

    [Fact]
    public void Load_EnvironmentVariableTakesPriorityOverToml()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_USERNAME"] = "EnvUser",
        };
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "TomlUser"
            password = "toml-password"
            """;

        var config = CreateLoader(env, toml).Load();

        Assert.Equal("EnvUser", config.Username);
        Assert.Equal("toml-password", config.Password);
    }

    [Fact]
    public void Load_EnvironmentVariableDefinedButEmpty_ThrowsConfigurationError()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/",
            ["MOINSEARCH_USERNAME"] = "",
            ["MOINSEARCH_PASSWORD"] = "secret",
        };

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(env, null).Load());
        Assert.Contains("MOINSEARCH_USERNAME", ex.Message);
    }

    [Fact]
    public void Load_MissingFieldEverywhere_ThrowsConfigurationError()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/",
            ["MOINSEARCH_USERNAME"] = "user",
        };

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(env, null).Load());
        Assert.Contains("password", ex.Message);
    }

    [Fact]
    public void Load_NoConfigFileButAllEnvironmentVariablesPresent_Succeeds()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/sub/",
            ["MOINSEARCH_USERNAME"] = "user",
            ["MOINSEARCH_PASSWORD"] = "secret",
        };

        var config = CreateLoader(env, tomlContent: null).Load();

        Assert.Equal("https://wiki.example.com/sub/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }

    [Fact]
    public void Load_InvalidToml_ThrowsConfigurationErrorWithoutLeakingValues()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/",
            ["MOINSEARCH_USERNAME"] = "user",
            ["MOINSEARCH_PASSWORD"] = "unrelated",
        };
        const string invalidToml = "password = supersecretvalue123\n";

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(env, invalidToml).Load());
        Assert.DoesNotContain("supersecretvalue123", ex.Message);
    }

    [Fact]
    public void Load_PasswordIsNotTrimmed()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://wiki.example.com/",
            ["MOINSEARCH_USERNAME"] = "user",
            ["MOINSEARCH_PASSWORD"] = " secret with spaces ",
        };

        var config = CreateLoader(env, null).Load();

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
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = url,
            ["MOINSEARCH_USERNAME"] = "user",
            ["MOINSEARCH_PASSWORD"] = "secret",
        };

        Assert.Throws<ConfigurationException>(() => CreateLoader(env, null).Load());
    }

    [Fact]
    public void Load_SubdirectoryUrl_IsAccepted()
    {
        var env = new Dictionary<string, string>
        {
            ["MOINSEARCH_URL"] = "https://example.com/wiki/mywiki/",
            ["MOINSEARCH_USERNAME"] = "user",
            ["MOINSEARCH_PASSWORD"] = "secret",
        };

        var config = CreateLoader(env, null).Load();

        Assert.Equal("https://example.com/wiki/mywiki/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }
}
