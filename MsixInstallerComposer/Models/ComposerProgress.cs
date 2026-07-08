using MsixInstallerComposer.Enums;

namespace MsixInstallerComposer.Models;

public sealed class ComposerProgress
{
    public required string Message { get; init; }

    public required ComposerProgressStage Stage { get; init; }
}
