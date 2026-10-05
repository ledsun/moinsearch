using System.Net;
using System.Text;
using Moinsearch;
using Moinsearch.Cli;
using Moinsearch.Configuration;
using Moinsearch.Output;
using Moinsearch.Search;
using Moinsearch.XmlRpc;

Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var parsedArguments = CommandLineParser.Parse(args);

switch (parsedArguments.Mode)
{
    case CommandMode.Help:
        Console.WriteLine(UsageText.Text);
        return ExitCode.Success;

    case CommandMode.Error:
        Console.Error.WriteLine(parsedArguments.ErrorMessage);
        Console.Error.WriteLine();
        Console.Error.WriteLine(UsageText.Text);
        return ExitCode.UsageOrConfigurationError;

    case CommandMode.AuthSet:
        return await RunAuthSetAsync().ConfigureAwait(false);
}

MoinsearchConfig config;
try
{
    config = new ConfigLoader().Load();
}
catch (ConfigurationException ex)
{
    Console.Error.WriteLine($"設定エラー: {ex.Message}");
    return ExitCode.UsageOrConfigurationError;
}

using var userCancellation = new CancellationTokenSource();
ConsoleCancelEventHandler cancelHandler = (_, cancelEventArgs) =>
{
    cancelEventArgs.Cancel = true;
    userCancellation.Cancel();
};
Console.CancelKeyPress += cancelHandler;

try
{
    using var httpClientHandler = new HttpClientHandler { AllowAutoRedirect = false };
    using var httpClient = new HttpClient(httpClientHandler);
    var xmlRpcClient = new XmlRpcClient(httpClient, config.XmlRpcEndpoint);
    var searchClient = new MoinMoinSearchClient(xmlRpcClient);

    if (parsedArguments.Mode == CommandMode.Search)
    {
        var results = await searchClient
            .SearchAsync(config.Username, config.Password, parsedArguments.SearchTerm!, userCancellation.Token)
            .ConfigureAwait(false);
        ResultWriter.Write(Console.Out, results);
    }
    else
    {
        if (!WikiPageUrl.TryGetPageName(config.Url, parsedArguments.PageUrl!, out var pageName))
        {
            Console.Error.WriteLine("指定されたURLは設定済みWikiのページURLではありません。");
            return ExitCode.UsageOrConfigurationError;
        }

        var page = await searchClient
            .GetPageAsync(config.Username, config.Password, pageName, userCancellation.Token)
            .ConfigureAwait(false);
        Console.Write(page);
    }

    return ExitCode.Success;
}
catch (OperationCanceledException) when (userCancellation.IsCancellationRequested)
{
    Console.Error.WriteLine("処理はユーザーによってキャンセルされました。");
    return ExitCode.Cancelled;
}
catch (AuthenticationFailedException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCode.AuthenticationFailure;
}
catch (CommunicationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ExitCode.ExecutionError;
}
catch (XmlRpcFaultException)
{
    Console.Error.WriteLine("サーバーがエラーを返しました。");
    return ExitCode.ExecutionError;
}
finally
{
    Console.CancelKeyPress -= cancelHandler;
}

static async Task<int> RunAuthSetAsync()
{
    var configLoader = new ConfigLoader();
    MoinsearchSettings settings;
    try
    {
        settings = configLoader.LoadSettings();
    }
    catch (ConfigurationException ex)
    {
        Console.Error.WriteLine($"設定エラー: {ex.Message}");
        return ExitCode.UsageOrConfigurationError;
    }

    string password;
    try
    {
        password = PasswordPrompt.Read();
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return ExitCode.UsageOrConfigurationError;
    }

    using var userCancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancelHandler = (_, cancelEventArgs) =>
    {
        cancelEventArgs.Cancel = true;
        userCancellation.Cancel();
    };
    Console.CancelKeyPress += cancelHandler;

    try
    {
        using var httpClientHandler = new HttpClientHandler { AllowAutoRedirect = false };
        using var httpClient = new HttpClient(httpClientHandler);
        var xmlRpcClient = new XmlRpcClient(httpClient, settings.XmlRpcEndpoint);
        var searchClient = new MoinMoinSearchClient(xmlRpcClient);

        await searchClient
            .ValidateCredentialsAsync(settings.Username, password, userCancellation.Token)
            .ConfigureAwait(false);

        configLoader.SavePassword(settings, password);
        Console.WriteLine("認証に成功し、Windows Credential Managerにパスワードを保存しました。");
        return ExitCode.Success;
    }
    catch (OperationCanceledException) when (userCancellation.IsCancellationRequested)
    {
        Console.Error.WriteLine("処理はユーザーによってキャンセルされました。");
        return ExitCode.Cancelled;
    }
    catch (AuthenticationFailedException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return ExitCode.AuthenticationFailure;
    }
    catch (CommunicationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        return ExitCode.ExecutionError;
    }
    catch (CredentialStoreException ex)
    {
        Console.Error.WriteLine($"資格情報の保存に失敗しました: {ex.Message}");
        return ExitCode.UsageOrConfigurationError;
    }
    catch (XmlRpcFaultException)
    {
        Console.Error.WriteLine("サーバーがエラーを返しました。");
        return ExitCode.ExecutionError;
    }
    finally
    {
        Console.CancelKeyPress -= cancelHandler;
    }
}
