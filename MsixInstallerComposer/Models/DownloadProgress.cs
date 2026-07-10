namespace MsixInstallerComposer.Models;

public sealed class DownloadProgress
{
    public required long BytesReceived { get; init; }

    public required long TotalBytes { get; init; }

    public int Percentage => TotalBytes > 0 ? (int)(BytesReceived * 100 / TotalBytes) : 0;
}
