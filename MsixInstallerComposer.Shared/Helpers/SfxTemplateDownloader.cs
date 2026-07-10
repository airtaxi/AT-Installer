using MsixInstallerComposer.Shared.Models;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MsixInstallerComposer.Shared.Helpers;

public static class SfxTemplateDownloader
{
    private const string RepositoryOwner = "airtaxi";
    private const string RepositoryName = "AT-Installer";
    private const string AssetFileName = "Release.zip";

    public static async Task<string> GetLatestReleaseTagAsync()
    {
        var releases = await GithubHelper.GetAllReleasesAsync(RepositoryOwner, RepositoryName);
        foreach (var release in releases)
        {
            var tagName = release?["tag_name"]?.ToString();
            if (tagName is not null && tagName.StartsWith("SFX-", StringComparison.OrdinalIgnoreCase)) return tagName;
        }
        throw new InvalidOperationException("No SFX release found in the repository.");
    }

    public static async Task<string> DownloadAsync(string downloadDirectoryPath, string releaseTag, IProgress<DownloadProgress> progress = null)
    {
        var releases = await GithubHelper.GetAllReleasesAsync(RepositoryOwner, RepositoryName);
        JsonNode matchingRelease = null;
        var found = false;
        foreach (var release in releases)
        {
            var tagName = release?["tag_name"]?.ToString();
            if (tagName is not null && tagName.Equals(releaseTag, StringComparison.OrdinalIgnoreCase))
            {
                matchingRelease = release;
                found = true;
                break;
            }
        }
        if (!found || matchingRelease is null) throw new InvalidOperationException($"Release '{releaseTag}' not found.");
        string downloadUrl = null;
        foreach (var asset in matchingRelease["assets"]!.AsArray())
        {
            if (asset?["name"]?.ToString() == AssetFileName)
            {
                downloadUrl = asset["browser_download_url"]!.ToString();
                break;
            }
        }
        if (downloadUrl is null) throw new InvalidOperationException($"Asset '{AssetFileName}' not found in release '{releaseTag}'.");
        Directory.CreateDirectory(downloadDirectoryPath);
        var filePath = Path.Combine(downloadDirectoryPath, AssetFileName);
        await GithubHelper.DownloadAssetAsync(downloadUrl, filePath, progress);
        return filePath;
    }
}