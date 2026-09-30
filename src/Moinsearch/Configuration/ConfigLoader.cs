using Tomlyn;

namespace Moinsearch.Configuration;

/// <summary>
/// ~/.moinsearch.toml から設定を読み込み、検証する。
/// </summary>
internal sealed class ConfigLoader
{
    private readonly Func<string?> _readConfigFile;
    private readonly string _configFilePathForMessages;

    public ConfigLoader()
        : this(ReadDefaultConfigFile, DefaultConfigFilePath)
    {
    }

    /// <summary>
    /// テスト用のコンストラクタ。実ファイルに触れずに検証できるようにする。
    /// </summary>
    internal ConfigLoader(
        Func<string?> readConfigFile,
        string configFilePathForMessages)
    {
        _readConfigFile = readConfigFile;
        _configFilePathForMessages = configFilePathForMessages;
    }

    public static string DefaultConfigFilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".moinsearch.toml");

    private static string? ReadDefaultConfigFile()
    {
        var path = DefaultConfigFilePath;
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public MoinsearchConfig Load()
    {
        var tomlModel = ParseConfigFileIfPresent();

        var url = ResolveField(tomlModel?.Url, "url");
        var username = ResolveField(tomlModel?.Username, "username");
        var password = ResolveField(tomlModel?.Password, "password");

        var (baseUri, endpoint) = ValidateAndBuildUrl(url);

        return new MoinsearchConfig(baseUri, endpoint, username, password);
    }

    private TomlConfigModel? ParseConfigFileIfPresent()
    {
        string? content;
        try
        {
            content = _readConfigFile();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ConfigurationException(
                $"設定ファイル ({_configFilePathForMessages}) を読み込めませんでした。");
        }

        if (content is null)
        {
            return null;
        }

        try
        {
            return TomlSerializer.Deserialize(content, MoinsearchTomlContext.Default.TomlConfigModel);
        }
        catch (TomlException)
        {
            // TomlException のメッセージには構文エラー箇所のトークン文字列が含まれることがあるため、
            // 設定値を漏らさないよう独自の汎用メッセージに置き換える。
            throw new ConfigurationException(
                $"設定ファイル ({_configFilePathForMessages}) の TOML 構文が不正です。");
        }
    }

    private string ResolveField(string? tomlValue, string fieldName)
    {
        if (string.IsNullOrEmpty(tomlValue))
        {
            throw new ConfigurationException(
                $"設定ファイル ({_configFilePathForMessages}) に '{fieldName}' が設定されていません。");
        }

        return tomlValue;
    }

    private static (Uri BaseUri, Uri Endpoint) ValidateAndBuildUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ConfigurationException("url は絶対URLで指定してください。");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ConfigurationException("url は https:// で始まる必要があります。");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ConfigurationException("url にユーザー名やパスワードを含めることはできません。");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            throw new ConfigurationException("url にクエリ文字列を含めることはできません。");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ConfigurationException("url にフラグメント (#...) を含めることはできません。");
        }

        var baseText = uri.AbsoluteUri;
        var endpointText = baseText + (baseText.Contains('?') ? "&" : "?") + "action=xmlrpc2";
        return (uri, new Uri(endpointText, UriKind.Absolute));
    }
}
