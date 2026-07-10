namespace MsixInstallerComposer.Models;

public sealed record SettingsPageDialogData(string Title, string Message, string PrimaryButtonText = null, string SecondaryButtonText = null, bool ShouldNavigateToSettingsAfterClose = false);