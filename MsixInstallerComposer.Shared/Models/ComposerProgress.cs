using MsixInstallerComposer.Shared.Enums;

namespace MsixInstallerComposer.Shared.Models;

public sealed class ComposerProgress
{
    public required string Message { get; init; }

    public required ComposerProgressStage Stage { get; init; }
}
