using MsixInstallerComposer.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace MsixInstallerComposer.Helpers;

public static class WinAppCliBinaryDownloader
{
    private const string DownloadUrl = "https://nightly.link/microsoft/WinAppCli/workflows/build-package/main/cli-binaries.zip";
    private const string FileName = "cli-binaries.zip";

    private static readonly HttpClient s_httpClient = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

    public static async Task<string> DownloadAsync(string downloadDirectoryPath, IProgress<DownloadProgress> progress = null)
    {
        Directory.CreateDirectory(downloadDirectoryPath);
        var filePath = Path.Combine(downloadDirectoryPath, FileName);

        using var response = await s_httpClient.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = File.Create(filePath);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            totalRead += bytesRead;
            if (totalBytes > 0) progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalBytes });
        }

        if (totalBytes <= 0) progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalRead });

        return filePath;
    }
}
