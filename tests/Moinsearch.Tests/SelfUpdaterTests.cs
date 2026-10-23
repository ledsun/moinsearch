using System.IO.Compression;
using Moinsearch;

namespace Moinsearch.Tests;

public class SelfUpdaterTests
{
    [Fact]
    public void ParseLatestRelease_ReturnsVersionAndExpectedAsset()
    {
        const string json =
            """
            {
              "tag_name": "v1.2.3",
              "assets": [
                {
                  "name": "moinsearch-v1.2.3-win-x64.zip",
                  "browser_download_url": "https://github.com/ledsun/moinsearch/releases/download/v1.2.3/moinsearch-v1.2.3-win-x64.zip"
                }
              ]
            }
            """;

        var release = SelfUpdater.ParseLatestRelease(json);

        Assert.Equal(new Version(1, 2, 3), release.Version);
        Assert.Equal(
            "https://github.com/ledsun/moinsearch/releases/download/v1.2.3/moinsearch-v1.2.3-win-x64.zip",
            release.AssetUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("v1.2")]
    [InlineData("v1.2.3.4")]
    [InlineData("v01.2.3")]
    public void ParseLatestRelease_RejectsInvalidTag(string tag)
    {
        var json = $$"""
            {
              "tag_name": "{{tag}}",
              "assets": []
            }
            """;

        Assert.Throws<InvalidDataException>(() => SelfUpdater.ParseLatestRelease(json));
    }

    [Fact]
    public void ParseLatestRelease_RejectsAssetOutsideGitHub()
    {
        const string json =
            """
            {
              "tag_name": "v1.2.3",
              "assets": [
                {
                  "name": "moinsearch-v1.2.3-win-x64.zip",
                  "browser_download_url": "https://example.com/moinsearch.zip"
                }
              ]
            }
            """;

        Assert.Throws<InvalidDataException>(() => SelfUpdater.ParseLatestRelease(json));
    }

    [Fact]
    public void ExtractExecutable_CopiesOnlyTheExpectedArchiveEntry()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"moinsearch-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        var archivePath = Path.Combine(temporaryDirectory, "release.zip");
        var executablePath = Path.Combine(temporaryDirectory, "moinsearch.exe");
        var executableContents = new byte[] { 1, 2, 3, 4 };

        try
        {
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                using var entryStream = archive.CreateEntry("moinsearch.exe").Open();
                entryStream.Write(executableContents);
            }

            SelfUpdater.ExtractExecutable(archivePath, executablePath);

            Assert.Equal(executableContents, File.ReadAllBytes(executablePath));
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Theory]
    [InlineData("1.2.3", "1.2.3", false)]
    [InlineData("1.2.3.0", "1.2.3", false)]
    [InlineData("1.2.3", "1.2.2", false)]
    [InlineData("1.2.3", "1.2.4", true)]
    [InlineData("1.2.3", "2.0.0", true)]
    public void IsUpdateAvailable_ComparesVersions(string current, string latest, bool expected)
    {
        var result = SelfUpdater.IsUpdateAvailable(
            Version.Parse(current),
            Version.Parse(latest));

        Assert.Equal(expected, result);
    }
}
