namespace MsixInstallerComposer.Models;

public sealed record SettingsPageUpdateCheckResult(string Title, string Message, string PrimaryButtonText, string SecondaryButtonText, bool ShouldOpenStoreAfterDialog);