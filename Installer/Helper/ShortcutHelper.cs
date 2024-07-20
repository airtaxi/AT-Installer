using ShellLink;

namespace InstallerCommons.Helper;

public static class ShortcutHelper
{
    public static string CreateShortcutToProgramsFolder(InstallManifest installManifest)
    {
        // Get Application executable path and icon path
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(installManifest);
        var applicationExecutablePath = Path.Combine(installationDirectoryPath, installManifest.ExecutableFileName);
        var applicationIconPath = Path.Combine(installationDirectoryPath, RegistryHelper.IconFileName);

        // Compose shortcut path
        var shortcutPath = GenerateShortcutPath(installManifest);

        // Create shortcut
        Shortcut.CreateShortcut(applicationExecutablePath, applicationIconPath, 0).WriteToFile(shortcutPath);

        // Return shortcut path
        return shortcutPath;
    }

    public static string GenerateShortcutPath(InstallManifest installManifest)
    {
        var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        var safeName = Utils.RemoveIllegalCharacters(installManifest.Name);
        var shortcutPath = Path.Combine(startMenuPath, safeName + ".lnk");
        return shortcutPath;
    }
}