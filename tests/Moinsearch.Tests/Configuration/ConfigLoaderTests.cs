using Moinsearch.Configuration;

namespace Moinsearch.Tests.Configuration;

public class ConfigLoaderTests
{
    private const string ConfigPath = "/home/testuser/.moinsearch.toml";

    private static ConfigLoader CreateLoader(string? tomlContent, FakeCredentialStore? credentialStore = null)
    {
        return new ConfigLoader(
            () => tomlContent,
            ConfigPath,
            credentialStore ?? new FakeCredentialStore());
    }

    [Fact]
    public void Load_ReadsPasswordFromCredentialStore()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "YourWikiName"
            """;
        var credentialStore = new FakeCredentialStore("stored-password");

        var config = CreateLoader(toml, credentialStore).Load();

        Assert.Equal("https://wiki.example.com/", config.Url.AbsoluteUri);
        Assert.Equal("YourWikiName", config.Username);
        Assert.Equal("stored-password", config.Password);
        Assert.Equal("https://wiki.example.com/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
        Assert.Equal(config.Url, credentialStore.ReadWikiUrl);
    }

    [Fact]
    public void Load_UsesTomlSettingsAndStoredPassword()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "TomlUser"
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal("TomlUser", config.Username);
        Assert.Equal("credential-password", config.Password);
    }

    [Fact]
    public void Load_EmptyField_ThrowsConfigurationError()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = ""
            """;

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(toml).Load());
        Assert.Contains("username", ex.Message);
    }

    [Fact]
    public void Load_MissingUsername_ThrowsConfigurationError()
    {
        const string toml = "url = \"https://wiki.example.com/\"\n";

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(toml).Load());
        Assert.Contains("username", ex.Message);
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
            """;

        var config = CreateLoader(toml, new FakeCredentialStore(" secret with spaces ")).Load();

        Assert.Equal(" secret with spaces ", config.Password);
    }

    [Fact]
    public void Load_MissingStoredPassword_ThrowsConfigurationError()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "user"
            """;

        var ex = Assert.Throws<ConfigurationException>(
            () => CreateLoader(toml, new FakeCredentialStore(null)).Load());

        Assert.Contains("moinsearch auth set", ex.Message);
    }

    [Fact]
    public void LoadSettings_LegacyPlaintextPassword_ThrowsConfigurationError()
    {
        const string toml =
            """
            url = "https://wiki.example.com/"
            username = "user"
            password = "legacy-password"
            """;

        var ex = Assert.Throws<ConfigurationException>(() => CreateLoader(toml).LoadSettings());

        Assert.Contains("平文", ex.Message);
        Assert.DoesNotContain("legacy-password", ex.Message);
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
            """;

        var config = CreateLoader(toml).Load();

        Assert.Equal("https://example.com/wiki/mywiki/?action=xmlrpc2", config.XmlRpcEndpoint.AbsoluteUri);
    }

    private sealed class FakeCredentialStore(string? password = "credential-password") : ICredentialStore
    {
        public Uri? ReadWikiUrl { get; private set; }

        public string? Read(Uri wikiUrl)
        {
            ReadWikiUrl = wikiUrl;
            return password;
        }

        public void Write(Uri wikiUrl, string storedPassword)
        {
            ReadWikiUrl = wikiUrl;
            password = storedPassword;
        }
    }
}
