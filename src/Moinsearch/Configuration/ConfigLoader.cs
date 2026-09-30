using Tomlyn;

namespace Moinsearch.Configuration;

/// <summary>
/// 環境変数と ~/.moinsearch.toml から設定を読み込み、検証する。
/// 同じ項目では環境変数を優先し、未定義ならTOMLへフォールバックする。
/// 環境変数が定義済みで空文字列の場合は設定エラーとする。
/// </summary>
internal sealed class ConfigLoader
{
    private const string UrlEnvironmentVariable = "MOINSEARCH_URL";
    private const string UsernameEnvironmentVariable = "MOINSEARCH_USERNAME";
    private const string PasswordEnvironmentVariable = "MOINSEARCH_PASSWORD";

    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string?> _readConfigFile;
    private readonly string _configFilePathForMessages;

    public ConfigLoader()
        : this(Environment.GetEnvironmentVariable, ReadDefaultConfigFile, DefaultConfigFilePath)
    {
    }

    /// <summary>
    /// テスト用のコンストラクタ。実環境変数・実ファイルに触れずに検証できるようにする。
    /// </summary>
    internal ConfigLoader(
        Func<string, string?> getEnvironmentVariable,
        Func<string?> readConfigFile,
        string configFilePathForMessages)
    {
        _getEnvironmentVariable = getEnvironmentVariable;
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

        var url = ResolveField(UrlEnvironmentVariable, tomlModel?.Url, "url");
        var username = ResolveField(UsernameEnvironmentVariable, tomlModel?.Username, "username");
        var password = ResolveField(PasswordEnvironmentVariable, tomlModel?.Password, "password");

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

    private string ResolveField(string environmentVariableName, string? tomlValue, string fieldName)
    {
        var envValue = _getEnvironmentVariable(environmentVariableName);
        if (envValue is not null)
        {
            if (envValue.Length == 0)
            {
                throw new ConfigurationException(
                    $"環境変数 {environmentVariableName} が空です。値を設定するか未設定にしてください。");
            }

            return envValue;
        }

        if (string.IsNullOrEmpty(tomlValue))
        {
            throw new ConfigurationException(
                $"{fieldName} が設定されていません。環境変数 {environmentVariableName} " +
                $"または設定ファイル ({_configFilePathForMessages}) の '{fieldName}' を指定してください。");
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
