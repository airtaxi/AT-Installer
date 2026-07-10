using MsixInstallerComposer.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MsixInstallerComposer.Shared.Helpers;

public static class GithubHelper
{
    private const string ApiBaseUrl = "https://api.github.com/repos";
    private const string UserAgent = "MsixInstallerComposer";

    private static readonly HttpClient s_httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<JsonArray> GetAllReleasesAsync(string repositoryOwner, string repositoryName, CancellationToken cancellationToken = default)
    {
        var requestUri = $"{ApiBaseUrl}/{repositoryOwner}/{repositoryName}/releases?per_page=100";
        using var response = await s_httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var releases = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return releases?.AsArray() ?? [];
    }

    public static async Task DownloadAssetAsync(string downloadUrl, string destinationFilePath, IProgress<DownloadProgress> progress = null, CancellationToken cancellationToken = default)
    {
        using var response = await s_httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFilePath) ?? string.Empty);
        await using var fileStream = File.Create(destinationFilePath);
        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;
            if (totalBytes > 0) progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalBytes });
        }
        if (totalBytes <= 0) progress?.Report(new DownloadProgress { BytesReceived = totalRead, TotalBytes = totalRead });
    }
}