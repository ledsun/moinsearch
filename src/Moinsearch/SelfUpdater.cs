using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Moinsearch.Cli;

namespace Moinsearch;

internal sealed record LatestRelease(Version Version, Uri AssetUri);

internal static class SelfUpdater
{
    private const string ReleaseApiUrl = "https://api.github.com/repos/ledsun/moinsearch/releases/latest";
    private const string ApplyUpdateArgument = "--apply-update";
    private const string CleanupUpdateArgument = "--cleanup-update";
    private const long MaximumDownloadSize = 100 * 1024 * 1024;

    public static bool IsInternalCommand(string[] args) =>
        args.Length > 0 && args[0] is ApplyUpdateArgument or CleanupUpdateArgument;

    public static Task<int> RunInternalCommandAsync(string[] args)
    {
        try
        {
            if (args[0] == ApplyUpdateArgument && args.Length == 5 &&
                int.TryParse(args[1], out var parentProcessId))
            {
                return Task.FromResult(ApplyUpdate(parentProcessId, args[2], args[3], args[4]));
            }

            if (args[0] == CleanupUpdateArgument && args.Length == 3 &&
                int.TryParse(args[1], out var helperProcessId))
            {
                WaitForProcessExit(helperProcessId);
                Directory.Delete(args[2], recursive: true);
                return Task.FromResult(ExitCode.Success);
            }

            Console.Error.WriteLine("更新処理の内部引数が正しくありません。");
            return Task.FromResult(ExitCode.UsageOrConfigurationError);
        }
        catch (Exception ex) when (IsUpdateError(ex))
        {
            Console.Error.WriteLine($"更新に失敗しました: {ex.Message}");
            return Task.FromResult(ExitCode.ExecutionError);
        }
    }

    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            Console.Error.WriteLine("self-update は Windows x64 でのみ使用できます。");
            return ExitCode.UsageOrConfigurationError;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !string.Equals(Path.GetFileName(executablePath), "moinsearch.exe", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("self-update は配布された moinsearch.exe から実行してください。");
            return ExitCode.UsageOrConfigurationError;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (currentVersion is null)
        {
            Console.Error.WriteLine("現在のバージョン情報を取得できませんでした。");
            return ExitCode.ExecutionError;
        }

        string? updateDirectory = null;
        var helperStarted = false;
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("moinsearch-self-update");
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            using var releaseResponse = await httpClient.GetAsync(ReleaseApiUrl).ConfigureAwait(false);
            releaseResponse.EnsureSuccessStatusCode();
            var releaseJson = await releaseResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var release = ParseLatestRelease(releaseJson);

            if (!IsUpdateAvailable(currentVersion, release.Version))
            {
                Console.WriteLine($"moinsearch {currentVersion.ToString(3)} は最新です。");
                return ExitCode.Success;
            }

            updateDirectory = Path.Combine(Path.GetTempPath(), $"moinsearch-update-{Guid.NewGuid():N}");
            Directory.CreateDirectory(updateDirectory);
            var archivePath = Path.Combine(updateDirectory, "release.zip");
            var stagedExecutablePath = Path.Combine(updateDirectory, "moinsearch.exe");

            await DownloadFileAsync(httpClient, release.AssetUri, archivePath).ConfigureAwait(false);
            ExtractExecutable(archivePath, stagedExecutablePath);
            EnsureTargetDirectoryIsWritable(executablePath);

            var startInfo = new ProcessStartInfo(stagedExecutablePath)
            {
                UseShellExecute = false,
                WorkingDirectory = updateDirectory,
            };
            startInfo.ArgumentList.Add(ApplyUpdateArgument);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            startInfo.ArgumentList.Add(Path.GetFullPath(executablePath));
            startInfo.ArgumentList.Add(stagedExecutablePath);
            startInfo.ArgumentList.Add(updateDirectory);

            if (Process.Start(startInfo) is null)
            {
                throw new InvalidOperationException("更新処理を開始できませんでした。");
            }

            helperStarted = true;
            Console.WriteLine($"moinsearch {release.Version} をダウンロードしました。終了後に更新します。");
            return ExitCode.Success;
        }
        catch (Exception ex) when (IsUpdateError(ex))
        {
            if (!helperStarted && updateDirectory is not null && Directory.Exists(updateDirectory))
            {
                try
                {
                    Directory.Delete(updateDirectory, recursive: true);
                }
                catch (Exception cleanupException) when (cleanupException is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine(
                        $"警告: 一時ファイルを削除できませんでした: {updateDirectory} ({cleanupException.Message})");
                }
            }

            Console.Error.WriteLine($"更新の確認または適用に失敗しました: {ex.Message}");
            return ExitCode.ExecutionError;
        }
    }

    internal static LatestRelease ParseLatestRelease(string releaseJson)
    {
        using var document = JsonDocument.Parse(releaseJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("tag_name", out var tagElement) ||
            tagElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("GitHub Releaseにtag_nameがありません。");
        }

        var tag = tagElement.GetString();
        if (tag is null ||
            !System.Text.RegularExpressions.Regex.IsMatch(tag, @"^v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$") ||
            !Version.TryParse(tag[1..], out var version) ||
            version.Major < 0 || version.Minor < 0 || version.Build < 0)
        {
            throw new InvalidDataException($"Release tag '{tag}' は vMAJOR.MINOR.PATCH 形式ではありません。");
        }

        var expectedAssetName = $"moinsearch-{tag}-win-x64.zip";
        if (!root.TryGetProperty("assets", out var assetsElement) ||
            assetsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub Releaseに配布ファイルがありません。");
        }

        foreach (var asset in assetsElement.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object ||
                !asset.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String ||
                nameElement.GetString() != expectedAssetName ||
                !asset.TryGetProperty("browser_download_url", out var urlElement) ||
                urlElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var urlText = urlElement.GetString();
            if (Uri.TryCreate(urlText, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps &&
                string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                return new LatestRelease(version, uri);
            }
        }

        throw new InvalidDataException($"Releaseに {expectedAssetName} がありません。");
    }

    internal static bool IsUpdateAvailable(Version currentVersion, Version latestVersion) =>
        latestVersion > currentVersion;

    private static async Task DownloadFileAsync(HttpClient httpClient, Uri uri, string destinationPath)
    {
        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is > MaximumDownloadSize)
        {
            throw new InvalidDataException("更新ファイルが許容サイズを超えています。");
        }

        await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var destination = File.Create(destinationPath);
        var buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            totalBytes += bytesRead;
            if (totalBytes > MaximumDownloadSize)
            {
                throw new InvalidDataException("更新ファイルが許容サイズを超えています。");
            }

            await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
        }
    }

    internal static void ExtractExecutable(string archivePath, string destinationPath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        var executableEntries = archive.Entries
            .Where(entry => entry.FullName == "moinsearch.exe")
            .ToArray();
        if (executableEntries.Length != 1 || executableEntries[0].Length is <= 0 or > MaximumDownloadSize)
        {
            throw new InvalidDataException("Release zipに有効なmoinsearch.exeがありません。");
        }

        using var source = executableEntries[0].Open();
        using var destination = File.Create(destinationPath);
        source.CopyTo(destination);
        if (destination.Length > MaximumDownloadSize)
        {
            throw new InvalidDataException("実行ファイルが許容サイズを超えています。");
        }
    }

    private static void EnsureTargetDirectoryIsWritable(string executablePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(executablePath))
            ?? throw new InvalidOperationException("実行ファイルの場所を特定できませんでした。");
        var probePath = Path.Combine(directory, $".moinsearch-write-test-{Guid.NewGuid():N}");
        using (File.Create(probePath))
        {
        }

        File.Delete(probePath);
    }

    private static int ApplyUpdate(int parentProcessId, string targetPath, string stagedExecutablePath, string updateDirectory)
    {
        WaitForProcessExit(parentProcessId);

        targetPath = Path.GetFullPath(targetPath);
        if (!File.Exists(targetPath) || !File.Exists(stagedExecutablePath))
        {
            throw new FileNotFoundException("更新対象またはダウンロードした実行ファイルが見つかりません。");
        }

        var targetDirectory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("更新先のディレクトリを特定できませんでした。");
        var suffix = Guid.NewGuid().ToString("N");
        var replacementPath = Path.Combine(targetDirectory, $".moinsearch-{suffix}.new");
        var backupPath = Path.Combine(targetDirectory, $".moinsearch-{suffix}.bak");

        try
        {
            File.Copy(stagedExecutablePath, replacementPath);
            File.Replace(replacementPath, targetPath, backupPath, ignoreMetadataErrors: true);
        }
        finally
        {
            DeleteIfExists(replacementPath);
        }

        try
        {
            DeleteIfExists(backupPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"警告: 更新前のバックアップを削除できませんでした: {backupPath} ({ex.Message})");
        }

        try
        {
            var cleanupInfo = new ProcessStartInfo(targetPath)
            {
                UseShellExecute = false,
                WorkingDirectory = targetDirectory,
            };
            cleanupInfo.ArgumentList.Add(CleanupUpdateArgument);
            cleanupInfo.ArgumentList.Add(Environment.ProcessId.ToString());
            cleanupInfo.ArgumentList.Add(updateDirectory);
            if (Process.Start(cleanupInfo) is null)
            {
                Console.Error.WriteLine($"警告: 一時ファイルを削除できませんでした: {updateDirectory}");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"警告: 一時ファイルを削除できませんでした: {updateDirectory} ({ex.Message})");
        }

        Console.WriteLine("moinsearch を更新しました。");
        return ExitCode.Success;
    }

    private static void WaitForProcessExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.WaitForExit();
        }
        catch (ArgumentException)
        {
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsUpdateError(Exception exception) =>
        exception is HttpRequestException
            or OperationCanceledException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or Win32Exception
            or InvalidOperationException
            or ArgumentException
            or NotSupportedException;
}
