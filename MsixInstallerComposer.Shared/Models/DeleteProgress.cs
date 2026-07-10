namespace MsixInstallerComposer.Shared.Models;

public sealed class DeleteProgress
{
    public required int DeletedFiles { get; init; }

    public required int TotalFiles { get; init; }

    public int Percentage => TotalFiles > 0 ? (int)(DeletedFiles * 100 / TotalFiles) : 0;
}