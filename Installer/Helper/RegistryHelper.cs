using ImageMagick;
using InstallerCommons;
using Microsoft.Win32;
using System.Text;

namespace Installer.Helper;

public static class RegistryHelper
{
    private const string UninstallRegKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";
    public const string IconFileName = "_icon.ico";

    // Should be called after installation
    public static void ComposeUninstallerRegistryKey(UninstallManifest uninstallManifest)
    {
        using var currentUserKey = Registry.CurrentUser;
        var installManifest = uninstallManifest.InstallManifest;

        var registryKeyName = "{" + installManifest.Id.ToUpperInvariant() + "}";
        using var uninstallKey = currentUserKey.CreateSubKey(UninstallRegKeyPath + registryKeyName);

        // Set general properties
        uninstallKey.SetValue("DisplayName", installManifest.Name);
        uninstallKey.SetValue("DisplayVersion", uninstallManifest.InstalledVersion.ToString());
        uninstallKey.SetValue("Publisher", installManifest.Publisher);

        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(installManifest);

        // Setup additional properties
        uninstallKey.SetValue("InstallLocation", installationDirectoryPath);
        uninstallKey.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        uninstallKey.SetValue("NoModify", 1); // Batch file doesn't support modifying
        uninstallKey.SetValue("NoRepair", 1); // Batch file doesn't support repairing

        // Generate icon file and set it as the display icon
        var iconBinary = GenerateIconBinaryFromPng(installManifest.IconBinary ?? App.DefaultIconBinary);
        var iconFilePath = Path.Combine(installationDirectoryPath, IconFileName);
        File.WriteAllBytes(iconFilePath, iconBinary);
        uninstallKey.SetValue("DisplayIcon", iconFilePath);

        // Generate uninstall batch file and set it as the uninstall string and quiet uninstall string
        var uninstallBatchFilePath = GenerateUninstallationBatchFile(uninstallManifest);
        uninstallKey.SetValue("UninstallString", uninstallBatchFilePath);
        uninstallKey.SetValue("QuietUninstallString", uninstallBatchFilePath + " /quiet");
    }

    private static byte[] GenerateIconBinaryFromPng(byte[] png)
    {
        using var collection = new MagickImageCollection();

        // Define icon sizes
        uint[] sizes = [16, 32, 48, 64, 128, 256];

        // Generate icon images
        foreach (var size in sizes)
        {
            var image = new MagickImage(png);
            image.Resize(size, size);
            collection.Add(image);
        }

        // Write icon images to memory stream
        using var memoryStream = new MemoryStream();
        collection.Write(memoryStream, MagickFormat.Ico);

        // Return icon binary
        return memoryStream.ToArray();
    }

    private static string GenerateUninstallationBatchFile(UninstallManifest uninstallManifest)
    {
        var installManifest = uninstallManifest.InstallManifest;
        var installationDirectoryPath = Utils.GetInstallationDirectoryPath(installManifest);
        var uninstallationBatchFilePath = Path.Combine(installationDirectoryPath, "Uninstall.bat");

        StringBuilder batchFileContent = new();

        // Compose uninstall batch file content
        batchFileContent.AppendLine("@echo off");
        batchFileContent.AppendLine("chcp 65001");
        batchFileContent.AppendLine("echo Uninstalling... Do not close this window.");

        if (!string.IsNullOrWhiteSpace(uninstallManifest.ExecuteOnUninstall))
        {
            batchFileContent.AppendLine("echo Executing uninstallation script...");
            batchFileContent.AppendLine("cd " + installationDirectoryPath);
            batchFileContent.AppendLine("cmd /c " + uninstallManifest.ExecuteOnUninstall);
        }

        // Kill executable if it's running
        batchFileContent.AppendLine("echo Closing opened applications...");
        batchFileContent.AppendLine($"taskkill /f /im \"{installManifest.ExecutableFileName}\"");

        // Remove registry key
        batchFileContent.AppendLine("echo Unregistering uninstaller...");
        var registryKeyName = "{" + installManifest.Id.ToUpperInvariant() + "}";
        batchFileContent.AppendLine($"reg delete \"HKEY_CURRENT_USER\\{UninstallRegKeyPath}{registryKeyName}\" /f");

        // Delete program shortcut file
        batchFileContent.AppendLine("echo Deleting program shortcut file...");
        batchFileContent.AppendLine($"del \"{ShortcutHelper.GenerateShortcutPath(installManifest)}\" 2>nul");

        // Delete installed files
        batchFileContent.AppendLine("echo Deleting installation directory...");
        batchFileContent.AppendLine("cd /"); // Change directory to root to avoid "The process cannot access the file because it is being used by another process" error
        batchFileContent.AppendLine($"rmdir /s /q \"{installationDirectoryPath}\"");

        // Write batch file to disk
        File.WriteAllText(uninstallationBatchFilePath, batchFileContent.ToString());

        // Generate script for running uninstaller as administrator
        var runAsAdminBatchFilePath = Path.Combine(installationDirectoryPath, "RunAsAdmin_Uninstall.bat");
        StringBuilder runAsAdminBatchFileContent = new StringBuilder();
        runAsAdminBatchFileContent.AppendLine("@echo off");
        runAsAdminBatchFileContent.AppendFormat("powershell -Command \"Start-Process '{0}' -Verb runAs\"", uninstallationBatchFilePath);

        // Write the run as admin batch file to disk
        File.WriteAllText(runAsAdminBatchFilePath, runAsAdminBatchFileContent.ToString());

        // Return path of the run as admin batch file
        return runAsAdminBatchFilePath;
    }
}
