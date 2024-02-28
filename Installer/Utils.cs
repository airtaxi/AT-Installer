using InstallerCommons;

namespace InstallerCommons;

public static class Utils
{
    public static string GetInstallationDirectoryPath(InstallManifest installManifest)
    {
        // Compose installation directory path
        var roamingDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // InstallManifest.InstallationFolderName will not contain illegal characters.
        // It will be safely set by the InstallerComposer.
        var safeName = installManifest.InstallationFolderName;

        // Combine the paths
        var installationDirectoryPath = Path.Combine(roamingDirectoryPath, safeName);

        // create directory if it doesn't exist
        if (!Directory.Exists(installationDirectoryPath)) Directory.CreateDirectory(installationDirectoryPath);

        // return installation directory path
        return installationDirectoryPath;
    }

    public static string RemoveIllegalCharacters(string input) => new(input.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
}
